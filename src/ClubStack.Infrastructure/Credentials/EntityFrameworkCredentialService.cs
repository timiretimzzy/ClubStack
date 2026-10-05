using ClubStack.Application.Credentials;
using ClubStack.Application.Exceptions;
using ClubStack.Application.References;
using ClubStack.Domain.Common;
using ClubStack.Domain.Credentials;
using ClubStack.Infrastructure.Persistence;
using ClubStack.Infrastructure.Persistence.Entities;
using ClubStack.Infrastructure.Membership;
using Microsoft.EntityFrameworkCore;

namespace ClubStack.Infrastructure.Credentials;

/// <inheritdoc />
public sealed class EntityFrameworkCredentialService : ICredentialService
{
    private const string CredentialNumberPrefix = "CRD";
    private const string SystemActor = "system";

    private readonly ClubStackDbContext _context;
    private readonly IReferenceGenerator _referenceGenerator;
    private readonly ISystemClock _clock;

    public EntityFrameworkCredentialService(ClubStackDbContext context, IReferenceGenerator referenceGenerator, ISystemClock clock)
    {
        _context = context;
        _referenceGenerator = referenceGenerator;
        _clock = clock;
    }

    public async Task<CredentialSearchResult> SearchAsync(CredentialSearchInput input, CancellationToken cancellationToken = default)
    {
        var (page, pageSize) = NormalizePage(input.Page, input.PageSize);
        IQueryable<CredentialEntity> query = _context.Credentials
            .Include(c => c.Member)
                .ThenInclude(m => m!.Person)
            .Include(c => c.ReplacedByCredential);

        if (input.MemberId is not null)
        {
            query = query.Where(c => c.MemberId == input.MemberId);
        }

        if (!string.IsNullOrWhiteSpace(input.Status))
        {
            var status = MembershipServiceHelpers.ParseEnum<CredentialStatus>(input.Status, "status");
            query = query.Where(c => c.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(input.Query))
        {
            var q = input.Query.Trim();
            query = query.Where(c => c.CredentialNumber.Contains(q, StringComparison.OrdinalIgnoreCase)
                                     || c.CredentialValue.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        var total = await query.CountAsync(cancellationToken);

        // Expiry is applied lazily on reads so an expired card is never returned as active.
        var now = _clock.UtcNow;
        var items = await query
            .OrderByDescending(c => c.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var changed = false;
        foreach (var credential in items)
        {
            changed |= ApplyExpiry(credential, now);
        }

        if (changed)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return new CredentialSearchResult(
            items.Select(CredentialMappings.ToDto).ToList(),
            total,
            page,
            pageSize);
    }

    public async Task<CredentialDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var credential = await LoadCredentialAsync(id, tracked: true, cancellationToken);
        if (ApplyExpiry(credential, _clock.UtcNow))
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return CredentialMappings.ToDto(credential);
    }

    public async Task<IReadOnlyList<CredentialHistoryDto>> GetHistoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await _context.Credentials.AnyAsync(c => c.Id == id, cancellationToken))
        {
            throw new NotFoundException($"Credential '{id}' was not found.");
        }

        return await _context.CredentialHistory
            .Where(h => h.CredentialId == id)
            .OrderBy(h => h.ChangedAtUtc)
            .Select(h => new CredentialHistoryDto(h.Id, h.CredentialId, h.PreviousState.ToString(), h.NewState.ToString(), h.ChangedByUserId, h.Reason, h.ChangedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<CredentialDto> IssueAsync(IssueCredentialInput input, string actorUserId, CancellationToken cancellationToken = default)
    {
        var credentialType = MembershipServiceHelpers.ParseEnum<CredentialType>(input.CredentialType, "credentialType");
        if (credentialType != CredentialType.MagneticStripe)
        {
            throw new ValidationException(
                "The credential could not be issued.",
                MembershipServiceHelpers.FieldError("credentialType", "Only magnetic-stripe credentials can be issued in this release (CRED-002)."));
        }

        var credentialValue = CardIdentifier.Normalize(input.CredentialValue);
        if (!CardIdentifier.IsValidFormat(credentialValue))
        {
            throw new ValidationException(
                "The credential could not be issued.",
                MembershipServiceHelpers.FieldError("credentialValue", "The card identifier must be 6-64 characters of letters, digits, '-' or '_'."));
        }

        var duplicate = await _context.Credentials
            .Where(c => c.CredentialValue == credentialValue)
            .FirstOrDefaultAsync(cancellationToken);
        if (duplicate is not null)
        {
            var state = duplicate.Status.ToString();
            throw new ConflictException($"A credential with this identifier already exists (state '{state}').");
        }

        var now = _clock.UtcNow;
        var credential = new CredentialEntity
        {
            CredentialNumber = await _referenceGenerator.GenerateAsync(CredentialNumberPrefix, cancellationToken: cancellationToken),
            CredentialType = credentialType,
            CredentialValue = credentialValue,
            Status = CredentialStatus.Issued,
            ExpiresAtUtc = input.ExpiresAtUtc,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        _context.Credentials.Add(credential);
        RecordTransition(credential, CredentialStatus.Issued, CredentialStatus.Issued, actorUserId, "Credential issued.");
        await _context.SaveChangesAsync(cancellationToken);
        return CredentialMappings.ToDto(credential);
    }

    public async Task<CredentialDto> ActivateAsync(Guid id, ActivateCredentialInput input, string actorUserId, CancellationToken cancellationToken = default)
    {
        var member = await _context.Members.AsNoTracking().FirstOrDefaultAsync(m => m.Id == input.MemberId, cancellationToken)
                     ?? throw new NotFoundException($"Member '{input.MemberId}' was not found.");

        var credential = await LoadCredentialAsync(id, tracked: true, cancellationToken);
        EnsureTransition(credential, CredentialStatus.Active, $"A credential in state '{credential.Status}' cannot be activated.");

        var now = _clock.UtcNow;
        var previous = credential.Status;
        credential.MemberId = member.Id;
        credential.Status = CredentialStatus.Active;
        credential.ActivatedAtUtc = now;
        credential.UpdatedAtUtc = now;
        RecordTransition(credential, previous, credential.Status, actorUserId, "Credential activated and assigned to the member.");
        await _context.SaveChangesAsync(cancellationToken);

        return CredentialMappings.ToDto(credential);
    }

    public async Task<CredentialDto> SuspendAsync(Guid id, SuspendCredentialInput input, string actorUserId, CancellationToken cancellationToken = default)
    {
        var credential = await LoadCredentialAsync(id, tracked: true, cancellationToken);
        EnsureTransition(credential, CredentialStatus.Suspended, $"A credential in state '{credential.Status}' cannot be suspended.");

        var now = _clock.UtcNow;
        var previous = credential.Status;
        credential.Status = CredentialStatus.Suspended;
        credential.SuspendedAtUtc = now;
        credential.UpdatedAtUtc = now;
        RecordTransition(credential, previous, credential.Status, actorUserId, RequiredReason(input.Reason));
        await _context.SaveChangesAsync(cancellationToken);

        return CredentialMappings.ToDto(credential);
    }

    public async Task<CredentialDto> MarkLostAsync(Guid id, LostCredentialInput input, string actorUserId, CancellationToken cancellationToken = default)
    {
        var credential = await LoadCredentialAsync(id, tracked: true, cancellationToken);
        EnsureTransition(credential, CredentialStatus.Lost, $"A credential in state '{credential.Status}' cannot be marked lost.");

        var now = _clock.UtcNow;
        var previous = credential.Status;
        credential.Status = CredentialStatus.Lost;
        credential.UpdatedAtUtc = now;
        RecordTransition(credential, previous, credential.Status, actorUserId, RequiredReason(input.Reason));
        await _context.SaveChangesAsync(cancellationToken);

        return CredentialMappings.ToDto(credential);
    }

    public async Task<CredentialDto> MarkStolenAsync(Guid id, StolenCredentialInput input, string actorUserId, CancellationToken cancellationToken = default)
    {
        var credential = await LoadCredentialAsync(id, tracked: true, cancellationToken);
        EnsureTransition(credential, CredentialStatus.Stolen, $"A credential in state '{credential.Status}' cannot be marked stolen.");

        var now = _clock.UtcNow;
        var previous = credential.Status;
        credential.Status = CredentialStatus.Stolen;
        credential.UpdatedAtUtc = now;
        RecordTransition(credential, previous, credential.Status, actorUserId, RequiredReason(input.Reason));
        await _context.SaveChangesAsync(cancellationToken);

        return CredentialMappings.ToDto(credential);
    }

    public async Task<CredentialDto> ReactivateAsync(Guid id, ReactivateCredentialInput input, string actorUserId, CancellationToken cancellationToken = default)
    {
        var credential = await LoadCredentialAsync(id, tracked: true, cancellationToken);
        EnsureTransition(credential, CredentialStatus.Active, $"A credential in state '{credential.Status}' cannot be reactivated.");

        var now = _clock.UtcNow;
        var previous = credential.Status;
        credential.Status = CredentialStatus.Active;
        credential.SuspendedAtUtc = null;
        credential.UpdatedAtUtc = now;
        RecordTransition(credential, previous, credential.Status, actorUserId, RequiredReason(input.Reason));
        await _context.SaveChangesAsync(cancellationToken);

        return CredentialMappings.ToDto(credential);
    }

    public async Task<CredentialDto> ReplaceAsync(Guid id, ReplaceCredentialInput input, string actorUserId, CancellationToken cancellationToken = default)
    {
        var oldCredential = await LoadCredentialAsync(id, tracked: true, cancellationToken);
        if (oldCredential.Status != CredentialStatus.Lost && oldCredential.Status != CredentialStatus.Stolen)
        {
            throw new ConflictException(
                $"A credential in state '{oldCredential.Status}' cannot be replaced; only lost or stolen credentials are replaced.");
        }

        if (oldCredential.MemberId is null)
        {
            throw new ConflictException("The credential is not assigned to a member and cannot be replaced.");
        }

        var credentialType = MembershipServiceHelpers.ParseEnum<CredentialType>(input.CredentialType, "credentialType");
        if (credentialType != CredentialType.MagneticStripe)
        {
            throw new ValidationException(
                "The credential could not be replaced.",
                MembershipServiceHelpers.FieldError("credentialType", "Only magnetic-stripe credentials can be issued in this release (CRED-002)."));
        }

        var credentialValue = CardIdentifier.Normalize(input.CredentialValue);
        if (!CardIdentifier.IsValidFormat(credentialValue))
        {
            throw new ValidationException(
                "The credential could not be replaced.",
                MembershipServiceHelpers.FieldError("credentialValue", "The card identifier must be 6-64 characters of letters, digits, '-' or '_'."));
        }

        var duplicate = await _context.Credentials
            .Where(c => c.CredentialValue == credentialValue)
            .FirstOrDefaultAsync(cancellationToken);
        if (duplicate is not null)
        {
            throw new ConflictException("A credential with this identifier already exists.");
        }

        var reason = RequiredReason(input.Reason);
        var now = _clock.UtcNow;
        var replacement = new CredentialEntity
        {
            CredentialNumber = await _referenceGenerator.GenerateAsync(CredentialNumberPrefix, cancellationToken: cancellationToken),
            CredentialType = credentialType,
            CredentialValue = credentialValue,
            Status = CredentialStatus.Active,
            MemberId = oldCredential.MemberId,
            ActivatedAtUtc = now,
            ExpiresAtUtc = input.ExpiresAtUtc,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        _context.Credentials.Add(replacement);
        RecordTransition(
            replacement,
            CredentialStatus.Issued,
            CredentialStatus.Active,
            actorUserId,
            $"Replacement for credential '{oldCredential.CredentialNumber}'.");

        var previous = oldCredential.Status;
        oldCredential.Status = CredentialStatus.Replaced;
        oldCredential.ReplacedByCredentialId = replacement.Id;
        oldCredential.UpdatedAtUtc = now;
        RecordTransition(oldCredential, previous, oldCredential.Status, actorUserId, reason);

        await _context.SaveChangesAsync(cancellationToken);
        return CredentialMappings.ToDto(replacement);
    }

    public async Task<ResolveCredentialResult> ResolveAsync(string credentialValue, CancellationToken cancellationToken = default)
    {
        var normalized = CardIdentifier.Normalize(credentialValue);
        if (!CardIdentifier.IsValidFormat(normalized))
        {
            throw new NotFoundException("Card not recognized.");
        }

        var credential = await _context.Credentials
            .Include(c => c.Member)
                .ThenInclude(m => m!.Person)
            .FirstOrDefaultAsync(c => c.CredentialValue == normalized, cancellationToken)
            ?? throw new NotFoundException("Card not recognized.");

        if (ApplyExpiry(credential, _clock.UtcNow))
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        if (credential.Status != CredentialStatus.Active)
        {
            throw new ConflictException("This card is not active.");
        }

        var member = credential.Member
                     ?? throw new ConflictException("This card is not assigned to a member.");

        return new ResolveCredentialResult(
            credential.Id,
            credential.CredentialNumber,
            credential.Status.ToString(),
            member.Id,
            member.MemberNumber,
            member.Person?.PreferredName ?? member.Person?.LegalName ?? string.Empty,
            member.Status.ToString());
    }

    private async Task<CredentialEntity> LoadCredentialAsync(Guid id, bool tracked, CancellationToken cancellationToken)
    {
        var query = tracked
            ? _context.Credentials
                .Include(c => c.Member).ThenInclude(m => m!.Person)
                .Include(c => c.ReplacedByCredential)
            : _context.Credentials
                .AsNoTracking()
                .Include(c => c.Member).ThenInclude(m => m!.Person)
                .Include(c => c.ReplacedByCredential);

        return await query.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
               ?? throw new NotFoundException($"Credential '{id}' was not found.");
    }

    private static void EnsureTransition(CredentialEntity credential, CredentialStatus next, string conflictMessage)
    {
        if (!CredentialStateTransitions.CanTransition(credential.Status, next))
        {
            throw new ConflictException(conflictMessage);
        }
    }

    private bool ApplyExpiry(CredentialEntity credential, DateTimeOffset now)
    {
        if (!CredentialStateTransitions.IsExpiredDue(credential.Status, credential.ExpiresAtUtc, now))
        {
            return false;
        }

        var previous = credential.Status;
        credential.Status = CredentialStatus.Expired;
        credential.UpdatedAtUtc = now;
        RecordTransition(credential, previous, CredentialStatus.Expired, SystemActor, "Credential expiry date passed.");
        return true;
    }

    private void RecordTransition(
        CredentialEntity credential,
        CredentialStatus previous,
        CredentialStatus next,
        string changedByUserId,
        string reason)
    {
        var history = new CredentialHistoryEntity
        {
            CredentialId = credential.Id,
            PreviousState = previous,
            NewState = next,
            ChangedByUserId = changedByUserId,
            Reason = reason,
            ChangedAtUtc = DateTimeOffset.UtcNow,
        };

        credential.History.Add(history);
        // Adding to the navigation collection alone leaves the entity to be discovered
        // by DetectChanges, where a non-default Guid key (Guid.CreateVersion7) is treated
        // as an existing (Modified) row instead of a new one. Track it explicitly as Added.
        _context.CredentialHistory.Add(history);
    }

    private static string RequiredReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ValidationException(
                "The operation could not be completed.",
                MembershipServiceHelpers.FieldError("reason", "A reason is required."));
        }

        return reason.Trim();
    }

    private static (int Page, int PageSize) NormalizePage(int page, int pageSize)
    {
        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 250);
        return (safePage, safePageSize);
    }
}

internal static class CredentialMappings
{
    public static CredentialDto ToDto(CredentialEntity c) => new(
        c.Id,
        c.CredentialNumber,
        c.MemberId,
        c.Member?.MemberNumber,
        c.Member?.Person is { } person ? (person.PreferredName.Length > 0 ? person.PreferredName : person.LegalName) : null,
        c.CredentialType.ToString(),
        c.CredentialValue,
        c.Status.ToString(),
        c.ActivatedAtUtc,
        c.SuspendedAtUtc,
        c.ExpiresAtUtc,
        c.ReplacedByCredentialId,
        c.ReplacedByCredential?.CredentialNumber,
        c.CreatedAtUtc,
        c.UpdatedAtUtc);
}