using ClubStack.Application.Exceptions;
using ClubStack.Application.Membership;
using ClubStack.Domain.Common;
using ClubStack.Domain.Membership;
using ClubStack.Infrastructure.Persistence;
using ClubStack.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClubStack.Infrastructure.Membership;

/// <inheritdoc />
public sealed class EntityFrameworkMembershipDirectoryService : IMembershipDirectoryService
{
    private readonly ClubStackDbContext _context;
    private readonly ISystemClock _clock;

    public EntityFrameworkMembershipDirectoryService(ClubStackDbContext context, ISystemClock clock)
    {
        _context = context;
        _clock = clock;
    }

    public async Task<PersonCreationResult> CreatePersonAsync(CreatePersonInput input, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input.LegalName))
        {
            throw new ValidationException(
                "The person could not be created.",
                MembershipServiceHelpers.FieldError("legalName", "A legal name is required."));
        }

        var warnings = await FindDuplicateWarningsAsync(input, cancellationToken);

        var person = new PersonEntity
        {
            LegalName = input.LegalName.Trim(),
            PreferredName = string.IsNullOrWhiteSpace(input.PreferredName) ? string.Empty : input.PreferredName.Trim(),
            IdNumber = Normalize(input.IdNumber),
            DateOfBirth = input.DateOfBirth,
            Email = Normalize(input.Email),
            Phone = Normalize(input.Phone),
            AddressLine1 = input.AddressLine1 ?? string.Empty,
            AddressLine2 = input.AddressLine2 ?? string.Empty,
            City = input.City ?? string.Empty,
            Region = input.Region ?? string.Empty,
            PostalCode = input.PostalCode ?? string.Empty,
            Country = input.Country ?? string.Empty,
            Notes = input.Notes ?? string.Empty,
            IsActive = true,
        };

        _context.Persons.Add(person);
        await _context.SaveChangesAsync(cancellationToken);

        return new PersonCreationResult(ToPersonDto(person), warnings);
    }

    public async Task<PersonDto> GetPersonAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var person = await _context.Persons
            .Include(p => p.Member)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Person '{id}' was not found.");
        return ToPersonDto(person);
    }

    public async Task<PersonSearchResult> SearchPersonsAsync(PersonSearchInput input, CancellationToken cancellationToken = default)
    {
        var (page, pageSize) = NormalizePage(input.Page, input.PageSize);
        IQueryable<PersonEntity> query = _context.Persons.Include(p => p.Member).AsNoTracking();

        if (!string.IsNullOrWhiteSpace(input.Query))
        {
            var term = input.Query.Trim();
            query = query.Where(p =>
                p.LegalName.Contains(term) ||
                p.PreferredName.Contains(term) ||
                (p.Email != null && p.Email.Contains(term)) ||
                (p.Phone != null && p.Phone.Contains(term)) ||
                (p.Member != null && p.Member.MemberNumber.Contains(term)));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(p => p.LegalName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PersonSearchResult(items.Select(ToPersonDto).ToList(), total, page, pageSize);
    }

    public async Task<MemberSearchResult> SearchMembersAsync(MemberSearchInput input, CancellationToken cancellationToken = default)
    {
        var (page, pageSize) = NormalizePage(input.Page, input.PageSize);
        IQueryable<MemberEntity> query = _context.Members.Include(m => m.Person).AsNoTracking();

        if (!string.IsNullOrWhiteSpace(input.Query))
        {
            var term = input.Query.Trim();
            query = query.Where(m =>
                m.MemberNumber.Contains(term) ||
                m.Person.LegalName.Contains(term) ||
                m.Person.PreferredName.Contains(term) ||
                (m.Person.Phone != null && m.Person.Phone.Contains(term)) ||
                (m.Person.Email != null && m.Person.Email.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(input.Status))
        {
            var status = MembershipServiceHelpers.ParseEnum<MemberStatus>(input.Status, "status");
            query = query.Where(m => m.Status == status);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(m => m.MemberNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new MemberSearchResult(items.Select(ToMemberDto).ToList(), total, page, pageSize);
    }

    public async Task<MemberDetailDto> GetMemberAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await MembershipServiceHelpers.ExpireActiveOverdueMembershipsAsync(_context, _clock, id, cancellationToken);

        var member = await _context.Members
            .Include(m => m.Person)
            .Include(m => m.Memberships).ThenInclude(x => x.Category)
            .Include(m => m.Memberships).ThenInclude(x => x.StatusHistory)
            .Include(m => m.Memberships).ThenInclude(x => x.FeeAssessments)
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Member '{id}' was not found.");

        var dependants = await _context.MembershipDependants
            .Include(d => d.DependantPerson)
            .AsNoTracking()
            .Where(d => d.MemberId == id)
            .OrderBy(d => d.DependantPerson.LegalName)
            .ToListAsync(cancellationToken);

        return ToMemberDetailDto(member, dependants);
    }

    public async Task<IReadOnlyList<MembershipDependantDto>> ListDependantsAsync(Guid memberId, CancellationToken cancellationToken = default)
    {
        await EnsureMemberAsync(memberId, cancellationToken);

        var rows = await _context.MembershipDependants
            .Include(d => d.DependantPerson)
            .AsNoTracking()
            .Where(d => d.MemberId == memberId)
            .OrderBy(d => d.DependantPerson.LegalName)
            .ToListAsync(cancellationToken);

        return rows.Select(ToDependantDto).ToList();
    }

    public async Task<MembershipDependantDto> AddDependantAsync(Guid memberId, AddDependantInput input, CancellationToken cancellationToken = default)
    {
        await EnsureMemberAsync(memberId, cancellationToken);

        if (!await _context.Persons.AnyAsync(p => p.Id == input.DependantPersonId, cancellationToken))
        {
            throw new NotFoundException($"Dependant person '{input.DependantPersonId}' was not found.");
        }

        var relationType = MembershipServiceHelpers.ParseEnum<DependantRelationType>(input.RelationType, "relationType");

        if (await _context.MembershipDependants.AnyAsync(
                d => d.MemberId == memberId && d.DependantPersonId == input.DependantPersonId, cancellationToken))
        {
            throw new ConflictException("The person is already a dependant of this member.");
        }

        var row = new MembershipDependantEntity
        {
            MemberId = memberId,
            DependantPersonId = input.DependantPersonId,
            RelationType = relationType,
            Notes = input.Notes ?? string.Empty,
        };

        _context.MembershipDependants.Add(row);
        await _context.SaveChangesAsync(cancellationToken);

        var dependant = await _context.MembershipDependants
            .Include(d => d.DependantPerson)
            .AsNoTracking()
            .SingleAsync(d => d.Id == row.Id, cancellationToken);

        return ToDependantDto(dependant);
    }

    public async Task RemoveDependantAsync(Guid memberId, Guid dependantPersonId, CancellationToken cancellationToken = default)
    {
        var row = await _context.MembershipDependants
            .FirstOrDefaultAsync(d => d.MemberId == memberId && d.DependantPersonId == dependantPersonId, cancellationToken)
            ?? throw new NotFoundException($"Dependant person '{dependantPersonId}' was not found for member '{memberId}'.");

        _context.MembershipDependants.Remove(row);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<HouseholdDto>> ListHouseholdsAsync(CancellationToken cancellationToken = default)
    {
        var households = await _context.Households
            .Include(h => h.PrimaryMember!.Person)
            .Include(h => h.Members).ThenInclude(m => m.Person)
            .Include(h => h.Members).ThenInclude(m => m.Person.Member)
            .AsNoTracking()
            .OrderBy(h => h.Name)
            .ToListAsync(cancellationToken);

        return households.Select(ToHouseholdDto).ToList();
    }

    public async Task<HouseholdDto> GetHouseholdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var household = await _context.Households
            .Include(h => h.PrimaryMember!.Person)
            .Include(h => h.Members).ThenInclude(m => m.Person)
            .Include(h => h.Members).ThenInclude(m => m.Person.Member)
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Household '{id}' was not found.");

        return ToHouseholdDto(household);
    }

    public async Task<HouseholdDto> CreateHouseholdAsync(HouseholdInput input, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
        {
            throw new ValidationException(
                "The household could not be created.",
                MembershipServiceHelpers.FieldError("name", "A household name is required."));
        }

        await EnsureOptionalPrimaryMemberAsync(input.PrimaryMemberId, cancellationToken);

        var household = new HouseholdEntity
        {
            Name = input.Name.Trim(),
            Reference = await GenerateHouseholdReferenceAsync(cancellationToken),
            PrimaryMemberId = input.PrimaryMemberId,
            IsActive = true,
        };

        _context.Households.Add(household);

        if (input.PrimaryMemberId is not null)
        {
            var member = await _context.Members.AsNoTracking().SingleAsync(m => m.Id == input.PrimaryMemberId, cancellationToken);
            _context.HouseholdMembers.Add(new HouseholdMemberEntity
            {
                Household = household,
                PersonId = member.PersonId,
                Role = HouseholdMemberRole.Primary,
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
        return await GetHouseholdAsync(household.Id, cancellationToken);
    }

    public async Task<HouseholdDto> UpdateHouseholdAsync(Guid id, HouseholdInput input, CancellationToken cancellationToken = default)
    {
        var household = await _context.Households.FirstOrDefaultAsync(h => h.Id == id, cancellationToken)
                        ?? throw new NotFoundException($"Household '{id}' was not found.");

        await EnsureOptionalPrimaryMemberAsync(input.PrimaryMemberId, cancellationToken);

        if (string.IsNullOrWhiteSpace(input.Name))
        {
            throw new ValidationException(
                "The household could not be updated.",
                MembershipServiceHelpers.FieldError("name", "A household name is required."));
        }

        household.Name = input.Name.Trim();
        if (household.PrimaryMemberId != input.PrimaryMemberId)
        {
            household.PrimaryMemberId = input.PrimaryMemberId;

            var existingPrimary = _context.HouseholdMembers
                .Where(m => m.HouseholdId == id && m.Role == HouseholdMemberRole.Primary);
            _context.HouseholdMembers.RemoveRange(existingPrimary);

            if (input.PrimaryMemberId is not null)
            {
                var member = await _context.Members.AsNoTracking().SingleAsync(m => m.Id == input.PrimaryMemberId, cancellationToken);
                _context.HouseholdMembers.Add(new HouseholdMemberEntity
                {
                    HouseholdId = id,
                    PersonId = member.PersonId,
                    Role = HouseholdMemberRole.Primary,
                });
            }
        }

        household.UpdatedAtUtc = _clock.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return await GetHouseholdAsync(id, cancellationToken);
    }

    public async Task<HouseholdDto> AddHouseholdMemberAsync(Guid id, AddHouseholdMemberInput input, CancellationToken cancellationToken = default)
    {
        if (!await _context.Households.AnyAsync(h => h.Id == id, cancellationToken))
        {
            throw new NotFoundException($"Household '{id}' was not found.");
        }

        if (!await _context.Persons.AnyAsync(p => p.Id == input.PersonId, cancellationToken))
        {
            throw new NotFoundException($"Person '{input.PersonId}' was not found.");
        }

        var role = MembershipServiceHelpers.ParseEnum<HouseholdMemberRole>(input.Role, "role");

        if (await _context.HouseholdMembers.AnyAsync(m => m.HouseholdId == id && m.PersonId == input.PersonId, cancellationToken))
        {
            throw new ConflictException("The person is already in this household.");
        }

        _context.HouseholdMembers.Add(new HouseholdMemberEntity
        {
            HouseholdId = id,
            PersonId = input.PersonId,
            Role = role,
        });

        await _context.SaveChangesAsync(cancellationToken);
        return await GetHouseholdAsync(id, cancellationToken);
    }

    public async Task RemoveHouseholdMemberAsync(Guid householdId, Guid personId, CancellationToken cancellationToken = default)
    {
        var row = await _context.HouseholdMembers
            .FirstOrDefaultAsync(m => m.HouseholdId == householdId && m.PersonId == personId, cancellationToken)
            ?? throw new NotFoundException($"Person '{personId}' is not a member of household '{householdId}'.");

        if (row.Role == HouseholdMemberRole.Primary)
        {
            throw new ConflictException("The primary household member cannot be removed while it is the primary. Change the primary member first.");
        }

        _context.HouseholdMembers.Remove(row);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureMemberAsync(Guid memberId, CancellationToken cancellationToken)
    {
        if (!await _context.Members.AnyAsync(m => m.Id == memberId, cancellationToken))
        {
            throw new NotFoundException($"Member '{memberId}' was not found.");
        }
    }

    private async Task EnsureOptionalPrimaryMemberAsync(Guid? memberId, CancellationToken cancellationToken)
    {
        if (memberId is not null && !await _context.Members.AnyAsync(m => m.Id == memberId, cancellationToken))
        {
            throw new NotFoundException($"Primary member '{memberId}' was not found.");
        }
    }

    private async Task<string> GenerateHouseholdReferenceAsync(CancellationToken cancellationToken)
    {
        var reference = $"HH-{Guid.NewGuid():N}"[..11].ToUpperInvariant();
        while (await _context.Households.AnyAsync(h => h.Reference == reference, cancellationToken))
        {
            reference = $"HH-{Guid.NewGuid():N}"[..11].ToUpperInvariant();
        }

        return reference;
    }

    private async Task<IReadOnlyList<string>> FindDuplicateWarningsAsync(CreatePersonInput input, CancellationToken cancellationToken)
    {
        IQueryable<PersonEntity> query = _context.Persons.AsNoTracking();
        var hasFilter = false;

        if (!string.IsNullOrWhiteSpace(input.IdNumber))
        {
            query = query.Where(p => p.IdNumber == input.IdNumber.Trim());
            hasFilter = true;
        }
        else if (!string.IsNullOrWhiteSpace(input.Email))
        {
            query = query.Where(p => p.Email == input.Email.Trim());
            hasFilter = true;
        }
        else if (!string.IsNullOrWhiteSpace(input.Phone))
        {
            query = query.Where(p => p.Phone == input.Phone.Trim());
            hasFilter = true;
        }

        if (!hasFilter)
        {
            return Array.Empty<string>();
        }

        var matches = await query.Take(10).Select(p => p.LegalName).ToListAsync(cancellationToken);
        return matches.Count == 0
            ? Array.Empty<string>()
            : new[] { $"One or more existing persons may match this applicant: {string.Join("; ", matches)}." };
    }

    private static (int Page, int PageSize) NormalizePage(int page, int pageSize)
    {
        var normalizedPage = page < 1 ? 1 : page;
        var normalizedSize = pageSize < 1 ? 50 : Math.Min(pageSize, 200);
        return (normalizedPage, normalizedSize);
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static PersonDto ToPersonDto(PersonEntity p) => new(
        p.Id,
        p.LegalName,
        p.PreferredName,
        p.Member?.Id,
        p.Member?.MemberNumber,
        p.IdNumber,
        p.DateOfBirth,
        p.Email,
        p.Phone,
        p.AddressLine1,
        p.AddressLine2,
        p.City,
        p.Region,
        p.PostalCode,
        p.Country,
        p.Notes,
        p.IsActive,
        p.CreatedAtUtc,
        p.UpdatedAtUtc);

    private static MemberDto ToMemberDto(MemberEntity m) => new(
        m.Id,
        m.PersonId,
        m.Person.LegalName,
        m.Person.PreferredName,
        m.MemberNumber,
        MembershipServiceHelpers.ToName(m.Status),
        m.Person.Phone,
        m.Person.Email);

    private static MemberDetailDto ToMemberDetailDto(MemberEntity m, IReadOnlyList<MembershipDependantEntity> dependants) => new(
        m.Id,
        m.PersonId,
        m.Person.LegalName,
        m.Person.PreferredName,
        m.MemberNumber,
        m.Status,
        ToPersonDto(m.Person),
        m.Memberships.OrderByDescending(x => x.CreatedAtUtc).Select(MembershipMappings.ToMembershipDto).ToList(),
        dependants.Select(ToDependantDto).ToList());

    private static MembershipDependantDto ToDependantDto(MembershipDependantEntity d) => new(
        d.Id,
        d.MemberId,
        d.DependantPersonId,
        d.DependantPerson.LegalName,
        MembershipServiceHelpers.ToName(d.RelationType),
        d.Notes);

    private static HouseholdDto ToHouseholdDto(HouseholdEntity h) => new(
        h.Id,
        h.Name,
        h.Reference,
        h.PrimaryMemberId,
        h.PrimaryMember is null ? null : $"{h.PrimaryMember.Person.LegalName}",
        h.PrimaryMember?.MemberNumber,
        h.IsActive,
        h.Members.OrderBy(m => m.Role).Select(ToHouseholdMemberDto).ToList());

    private static HouseholdMemberDto ToHouseholdMemberDto(HouseholdMemberEntity m) => new(
        m.HouseholdId,
        m.PersonId,
        m.Person.LegalName,
        m.Person.Member?.MemberNumber,
        MembershipServiceHelpers.ToName(m.Role));
}

/// <summary>Membership (period) DTO mappings shared with the lifecycle service.</summary>
internal static class MembershipMappings
{
    public static MembershipDto ToMembershipDto(MembershipEntity m) => new(
        m.Id,
        m.MemberId,
        m.CategoryId,
        m.Category.Name,
        m.Status,
        m.StartDateUtc,
        m.EndDateUtc,
        m.RenewalDateUtc,
        m.ApprovedByUserId,
        m.ApprovedAtUtc,
        m.ActivatedAtUtc,
        m.CreatedAtUtc);

    public static MembershipStatusHistoryDto ToStatusHistoryDto(MembershipStatusHistoryEntity h) => new(
        h.Id,
        h.MembershipId,
        MembershipServiceHelpers.ToName(h.PreviousState),
        MembershipServiceHelpers.ToName(h.NewState),
        h.ChangedByUserId,
        h.Reason,
        h.ChangedAtUtc);
}