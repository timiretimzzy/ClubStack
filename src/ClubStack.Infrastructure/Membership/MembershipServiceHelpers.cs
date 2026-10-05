using ClubStack.Application.Exceptions;
using ClubStack.Domain.Common;
using ClubStack.Domain.Membership;
using ClubStack.Infrastructure.Persistence;
using ClubStack.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClubStack.Infrastructure.Membership;

/// <summary>
/// Shared helpers for the membership services: state-transition guards that reject
/// invalid transitions, lazy expiry of overdue active memberships, and safe enum
/// conversion with field-level validation errors.
/// </summary>
internal static class MembershipServiceHelpers
{
    public static void EnsureApplicationTransition(MembershipApplicationStatus from, MembershipApplicationStatus to, string message)
    {
        if (!MembershipStateTransitions.CanTransition(from, to))
        {
            throw new ConflictException(message);
        }
    }

    public static void EnsureMemberTransition(MemberStatus from, MemberStatus to, string message)
    {
        if (!MembershipStateTransitions.CanTransition(from, to))
        {
            throw new ConflictException(message);
        }
    }

    public static void EnsureMembershipTransition(MembershipStatus from, MembershipStatus to, string message)
    {
        if (!MembershipStateTransitions.CanTransition(from, to))
        {
            throw new ConflictException(message);
        }
    }

    /// <summary>
    /// Applies the expiry rule (Phase 2 MEM-028) lazily: any active membership of the
    /// member whose period end date has passed transitions to <see cref="MembershipStatus.Expired"/>
    /// with a recorded history entry. The member registry status is untouched (Phase 8 §7.1
    /// does not define an expired member state; 'active member' is derived from an active period).
    /// </summary>
    public static async Task ExpireActiveOverdueMembershipsAsync(
        ClubStackDbContext context,
        ISystemClock clock,
        Guid memberId,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var due = await context.Memberships
            .Where(m => m.MemberId == memberId
                        && m.Status == MembershipStatus.Active
                        && m.EndDateUtc != null
                        && m.EndDateUtc <= now)
            .ToListAsync(cancellationToken);

        if (due.Count == 0)
        {
            return;
        }

        foreach (var membership in due)
        {
            membership.Status = MembershipStatus.Expired;
            membership.UpdatedAtUtc = now;
            context.MembershipStatusHistory.Add(new MembershipStatusHistoryEntity
            {
                MembershipId = membership.Id,
                PreviousState = MembershipStatus.Active,
                NewState = MembershipStatus.Expired,
                ChangedByUserId = "system",
                Reason = "Membership period expired.",
                ChangedAtUtc = now,
            });
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public static void RecordMembershipTransition(
        ClubStackDbContext context,
        MembershipEntity membership,
        MembershipStatus previous,
        MembershipStatus next,
        string changedByUserId,
        string reason)
    {
        context.MembershipStatusHistory.Add(new MembershipStatusHistoryEntity
        {
            MembershipId = membership.Id,
            PreviousState = previous,
            NewState = next,
            ChangedByUserId = changedByUserId,
            Reason = reason,
            ChangedAtUtc = DateTimeOffset.UtcNow,
        });
    }

    /// <summary>Converts an enum to its display name (lowercase-first camel for API payloads).</summary>
    public static string ToName<TEnum>(TEnum value)
        where TEnum : struct, Enum
        => value.ToString();

    /// <summary>
    /// Parses an enum from an upper/lower/mixed-case name and maps an invalid value
    /// to a field-level <see cref="ValidationException"/> (convention of the API error
    /// contract). <paramref name="fieldName"/> is relative, e.g. <c>feeType</c>.
    /// </summary>
    public static TEnum ParseEnum<TEnum>(string? value, string fieldName)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException("A value is required.", FieldError(fieldName, "A value is required."));
        }

        if (!Enum.TryParse<TEnum>(value.Trim(), ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
        {
            var allowed = string.Join(", ", Enum.GetNames(typeof(TEnum)).OrderBy(n => n, StringComparer.Ordinal));
            throw new ValidationException(
                $"'{value}' is not a valid {fieldName}.",
                FieldError(fieldName, $"The value must be one of: {allowed}."));
        }

        return parsed;
    }

    public static IDictionary<string, string[]> FieldError(string fieldName, string message)
        => new Dictionary<string, string[]> { [fieldName] = new[] { message } };

    public static IDictionary<string, string[]> FieldErrors(params (string Field, string Message)[] errors)
    {
        var result = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var (field, message) in errors)
        {
            if (!result.TryGetValue(field, out var list))
            {
                result[field] = new[] { message };
            }
            else
            {
                result[field] = list.Append(message).ToArray();
            }
        }

        return result;
    }
}