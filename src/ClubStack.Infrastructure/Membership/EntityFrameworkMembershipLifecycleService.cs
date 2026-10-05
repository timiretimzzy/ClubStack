using ClubStack.Application.Exceptions;
using ClubStack.Application.Membership;
using ClubStack.Application.References;
using ClubStack.Domain.Common;
using ClubStack.Domain.Membership;
using ClubStack.Infrastructure.Persistence;
using ClubStack.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClubStack.Infrastructure.Membership;

/// <inheritdoc />
public sealed class EntityFrameworkMembershipLifecycleService : IMembershipLifecycleService
{
    private const string MemberNumberPrefix = "MEM";

    private readonly ClubStackDbContext _context;
    private readonly IReferenceGenerator _referenceGenerator;
    private readonly ISystemClock _clock;

    public EntityFrameworkMembershipLifecycleService(
        ClubStackDbContext context,
        IReferenceGenerator referenceGenerator,
        ISystemClock clock)
    {
        _context = context;
        _referenceGenerator = referenceGenerator;
        _clock = clock;
    }

    public async Task<MembershipApplicationDto> CreateApplicationAsync(CreateMembershipApplicationInput input, CancellationToken cancellationToken = default)
    {
        var person = await _context.Persons.AsNoTracking().FirstOrDefaultAsync(p => p.Id == input.PersonId, cancellationToken)
                     ?? throw new NotFoundException($"Person '{input.PersonId}' was not found.");

        if (!await _context.MembershipCategories.AnyAsync(c => c.Id == input.CategoryId, cancellationToken))
        {
            throw new NotFoundException($"Membership category '{input.CategoryId}' was not found.");
        }

        var hasActiveOrSuspendedMember = await _context.Members
            .AnyAsync(m => m.PersonId == input.PersonId
                           && (m.Status == MemberStatus.Active || m.Status == MemberStatus.Suspended), cancellationToken);
        if (hasActiveOrSuspendedMember)
        {
            throw new ConflictException("The person already has an active or suspended member record.");
        }

        var application = new MembershipApplicationEntity
        {
            PersonId = input.PersonId,
            CategoryId = input.CategoryId,
            SponsorName = input.SponsorName,
            ReferrerName = input.ReferrerName,
            Notes = input.Notes ?? string.Empty,
            RequiredDocuments = input.RequiredDocuments ?? string.Empty,
        };

        _context.MembershipApplications.Add(application);
        await _context.SaveChangesAsync(cancellationToken);

        return await GetApplicationAsync(application.Id, cancellationToken);
    }

    public async Task<MembershipApplicationDto> GetApplicationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var application = await _context.MembershipApplications
            .Include(a => a.Person)
            .Include(a => a.Category)
            .Include(a => a.Member)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Membership application '{id}' was not found.");

