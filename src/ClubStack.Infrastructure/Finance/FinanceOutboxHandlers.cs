using System.Text.Json;
using ClubStack.Application.Finance;
using ClubStack.Application.Outbox;
using ClubStack.Application.Pos;
using ClubStack.Domain.Finance;
using ClubStack.Infrastructure.Outbox;
using ClubStack.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClubStack.Infrastructure.Finance;

/// <summary>
/// Shared plumbing for the D6 outbox handlers: payload deserialisation and the
/// deterministic source identity that makes a redelivered message a no-op.
/// </summary>
internal static class FinanceHandlerSupport
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// A stable Guid derived from an operational record and a discriminator. One
    /// operational record can legitimately produce more than one journal — a mixed-tender
    /// POS sale posts a cash journal and a member-account journal — and the unique
    /// (SourceType, SourceId) index must still let each one through while rejecting a
    /// redelivery. Deriving the identity deterministically does both: the same
    /// (record, discriminator) always maps to the same source id, so a replay collides
    /// with its own original instead of posting again.
    /// </summary>
    public static Guid DeriveSourceId(Guid operationalRecordId, string discriminator)
    {
        var key = $"{operationalRecordId:N}:{discriminator}";
        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(key));

        return new Guid(hash.AsSpan(0, 16));
    }

    /// <summary>
    /// An unreadable payload is logged and swallowed rather than thrown: a malformed
    /// message can never be satisfied, so retrying it only delays the healthy messages.
    /// </summary>
    public static async Task<T?> ReadAsync<T>(
        ILogger logger,
        OutboxMessageData message,
        CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(message.PayloadJson, JsonOptions);
        }
        catch (JsonException exception)
        {
            logger.LogError(exception, "Finance outbox message {MessageId} has an unreadable payload", message.Id);
            return null;
        }
    }

    public static string Serialize<T>(T payload) => JsonSerializer.Serialize(payload, JsonOptions);

    /// <summary>
    /// The B18 amount components for a tax-inclusive sale: the gross amount, the net
    /// revenue and the tax. Mappings select these by name, which is how the tax payable
    /// account is configured rather than hard-coded.
    /// </summary>
    public static IReadOnlyList<FinancialPostingAmount> SplitAmounts(decimal gross, decimal tax)
    {
        var normalizedGross = FinanceDomain.NormalizeAmount(gross);
        var normalizedTax = FinanceDomain.NormalizeAmount(tax);

        return
        [
            new FinancialPostingAmount(FinancialPostingSelectors.Gross, normalizedGross),
            new FinancialPostingAmount(FinancialPostingSelectors.Net, FinanceDomain.NormalizeAmount(normalizedGross - normalizedTax)),
            new FinancialPostingAmount(FinancialPostingSelectors.Tax, normalizedTax),
        ];
    }
}

/// <summary>
/// Phase 8 §14.1 and §14.2. A completed POS sale is posted exactly once, and the rule is
/// chosen by the tender mix: money tendered on a member account is a member-account
/// charge (§14.2, Dr member receivable / Cr sales revenue) and everything else is a cash
/// sale (§14.1, Dr cash or the configured clearing account / Cr sales revenue). A sale is
/// never posted by both rules for the same money.
/// </summary>
public sealed class PosSaleFinancialOutboxHandler : IOutboxMessageHandler
{
    /// <summary>Seeded payment-method code for a member-account tender (D3 reference data).</summary>
    private const string AccountTenderCode = "ACCOUNT";

    private readonly IFinancePostingService _posting;
    private readonly IReceivableService _receivables;
    private readonly ClubStackDbContext _context;
    private readonly ILogger<PosSaleFinancialOutboxHandler> _logger;

    public PosSaleFinancialOutboxHandler(
        IFinancePostingService posting,
        IReceivableService receivables,
        ClubStackDbContext context,
        ILogger<PosSaleFinancialOutboxHandler> logger)
    {
        _posting = posting;
        _receivables = receivables;
        _context = context;
        _logger = logger;
    }

