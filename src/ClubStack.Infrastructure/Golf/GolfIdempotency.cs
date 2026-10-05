using ClubStack.Application.Exceptions;
using ClubStack.Application.Idempotency;
using System;
using System.Diagnostics;
using System.Text.Json;

namespace ClubStack.Infrastructure.Golf;

/// <summary>JSON contract used for golf idempotent result snapshots.</summary>
internal static class GolfJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T Deserialize<T>(string json)
        => JsonSerializer.Deserialize<T>(json, Options)
           ?? throw new InvalidOperationException("The stored payload could not be deserialized.");
}

/// <summary>
/// The D5/D6 idempotency contract, applied to the golf commands that move money or
/// consume capacity.
///
/// <para>Booking creation, confirmation, adding a charge and posting charges are the four
/// golf commands a client may retry: a dropped connection on a flaky clubhouse network is
/// routine, and a retry that created a second booking or a second set of receivables would
/// be worse than the failure it was hiding. Pure state moves that are already
/// self-protecting - check-in, complete, no-show, cancellation, tee-sheet generation and
/// guest status - are deliberately left without a key, because a second attempt fails
/// cleanly on the state machine and a key would only add a way to get the answer wrong.
/// </para>
/// </summary>
internal static class GolfIdempotency
{
    public const string CreateBookingOperation = "golf.booking.create";
    public const string ConfirmBookingOperation = "golf.booking.confirm";
    public const string AddChargeOperation = "golf.charge.add";
    public const string PostChargesOperation = "golf.charges.post";

    public static string? NormalizeKey(string? key)
    {
        var normalized = key?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }

    /// <summary>
    /// Replays a stored result for a repeated key, or refuses the request. A key reused
    /// with a materially different payload must never return the original operation's
    /// result, and must not overwrite it.
    /// </summary>
    public static T ReplayStoredOrConflict<T>(
        StoredIdempotencyResult? existing,
        string operation,
        string? requestHash)
    {
        if (existing is not null
            && requestHash is not null
            && existing.RequestHash is not null
            && !string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
        {
            throw new ConflictException(
                $"The idempotency key for this {operation} was already used with a different request payload.");
        }

        if (existing is { Status: IdempotencyStatus.Completed } && existing.ResultJson is not null)
        {
            return GolfJson.Deserialize<T>(existing.ResultJson);
        }

        if (existing is { Status: IdempotencyStatus.InProgress })
        {
            throw new ConflictException($"The {operation} with this idempotency key is still in progress.");
        }

        if (existing is { Status: IdempotencyStatus.Failed })
        {
            throw new ConflictException($"The {operation} with this idempotency key previously failed; retry the request.");
        }

        throw new ConflictException($"The {operation} with this idempotency key could not be processed.");
    }

    /// <summary>
    /// Releases a claimed key after a failed attempt so the same request can be retried.
    /// A secondary failure here must never mask the error the caller is about to rethrow.
    /// The failure is recorded via diagnostics so the key is never left in an ambiguous state.
    /// </summary>
    public static async Task MarkFailedAsync(
        IIdempotencyStore store,
        string? idempotencyKey,
        string operation,
        string actorUserId,
        string error,
        CancellationToken cancellationToken)
    {
        if (idempotencyKey is null)
        {
            return;
        }

        try
        {
            await store.FailAsync(idempotencyKey, operation, actorUserId, error, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Debug.WriteLine($"[GolfIdempotency] Failed to mark key inactive for {operation}: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
