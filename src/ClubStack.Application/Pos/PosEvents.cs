namespace ClubStack.Application.Pos;

/// <summary>Event type names for POS/Cashiering outbox messages.</summary>
public static class PosEventTypes
{
    /// <summary>
    /// Emitted once a sale is completed (D5 POS). Carries a <see cref="SaleFinancialEvent"/>
    /// payload: the monetary facts a future finance slice (D6) replays into the
    /// general ledger. No handler is registered in D5 by design; the dispatcher
    /// records a logged no-op and the message is marked processed.
    /// </summary>
    public const string SaleFinancialEvent = "SaleFinancialEvent";

    /// <summary>Emitted once a refund is finalised (D5 POS), with a refund payload.</summary>
    public const string SaleRefundEvent = "SaleRefundEvent";
}

/// <summary>
/// Financial facts of a completed sale (D5 POS §22 finance boundary). The payload
/// is intentionally a snapshot: amounts, parties and the exact product/member
/// identifiers carried by the outbox message so a later GL slice needs no further
/// queries to post the sale.
/// </summary>
public sealed record SaleFinancialEvent(
    Guid SaleId,
    string SaleNumber,
    Guid PosOutletId,
    Guid RegisterId,
    Guid? LocationId,
    string CashierUserId,
    Guid? MemberId,
    string CurrencyCode,
    decimal Subtotal,
    decimal DiscountTotal,
    decimal TaxTotal,
    decimal GrandTotal,
    IReadOnlyList<SalePaymentDto> Payments,
    DateTimeOffset BusinessDateUtc,
    DateTimeOffset CompletedAtUtc,
    string CorrelationId);

/// <summary>Financial facts of a completed refund (D5 POS §22 finance boundary).</summary>
public sealed record SaleRefundEvent(
    Guid RefundId,
    string RefundNumber,
    Guid SaleId,
    string SaleNumber,
    Guid PosOutletId,
    Guid RegisterId,
    Guid? LocationId,
    string RefundedByUserId,
    Guid? MemberId,
    string CurrencyCode,
    decimal Amount,
    IReadOnlyList<SaleRefundPaymentDto> Payments,
    DateTimeOffset RefundedAtUtc,
    string CorrelationId);