    public string HandledEventType => PosEventTypes.SaleFinancialEvent;

    public async Task HandleAsync(OutboxMessageData message, CancellationToken cancellationToken = default)
    {
        var payload = await FinanceHandlerSupport.ReadAsync<SaleFinancialEvent>(_logger, message, cancellationToken);
        if (payload is null)
        {
            return;
        }

        var actor = payload.CashierUserId;
        var correlationId = string.IsNullOrWhiteSpace(payload.CorrelationId) ? message.CorrelationId : payload.CorrelationId;

        // The tender rows decide which rules apply. Anything not settled on a member
        // account is money into cash/clearing.
        var accountTender = payload.Payments
            .Where(p => string.Equals(p.PaymentMethodCode, AccountTenderCode, StringComparison.OrdinalIgnoreCase))
            .Sum(p => p.Amount);

        var clearingTotal = FinanceDomain.NormalizeAmount(payload.GrandTotal - accountTender);

        if (clearingTotal > 0m)
        {
            var (gross, tax) = SplitForTender(payload.GrandTotal, payload.TaxTotal, clearingTotal);

            await _posting.PostAsync(
                new FinancialPostingRequest(
                    FinancialPostingRuleCodes.PosCashSale,
                    "PosSale",
                    FinanceHandlerSupport.DeriveSourceId(payload.SaleId, "cash"),
                    payload.SaleNumber,
                    payload.CompletedAtUtc,
                    payload.BusinessDateUtc,
                    payload.CurrencyCode,
                    null,
                    null,
                    $"POS cash sale {payload.SaleNumber}",
                    FinanceHandlerSupport.SplitAmounts(gross, tax)),
                actor,
                correlationId,
                cancellationToken);
        }

        if (accountTender > 0m)
        {
            if (payload.MemberId is not { } memberId)
            {
                // The sale recorded a member-account tender without a member, so there is
                // no receivable to charge. Refusing loudly beats posting to an unknown
                // member's ledger.
                _logger.LogError(
                    "Sale {SaleNumber} has a member-account tender of {Amount} but no member; no finance posting was made",
                    payload.SaleNumber,
                    accountTender);
                return;
            }

            var memberAccountId = await _context.MemberAccounts
                .AsNoTracking()
                .Where(a => a.MemberId == memberId)
                .OrderBy(a => a.OpenedAtUtc)
                .Select(a => (Guid?)a.Id)
                .FirstOrDefaultAsync(cancellationToken);

            var (accountGross, accountTax) = SplitForTender(payload.GrandTotal, payload.TaxTotal, accountTender);

            // The receivable is the document side of §14.2; the journal is its GL side.
            // CreateAsync posts through the same mapping table, so the journal and the
            // document commit together, and the B18 gross/net/tax split is carried by the
            // amount components rather than by a hard-coded account.
            var receivable = await _receivables.CreateAsync(
                new CreateReceivableInput(
                    memberId,
                    "PosSale",
                    FinanceHandlerSupport.DeriveSourceId(payload.SaleId, "account"),
                    payload.SaleNumber,
                    accountTender,
                    payload.CompletedAtUtc,
                    null,
                    payload.CurrencyCode,
                    null,
                    null,
                    $"POS member-account charge {payload.SaleNumber}",
                    FinancialPostingRuleCodes.PosMemberAccountCharge,
                    null,
                    FinanceHandlerSupport.SplitAmounts(accountGross, accountTax)),
                actor,
                cancellationToken);

            // A member-account tender that has no member account is still a valid sale, so
            // the receivable is simply not linked to one.
            if (memberAccountId is null)
            {
                _logger.LogWarning(
                    "Member {MemberId} has no member account; the receivable {Reference} for sale {SaleNumber} is not linked to one",
                    memberId,
                    receivable.Reference,
                    payload.SaleNumber);
            }
        }

        _logger.LogInformation(
            "Finance posting complete for sale {SaleNumber}: clearing {ClearingTotal}, member account {AccountTender}",
            payload.SaleNumber,
            clearingTotal,
            accountTender);
    }

