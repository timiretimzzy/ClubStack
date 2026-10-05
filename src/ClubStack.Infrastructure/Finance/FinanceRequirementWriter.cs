using System.Text.Json;
using ClubStack.Application.Finance;
using ClubStack.Application.Outbox;
using ClubStack.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace ClubStack.Infrastructure.Finance;

/// <summary>
/// Enqueues the D6 finance outbox messages for operational records that D3/D4 already
/// own. The message is written inside the same transaction as the operational row, so an
/// operational record and the statement that it owes a journal commit together and a
/// crash can never leave one without the other.
///
/// This writer never posts anything itself: it only records what the finance slice
/// owes, and <c>OutboxProcessorJob</c> drives the posting later.
/// </summary>
public interface IFinanceRequirementWriter
{
    /// <summary>Phase 8 §14.4 — a membership fee assessment owes a Dr receivable / Cr membership revenue journal.</summary>
    Task RequireMembershipFeePostingAsync(
        Guid assessmentId,
        Guid membershipId,
        Guid memberId,
        Guid? memberAccountId,
        string reference,
        string description,
        decimal amount,
        string currencyCode,
        DateTimeOffset transactionDate,
        DateTimeOffset businessDate,
        string correlationId,
        CancellationToken cancellationToken = default);

    /// <summary>Phase 8 §14.3 — a payment into a member account owes a Dr cash / Cr receivable journal.</summary>
    Task RequireMemberPaymentPostingAsync(
        Guid accountTransactionId,
        Guid memberAccountId,
        Guid memberId,
        string reference,
        string description,
        decimal amount,
        string currencyCode,
        DateTimeOffset transactionDate,
        DateTimeOffset businessDate,
        string correlationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Phase 8 §14.5 is deliberately not wired to an operational emitter. The section
    /// states that the exact timing depends on the configured procurement accounting
    /// policy, so auto-posting a stock receipt would be inventing policy the PM pack
    /// deliberately left open. The rule is seeded and configurable; a club that posts on
    /// receipt raises the payable through the finance API with that rule code.
    /// </summary>
}

/// <inheritdoc />
public sealed class FinanceRequirementWriter : IFinanceRequirementWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IOutboxStore _outbox;
    private readonly ILogger<FinanceRequirementWriter> _logger;

    public FinanceRequirementWriter(IOutboxStore outbox, ILogger<FinanceRequirementWriter> logger)
    {
        _outbox = outbox;
        _logger = logger;
    }

    public Task RequireMembershipFeePostingAsync(
        Guid assessmentId,
        Guid membershipId,
        Guid memberId,
        Guid? memberAccountId,
        string reference,
        string description,
        decimal amount,
        string currencyCode,
        DateTimeOffset transactionDate,
        DateTimeOffset businessDate,
        string correlationId,
        CancellationToken cancellationToken = default) =>
        EnqueueAsync(
            FinanceEventTypes.MembershipFeeAssessed,
            new MembershipFeeAssessedEvent(
                assessmentId,
                membershipId,
                memberId,
                memberAccountId ?? Guid.Empty,
                reference,
                description,
                amount,
                currencyCode,
                transactionDate,
                businessDate,
                correlationId),
            "membership fee assessment",
            reference,
            cancellationToken);

    public Task RequireMemberPaymentPostingAsync(
        Guid accountTransactionId,
        Guid memberAccountId,
        Guid memberId,
        string reference,
        string description,
        decimal amount,
        string currencyCode,
        DateTimeOffset transactionDate,
        DateTimeOffset businessDate,
        string correlationId,
        CancellationToken cancellationToken = default) =>
        EnqueueAsync(
            FinanceEventTypes.MemberAccountPayment,
            new MemberAccountPaymentEvent(
                accountTransactionId,
                memberAccountId,
                memberId,
                reference,
                description,
                amount,
                currencyCode,
                transactionDate,
                businessDate,
                correlationId),
            "member account payment",
            reference,
            cancellationToken);

    public async Task EnqueueAsync<T>(
        string eventType,
        T payload,
        string what,
        string subject,
        CancellationToken cancellationToken)
    {
        await _outbox.EnqueueAsync(
            eventType,
            JsonSerializer.Serialize(payload, JsonOptions),
            string.Empty,
            cancellationToken);

        _logger.LogInformation(
            "Finance posting required for {What} {Subject} ({EventType}).",
            what,
            subject,
            eventType);
    }
}
