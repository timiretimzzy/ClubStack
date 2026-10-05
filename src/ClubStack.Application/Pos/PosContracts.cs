namespace ClubStack.Application.Pos;

// ---- Catalog (D5 POS product lookup) ----

public sealed record PosProductSearchInput(
    string? Query,
    Guid? CategoryId,
    Guid? LocationId,
    Guid? MemberId,
    int Page = 1,
    int PageSize = 50);

public sealed record PosProductDto(
    Guid Id,
    string Sku,
    string Name,
    string? Barcode,
    string UnitOfMeasure,
    bool StockManaged,
    decimal UnitPrice,
    string PriceSourceType,
    bool IsOverride,
    decimal? AvailableQuantity,
    string CurrencyCode,
    string TaxTreatment,
    string? TaxRateName,
    decimal? TaxRate);

public sealed record PosProductSearchResult(
    IReadOnlyList<PosProductDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

// ---- Member identification (D5 POS §member identification) ----

public sealed record PosMemberDto(
    Guid MemberId,
    string MemberNumber,
    string MemberName,
    string Status,
    Guid? CredentialId,
    string? CredentialNumber,
    string? CredentialStatus);

public sealed record PosMemberSearchInput(string? Query, Guid? CredentialId, int Page = 1, int PageSize = 50);

public sealed record PosMemberSearchResult(IReadOnlyList<PosMemberDto> Items, int TotalCount, int Page, int PageSize);

// ---- POS outlet / register configuration ----

public sealed record CreatePosOutletInput(string Code, string Name, string? Notes);

public sealed record PosOutletDto(Guid Id, string Code, string Name, string? Notes, string Status, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);

public sealed record CreatePosRegisterInput(
    Guid PosOutletId,
    string Code,
    string Name,
    Guid InventoryLocationId);

public sealed record PosRegisterDto(
    Guid Id,
    Guid PosOutletId,
    string PosOutletCode,
    string PosOutletName,
    string Code,
    string Name,
    Guid InventoryLocationId,
    string InventoryLocationCode,
    string InventoryLocationName,
    string Status,
    Guid? CurrentSessionId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record PosConfigurationState(
    IReadOnlyList<PosOutletDto> Outlets,
    IReadOnlyList<PosRegisterDto> Registers);

// ---- Sales (D5 POS) ----

public sealed record CreateSaleLineInput(
    Guid ProductId,
    decimal Quantity,
    decimal? UnitPriceOverride,
    SaleDiscountLineInput? Discount);

public sealed record SaleDiscountLineInput(string DiscountType, decimal Value, string? Reason);

public sealed record CreateSaleInput(
    Guid RegisterId,
    Guid? MemberId,
    string? CredentialValue,
    IReadOnlyList<CreateSaleLineInput> Lines,
    string? Note,
    DateTimeOffset? BusinessDateUtc);

public sealed record SalePaymentInput(string PaymentMethodCode, decimal Amount, string? ExternalReference);

public sealed record CompleteSaleInput(
    Guid? MemberId,
    IReadOnlyList<SalePaymentInput> Payments,
    string? IdempotencyKey);

public sealed record VoidSaleInput(string Reason);

public sealed record RefundSaleInput(
    string Reason,
    IReadOnlyList<SalePaymentInput>? Payments,
    string? IdempotencyKey);

public sealed record SaleLineDto(
    Guid Id,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    decimal Quantity,
    decimal OriginalUnitPrice,
    decimal UnitPrice,
    decimal DiscountAmount,
    decimal TaxableBase,
    decimal TaxAmount,
    decimal LineTotal,
    string TaxTreatment,
    string? TaxRateName,
    decimal? TaxRate,
    bool StockManaged);

public sealed record SalePaymentDto(
    Guid Id,
    string PaymentMethodCode,
    string PaymentMethodName,
    string Kind,
    decimal Amount,
    string? ExternalReference,
    Guid? AccountTransactionId,
    string? AccountTransactionReference);

public sealed record SaleDiscountDto(
    Guid Id,
    string Level,
    Guid? LineId,
    string DiscountType,
    decimal Value,
    string? Reason,
    string AuthorizedByUserId,
    DateTimeOffset AppliedAtUtc);

public sealed record SaleDto(
    Guid Id,
    string SaleNumber,
    string Status,
    Guid RegisterId,
    string RegisterCode,
    Guid PosOutletId,
    string PosOutletCode,
    Guid? LocationId,
    string? LocationCode,
    string CashierUserId,
    Guid? MemberId,
    string? MemberNumber,
    string? MemberName,
    string CurrencyCode,
    decimal Subtotal,
    decimal DiscountTotal,
    decimal TaxTotal,
    decimal GrandTotal,
    string? Note,
    DateTimeOffset BusinessDateUtc,
    DateTimeOffset? CompletedAtUtc,
    DateTimeOffset? VoidedAtUtc,
    string? VoidReason,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<SaleLineDto> Lines,
    IReadOnlyList<SalePaymentDto> Payments,
    IReadOnlyList<SaleDiscountDto> Discounts,
    ReceiptDto? Receipt);

public sealed record ReceiptDto(
    Guid Id,
    string ReceiptNumber,
    Guid SaleId,
    string SaleNumber,
    string Status,
    Guid RegisterId,
    string RegisterCode,
    Guid PosOutletId,
    string PosOutletCode,
    string CashierUserId,
    Guid? MemberId,
    string? MemberNumber,
    string? MemberName,
    string CurrencyCode,
    decimal Subtotal,
    decimal DiscountTotal,
    decimal TaxTotal,
    decimal GrandTotal,
    DateTimeOffset BusinessDateUtc,
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<SaleLineDto> Lines,
    IReadOnlyList<SalePaymentDto> Payments);

public sealed record SaleSearchInput(
    string? Status,
    Guid? RegisterId,
    Guid? MemberId,
    DateTimeOffset? FromDateUtc,
    DateTimeOffset? ToDateUtc,
    int Page = 1,
    int PageSize = 50);

public sealed record SaleSearchResult(IReadOnlyList<SaleDto> Items, int TotalCount, int Page, int PageSize);

// ---- Refunds (D5 POS §refunds) ----

/// <summary>
/// A refunded sale line, snapshotted at refund time.
///
/// D5.5 added the tax fields. A credit note has to reverse the tax of the exact lines
/// it credits, and a proportional share of the invoice total cannot reproduce that on a
/// mixed-rate sale: the invoice's tax is the sum of its line taxes, so a share of the
/// total smears a standard-rated line's tax across a zero-rated line. The tax values are
/// therefore snapshotted here, satisfying DATA-003 (P2:1607 "Transactions MUST retain
/// relevant historical values") and P9:259.
///
/// The tax members are optional so that refund payloads written before D5.5 still
/// deserialize. A line with no snapshotted tax treatment is a legacy snapshot and is
/// handled by <see cref="RefundTaxSnapshotState"/>.
/// </summary>
public sealed record SaleRefundLineDto(
    Guid ProductId,
    string ProductSku,
    string ProductName,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    decimal TaxableBase = 0m,
    decimal TaxAmount = 0m,
    string? TaxTreatment = null,
    string? TaxRateName = null,
    decimal? TaxRate = null);

/// <summary>Whether a stored refund line snapshot carries authoritative per-line tax.</summary>
public enum RefundTaxSnapshotState
{
    /// <summary>No line carries snapshotted tax: the refund predates D5.5.</summary>
    Legacy = 0,

    /// <summary>Every line carries snapshotted tax and is authoritative.</summary>
    Authoritative = 1,

    /// <summary>Some lines carry tax and some do not: the snapshot is inconsistent.</summary>
    Inconsistent = 2,
}

public sealed record SaleRefundPaymentDto(string PaymentMethodCode, string PaymentMethodName, string Kind, decimal Amount, string? ExternalReference);

public sealed record SaleRefundDto(
    Guid Id,
    string RefundNumber,
    Guid SaleId,
    string SaleNumber,
    string Status,
    string CurrencyCode,
    decimal Amount,
    string Reason,
    string RefundedByUserId,
    DateTimeOffset RefundedAtUtc,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<SaleRefundLineDto> Lines,
    IReadOnlyList<SaleRefundPaymentDto> Payments);

// ---- Cashier sessions (D5 Cashiering) ----

public sealed record OpenSessionInput(Guid RegisterId, decimal OpeningFloat, string? Note);

public sealed record CashMovementInput(string MovementType, decimal Amount, string? Reason);

public sealed record CloseSessionInput(decimal DeclaredCash);

public sealed record CashierSessionDto(
    Guid Id,
    string SessionNumber,
    Guid RegisterId,
    string RegisterCode,
    string CashierUserId,
    string Status,
    decimal OpeningFloat,
    decimal ExpectedCash,
    decimal? DeclaredCash,
    decimal? Variance,
    bool VarianceApproved,
    string? Note,
    DateTimeOffset OpenedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? ClosingStartedAtUtc,
    DateTimeOffset? ClosedAtUtc,
    DateTimeOffset? ReconciledAtUtc,
    string? ReconciledByUserId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<CashMovementDto> Movements);

public sealed record CashMovementDto(
    Guid Id,
    Guid SessionId,
    string MovementType,
    decimal Amount,
    decimal SignedAmount,
    string? Reason,
    string CreatedByUserId,
    DateTimeOffset CreatedAtUtc);

public sealed record CashierSessionSearchInput(Guid? RegisterId, string? Status, int Page = 1, int PageSize = 50);

public sealed record CashierSessionSearchResult(IReadOnlyList<CashierSessionDto> Items, int TotalCount, int Page, int PageSize);

// ---- Services ----

public interface IPosProductService
{
    Task<PosProductSearchResult> SearchAsync(PosProductSearchInput input, CancellationToken cancellationToken = default);

    Task<PosMemberSearchResult> SearchMembersAsync(PosMemberSearchInput input, CancellationToken cancellationToken = default);
}

public interface IPosConfigurationService
{
    Task<PosConfigurationState> GetStateAsync(CancellationToken cancellationToken = default);

    Task<PosOutletDto> CreateOutletAsync(CreatePosOutletInput input, string actorUserId, CancellationToken cancellationToken = default);

    Task<PosRegisterDto> CreateRegisterAsync(CreatePosRegisterInput input, string actorUserId, CancellationToken cancellationToken = default);
}

public interface ISaleService
{
    Task<SaleDto> CreateAsync(CreateSaleInput input, string actorUserId, CancellationToken cancellationToken = default);

    Task<SaleDto> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<SaleDto> CompleteAsync(Guid id, CompleteSaleInput input, string actorUserId, string correlationId, CancellationToken cancellationToken = default);

    Task<SaleDto> VoidAsync(Guid id, VoidSaleInput input, string actorUserId, CancellationToken cancellationToken = default);

    Task<SaleRefundDto> RefundAsync(Guid id, RefundSaleInput input, string actorUserId, string correlationId, CancellationToken cancellationToken = default);

    Task<ReceiptDto> GetReceiptAsync(Guid id, CancellationToken cancellationToken = default);

    Task<SaleSearchResult> SearchAsync(SaleSearchInput input, CancellationToken cancellationToken = default);
}

public interface ICashierSessionService
{
    Task<CashierSessionDto> OpenAsync(OpenSessionInput input, string operatorUserId, CancellationToken cancellationToken = default);

    Task<CashierSessionDto> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CashierSessionSearchResult> SearchAsync(CashierSessionSearchInput input, CancellationToken cancellationToken = default);

    Task<CashierSessionDto> AddCashMovementAsync(Guid id, CashMovementInput input, string operatorUserId, CancellationToken cancellationToken = default);

    Task<CashierSessionDto> CloseAsync(Guid id, CloseSessionInput input, string operatorUserId, CancellationToken cancellationToken = default);

    Task<CashierSessionDto> ReconcileAsync(Guid id, string operatorUserId, CancellationToken cancellationToken = default);
}