    /// <summary>
    /// Splits a sale's gross and tax across a tender portion. With a single portion the
    /// exact figures are used; with a mixed tender each portion takes its proportional
    /// share, rounded to the B17 amount scale, and the clearing portion absorbs the
    /// rounding remainder so the two portions always re-add to the sale total exactly.
    /// </summary>
    private static (decimal Gross, decimal Tax) SplitForTender(decimal grandTotal, decimal taxTotal, decimal portion)
    {
        if (portion >= grandTotal)
        {
            return (FinanceDomain.NormalizeAmount(grandTotal), FinanceDomain.NormalizeAmount(taxTotal));
        }

        var gross = FinanceDomain.NormalizeAmount(grandTotal * portion / grandTotal);
        var tax = FinanceDomain.NormalizeAmount(taxTotal * portion / grandTotal);
        return (gross, tax);
    }
}

/// <summary>
/// Phase 8 §14.8. A refund reverses the applicable revenue, receivable and payment
/// effect. The handler does that by reversing every journal the original sale produced
/// (B4), which inverts revenue and the tender side in one controlled reversal journal
/// and leaves the original sale journals untouched.
/// </summary>
public sealed class PosRefundFinancialOutboxHandler : IOutboxMessageHandler
{
    private readonly IJournalService _journals;
    private readonly ClubStackDbContext _context;
    private readonly ILogger<PosRefundFinancialOutboxHandler> _logger;

    public PosRefundFinancialOutboxHandler(
        IJournalService journals,
        ClubStackDbContext context,
        ILogger<PosRefundFinancialOutboxHandler> logger)
    {
        _journals = journals;
        _context = context;
        _logger = logger;
    }

    public string HandledEventType => PosEventTypes.SaleRefundEvent;