        return MembershipLifecycleMappings.ToApplicationDto(application);
    }

    public async Task<MembershipApplicationSearchResult> SearchApplicationsAsync(MembershipApplicationSearchInput input, CancellationToken cancellationToken = default)
    {
        var (page, pageSize) = NormalizePage(input.Page, input.PageSize);
        IQueryable<MembershipApplicationEntity> query = _context.MembershipApplications
            .Include(a => a.Person)
            .Include(a => a.Category)
            .Include(a => a.Member)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(input.Status))
        {
            var status = MembershipServiceHelpers.ParseEnum<MembershipApplicationStatus>(input.Status, "status");
            query = query.Where(a => a.Status == status);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(a => a.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new MembershipApplicationSearchResult(
            items.Select(MembershipLifecycleMappings.ToApplicationDto).ToList(),
            total,
            page,
            pageSize);
    }

    public async Task<MembershipApplicationDto> SubmitApplicationAsync(Guid id, string actorUserId, CancellationToken cancellationToken = default)
    {
        var application = await LoadApplicationAsync(id, cancellationToken);
        MembershipServiceHelpers.EnsureApplicationTransition(
            application.Status, MembershipApplicationStatus.Submitted,
            $"An application in state '{application.Status}' cannot be submitted.");

        application.Status = MembershipApplicationStatus.Submitted;
        application.SubmittedAtUtc = _clock.UtcNow;
        application.UpdatedAtUtc = _clock.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return await GetApplicationAsync(id, cancellationToken);
    }

    public async Task<MembershipApplicationDto> StartReviewApplicationAsync(Guid id, string actorUserId, CancellationToken cancellationToken = default)
    {
        var application = await LoadApplicationAsync(id, cancellationToken);
        MembershipServiceHelpers.EnsureApplicationTransition(
            application.Status, MembershipApplicationStatus.UnderReview,
            $"An application in state '{application.Status}' cannot begin review.");

        application.Status = MembershipApplicationStatus.UnderReview;
        application.ReviewedAtUtc = _clock.UtcNow;
        application.UpdatedAtUtc = _clock.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return await GetApplicationAsync(id, cancellationToken);
    }

    public async Task<MembershipApplicationDto> ApproveApplicationAsync(Guid id, string actorUserId, CancellationToken cancellationToken = default)
    {
        var application = await LoadApplicationAsync(id, cancellationToken);
        MembershipServiceHelpers.EnsureApplicationTransition(
            application.Status, MembershipApplicationStatus.Approved,
            $"An application in state '{application.Status}' cannot be approved.");

        application.Status = MembershipApplicationStatus.Approved;
        application.ApproverUserId = actorUserId;
        application.ApprovedAtUtc = _clock.UtcNow;
        application.UpdatedAtUtc = _clock.UtcNow;

        var member = new MemberEntity
        {
            PersonId = application.PersonId,
            MemberNumber = await _referenceGenerator.GenerateAsync(MemberNumberPrefix, cancellationToken: cancellationToken),
            Status = MemberStatus.Prospect,
        };

        _context.Members.Add(member);

        var now = _clock.UtcNow;
        var membership = new MembershipEntity
        {
            MemberId = member.Id,
            CategoryId = application.CategoryId,
            Status = MembershipStatus.Pending,
            RenewalDateUtc = now,
            ApprovedByUserId = actorUserId,
            ApprovedAtUtc = now,
        };

        _context.Memberships.Add(membership);
        application.MemberId = member.Id;

        await _context.SaveChangesAsync(cancellationToken);
        return await GetApplicationAsync(id, cancellationToken);
    }

    public async Task<MembershipApplicationDto> RejectApplicationAsync(Guid id, RejectMembershipApplicationInput input, string actorUserId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            throw new ValidationException(
                "The application could not be rejected.",
                MembershipServiceHelpers.FieldError("reason", "A reason is required."));
        }

        var application = await LoadApplicationAsync(id, cancellationToken);

        var rejectFrom = application.Status;
        if (rejectFrom != MembershipApplicationStatus.Submitted && rejectFrom != MembershipApplicationStatus.UnderReview)
        {
            throw new ConflictException($"An application in state '{rejectFrom}' cannot be rejected.");
        }

        application.Status = MembershipApplicationStatus.Rejected;
        application.RejectionReason = input.Reason.Trim();
        application.UpdatedAtUtc = _clock.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return await GetApplicationAsync(id, cancellationToken);
    }

    public async Task<IReadOnlyList<MembershipDto>> GetMembershipsForMemberAsync(Guid memberId, CancellationToken cancellationToken = default)
    {
        await EnsureMemberAsync(memberId, cancellationToken);
        await MembershipServiceHelpers.ExpireActiveOverdueMembershipsAsync(_context, _clock, memberId, cancellationToken);

        var memberships = await _context.Memberships
            .Include(m => m.Category)
            .AsNoTracking()
            .Where(m => m.MemberId == memberId)
            .OrderByDescending(m => m.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return memberships.Select(MembershipMappings.ToMembershipDto).ToList();
    }

    public async Task<MembershipDto> GetMembershipAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var membership = await LoadMembershipAsync(id, cancellationToken);
        await MembershipServiceHelpers.ExpireActiveOverdueMembershipsAsync(_context, _clock, membership.MemberId, cancellationToken);

        var refreshed = await LoadMembershipAsync(id, cancellationToken);
        return MembershipMappings.ToMembershipDto(refreshed);
    }

    public async Task<IReadOnlyList<MembershipStatusHistoryDto>> GetMembershipHistoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await LoadMembershipAsync(id, cancellationToken);

        var history = await _context.MembershipStatusHistory.AsNoTracking()
            .Where(h => h.MembershipId == id)
            .OrderBy(h => h.ChangedAtUtc)
            .ToListAsync(cancellationToken);

        return history.Select(MembershipMappings.ToStatusHistoryDto).ToList();
    }

    public async Task<MembershipDto> ActivateMembershipAsync(Guid id, string actorUserId, CancellationToken cancellationToken = default)
    {
        var membership = await LoadMembershipAsync(id, cancellationToken);
        MembershipServiceHelpers.EnsureMembershipTransition(
            membership.Status, MembershipStatus.Active,
            $"A membership in state '{membership.Status}' cannot be activated.");

        var category = await _context.MembershipCategories.SingleAsync(c => c.Id == membership.CategoryId, cancellationToken);

        var now = _clock.UtcNow;
        var previous = membership.Status;
        membership.Status = MembershipStatus.Active;
        membership.StartDateUtc = now;
        membership.RenewalDateUtc = now.AddMonths(category.RenewalPeriodMonths);
        membership.EndDateUtc = membership.RenewalDateUtc;
        membership.ActivatedAtUtc = now;
        membership.UpdatedAtUtc = now;
        membership.ApprovedByUserId ??= actorUserId;
        membership.ApprovedAtUtc ??= now;

        MembershipServiceHelpers.RecordMembershipTransition(
            _context, membership, previous, MembershipStatus.Active, actorUserId,
            $"Membership activated. Period ends {membership.RenewalDateUtc:yyyy-MM-dd}.");

        if (membership.Member.Status == MemberStatus.Prospect)
        {
            membership.Member.Status = MemberStatus.Active;
            membership.Member.UpdatedAtUtc = now;
        }

        var application = await _context.MembershipApplications
            .FirstOrDefaultAsync(a => a.MemberId == membership.MemberId
                                      && a.Status == MembershipApplicationStatus.Approved, cancellationToken);
        if (application is not null)
        {
            application.Status = MembershipApplicationStatus.Activated;
            application.UpdatedAtUtc = now;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return await GetMembershipAsync(id, cancellationToken);
    }

    public async Task<MembershipDto> SuspendMembershipAsync(Guid id, string reason, string actorUserId, CancellationToken cancellationToken = default)
    {
        var membership = await LoadMembershipAsync(id, cancellationToken);
        MembershipServiceHelpers.EnsureMembershipTransition(
            membership.Status, MembershipStatus.Suspended,
            $"A membership in state '{membership.Status}' cannot be suspended.");

        var now = _clock.UtcNow;
        var previous = membership.Status;
        membership.Status = MembershipStatus.Suspended;
        membership.UpdatedAtUtc = now;

        MembershipServiceHelpers.RecordMembershipTransition(
            _context, membership, previous, MembershipStatus.Suspended, actorUserId,
            string.IsNullOrWhiteSpace(reason) ? "Membership suspended." : reason.Trim());

        if (membership.Member.Status == MemberStatus.Active)
        {
            membership.Member.Status = MemberStatus.Suspended;
            membership.Member.UpdatedAtUtc = now;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return await GetMembershipAsync(id, cancellationToken);
    }

    public async Task<MembershipDto> ReinstateMembershipAsync(Guid id, string reason, string actorUserId, CancellationToken cancellationToken = default)
    {
        var membership = await LoadMembershipAsync(id, cancellationToken);
        MembershipServiceHelpers.EnsureMembershipTransition(
            membership.Status, MembershipStatus.Active,
            $"A membership in state '{membership.Status}' cannot be reinstated.");

        var now = _clock.UtcNow;
        var previous = membership.Status;
        membership.Status = MembershipStatus.Active;
        membership.UpdatedAtUtc = now;

        MembershipServiceHelpers.RecordMembershipTransition(
            _context, membership, previous, MembershipStatus.Active, actorUserId,
            string.IsNullOrWhiteSpace(reason) ? "Membership reinstated." : reason.Trim());

        if (membership.Member.Status == MemberStatus.Suspended)
        {
            membership.Member.Status = MemberStatus.Active;
            membership.Member.UpdatedAtUtc = now;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return await GetMembershipAsync(id, cancellationToken);
    }

    public async Task<MembershipDto> RenewMembershipAsync(Guid id, string actorUserId, CancellationToken cancellationToken = default)
    {
        var membership = await LoadMembershipAsync(id, cancellationToken);
        MembershipServiceHelpers.EnsureMembershipTransition(
            membership.Status, MembershipStatus.Active,
            $"A membership in state '{membership.Status}' cannot be renewed.");

        if (membership.Member.Status == MemberStatus.Cancelled)
        {
            throw new ConflictException("A membership of a cancelled member cannot be renewed.");
        }

        var category = await _context.MembershipCategories.SingleAsync(c => c.Id == membership.CategoryId, cancellationToken);

        var now = _clock.UtcNow;
        var previous = membership.Status;
        membership.Status = MembershipStatus.Active;
        membership.RenewalDateUtc = now.AddMonths(category.RenewalPeriodMonths);
        membership.EndDateUtc = membership.RenewalDateUtc;
        membership.UpdatedAtUtc = now;

        MembershipServiceHelpers.RecordMembershipTransition(
            _context, membership, previous, MembershipStatus.Active, actorUserId,
            $"Membership renewed for {category.RenewalPeriodMonths} months. Period ends {membership.RenewalDateUtc:yyyy-MM-dd}.");

        await _context.SaveChangesAsync(cancellationToken);
        return await GetMembershipAsync(id, cancellationToken);
    }

    public async Task<MembershipDto> CancelMembershipAsync(Guid id, string reason, string actorUserId, CancellationToken cancellationToken = default)
    {
        var membership = await LoadMembershipAsync(id, cancellationToken);
        MembershipServiceHelpers.EnsureMembershipTransition(
            membership.Status, MembershipStatus.Cancelled,
            $"A membership in state '{membership.Status}' cannot be cancelled.");

        var now = _clock.UtcNow;
        var previous = membership.Status;
        membership.Status = MembershipStatus.Cancelled;
        membership.UpdatedAtUtc = now;

        MembershipServiceHelpers.RecordMembershipTransition(
            _context, membership, previous, MembershipStatus.Cancelled, actorUserId,
            string.IsNullOrWhiteSpace(reason) ? "Membership cancelled." : reason.Trim());

        if (membership.Member.Status != MemberStatus.Cancelled)
        {
            membership.Member.Status = MemberStatus.Cancelled;
            membership.Member.UpdatedAtUtc = now;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return await GetMembershipAsync(id, cancellationToken);
    }

    public async Task<MembershipDto> TransferMembershipAsync(Guid id, Guid targetCategoryId, string actorUserId, CancellationToken cancellationToken = default)
    {
        var source = await LoadMembershipAsync(id, cancellationToken);
        if (source.Status != MembershipStatus.Pending && source.Status != MembershipStatus.Active)
        {
            throw new ConflictException($"A membership in state '{source.Status}' cannot be transferred.");
        }

        var targetCategory = await _context.MembershipCategories.AsNoTracking()
                                 .FirstOrDefaultAsync(c => c.Id == targetCategoryId, cancellationToken)
                             ?? throw new NotFoundException($"Membership category '{targetCategoryId}' was not found.");

        var now = _clock.UtcNow;
        var previous = source.Status;
        source.Status = MembershipStatus.Transferred;
        source.UpdatedAtUtc = now;

        MembershipServiceHelpers.RecordMembershipTransition(
            _context, source, previous, MembershipStatus.Transferred, actorUserId,
            $"Membership transferred to category '{targetCategory.Name}'.");

        var successor = new MembershipEntity
        {
            MemberId = source.MemberId,
            CategoryId = targetCategoryId,
            Status = MembershipStatus.Pending,
            RenewalDateUtc = now,
            PreviousMembershipId = source.Id,
        };

        _context.Memberships.Add(successor);
        await _context.SaveChangesAsync(cancellationToken);

        return await GetMembershipAsync(id, cancellationToken);
    }

    private async Task<MembershipEntity> LoadMembershipAsync(Guid id, CancellationToken cancellationToken)
        => await _context.Memberships
            .Include(m => m.Category)
            .Include(m => m.Member)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Membership '{id}' was not found.");

    private async Task<MembershipApplicationEntity> LoadApplicationAsync(Guid id, CancellationToken cancellationToken)
        => await _context.MembershipApplications
            .Include(a => a.Person)
            .Include(a => a.Category)
            .Include(a => a.Member)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Membership application '{id}' was not found.");

    private async Task EnsureMemberAsync(Guid memberId, CancellationToken cancellationToken)
    {
        if (!await _context.Members.AnyAsync(m => m.Id == memberId, cancellationToken))
        {
            throw new NotFoundException($"Member '{memberId}' was not found.");
        }
    }

    private static (int Page, int PageSize) NormalizePage(int page, int pageSize)
    {
        var normalizedPage = page < 1 ? 1 : page;
        var normalizedSize = pageSize < 1 ? 50 : Math.Min(pageSize, 200);
        return (normalizedPage, normalizedSize);
    }
}

/// <summary>Application and membership DTO mappings.</summary>
internal static class MembershipLifecycleMappings
{
    public static MembershipApplicationDto ToApplicationDto(MembershipApplicationEntity a) => new(
        a.Id,
        a.PersonId,
        a.Person.LegalName,
        a.CategoryId,
        a.Category.Name,
        MembershipServiceHelpers.ToName(a.Status),
        a.ApproverUserId,
        a.SponsorName,
        a.ReferrerName,
        a.Notes,
        a.RequiredDocuments,
        a.SubmittedAtUtc,
        a.ReviewedAtUtc,
        a.ApprovedAtUtc,
        a.RejectionReason,
        a.MemberId,
        a.Member?.MemberNumber,
        a.CreatedAtUtc,
        a.UpdatedAtUtc);
}