    public async Task HandleAsync(OutboxMessageData message, CancellationToken cancellationToken = default)
    {
        var payload = await FinanceHandlerSupport.ReadAsync<SaleRefundEvent>(_logger, message, cancellationToken);
        if (payload is null)
        {
            return;
        }

        // Every journal the sale posted, in posting order, so the reversal mirrors it.
        var saleJournals = await _context.Journals
            .AsNoTracking()
            .Where(j => j.SourceType == "PosSale" && j.Status == JournalStatus.Posted)
            .Where(j => j.SourceId == FinanceHandlerSupport.DeriveSourceId(payload.SaleId, "cash") ||
                        j.SourceId == FinanceHandlerSupport.DeriveSourceId(payload.SaleId, "account"))
            .OrderBy(j => j.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        if (saleJournals.Count == 0)
        {
            _logger.LogWarning(
                "Refund {RefundNumber} references sale {SaleNumber}, which has no posted finance journal yet; nothing to reverse",
                payload.RefundNumber,
                payload.SaleNumber);
            return;
        }

        foreach (var journal in saleJournals)
        {
            await _journals.ReverseAsync(
                journal.Id,
                new ReverseJournalInput(
                    $"Refund {payload.RefundNumber} of sale {payload.SaleNumber}",
                    payload.RefundedAtUtc),
                payload.RefundedByUserId,
                cancellationToken);
        }

        // A refund of a member-account sale settles the receivable document as well. The
        // compensating journal is the reversal above, so the document is closed with a
        // reason instead of posting a second compensating entry.
        var receivable = await _context.AccountsReceivable
            .FirstOrDefaultAsync(
                a => a.SourceType == "PosSale" &&
                     a.SourceId == FinanceHandlerSupport.DeriveSourceId(payload.SaleId, "account") &&
                     a.Status != ReceivableStatus.Cancelled,
                cancellationToken);

        if (receivable is not null)
        {
            receivable.Status = ReceivableStatus.Cancelled;
            receivable.CancelledReason = $"Refunded in full by {payload.RefundNumber}.";
            receivable.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation(
            "Reversed {JournalCount} journal(s) for sale {SaleNumber} on behalf of refund {RefundNumber}",
            saleJournals.Count,
            payload.SaleNumber,
            payload.RefundNumber);
    }
}

/// <summary>
/// Phase 8 §14.4. A membership fee assessment debits member receivable and credits
/// membership revenue.
/// </summary>
public sealed class FinanceMembershipFeeOutboxHandler : IOutboxMessageHandler
{
    private readonly IFinancePostingService _posting;
    private readonly ILogger<FinanceMembershipFeeOutboxHandler> _logger;

    public FinanceMembershipFeeOutboxHandler(
        IFinancePostingService posting,
        ILogger<FinanceMembershipFeeOutboxHandler> logger)
    {
        _posting = posting;
        _logger = logger;
    }

    public string HandledEventType => FinanceEventTypes.MembershipFeeAssessed;

    public async Task HandleAsync(OutboxMessageData message, CancellationToken cancellationToken = default)
    {
        var payload = await FinanceHandlerSupport.ReadAsync<MembershipFeeAssessedEvent>(_logger, message, cancellationToken);
        if (payload is null)
        {
            return;
        }

        await _posting.PostAsync(
            new FinancialPostingRequest(
                FinancialPostingRuleCodes.MembershipFeeAssessment,
                FinanceEventTypes.MembershipFeeAssessed,
                payload.AssessmentId,
                payload.Reference,
                payload.TransactionDateUtc,
                payload.BusinessDateUtc,
                payload.CurrencyCode,
                null,
                null,
                string.IsNullOrWhiteSpace(payload.Description)
                    ? "Membership fee assessment"
                    : payload.Description,
                [new FinancialPostingAmount(null, payload.Amount)]),
            "system:finance-outbox",
            payload.CorrelationId,
            cancellationToken);

        _logger.LogInformation(
            "Posted membership fee assessment {Reference} of {Amount} {Currency}",
            payload.Reference,
            payload.Amount,
            payload.CurrencyCode);
    }
}

/// <summary>
/// Phase 8 §14.3. A payment received into a member account debits the cash/bank clearing
/// account and credits member receivable. The D3 account transaction stays the
/// authoritative record; this only mirrors it into the general ledger.
/// </summary>
public sealed class FinanceMemberPaymentOutboxHandler : IOutboxMessageHandler
{
    private readonly IFinancePostingService _posting;
    private readonly ILogger<FinanceMemberPaymentOutboxHandler> _logger;

    public FinanceMemberPaymentOutboxHandler(
        IFinancePostingService posting,
        ILogger<FinanceMemberPaymentOutboxHandler> logger)
    {
        _posting = posting;
        _logger = logger;
    }

    public string HandledEventType => FinanceEventTypes.MemberAccountPayment;

    public async Task HandleAsync(OutboxMessageData message, CancellationToken cancellationToken = default)
    {
        var payload = await FinanceHandlerSupport.ReadAsync<MemberAccountPaymentEvent>(_logger, message, cancellationToken);
        if (payload is null)
        {
            return;
        }

        await _posting.PostAsync(
            new FinancialPostingRequest(
                FinancialPostingRuleCodes.MemberAccountPayment,
                FinanceEventTypes.MemberAccountPayment,
                payload.AccountTransactionId,
                payload.Reference,
                payload.TransactionDateUtc,
                payload.BusinessDateUtc,
                payload.CurrencyCode,
                null,
                null,
                string.IsNullOrWhiteSpace(payload.Description)
                    ? "Member account payment"
                    : payload.Description,
                [new FinancialPostingAmount(null, payload.Amount)]),
            "system:finance-outbox",
            payload.CorrelationId,
            cancellationToken);

        _logger.LogInformation(
            "Posted member account payment {Reference} of {Amount} {Currency}",
            payload.Reference,
            payload.Amount,
            payload.CurrencyCode);
    }
}

/// <summary>
/// Phase 8 §14.4. A membership fee assessment debits member receivable and credits
/// membership revenue.
/// </summary>