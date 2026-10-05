using ClubStack.Hardware.Fiscalization;

namespace ClubStack.Infrastructure.Persistence.Entities;

/// <summary>
/// ZIMRA fiscalisation record (D5.5, <c>fiscalisations</c>). Exactly one row exists per
/// fiscalisable sale or refund. The row is the durable statement that a fiscal
/// document is <em>owed</em> to the tax authority, so it is written in the same
/// transaction that commits the sale and is never inferred from sale completion.
/// </summary>
public sealed class FiscalisationEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid SaleId { get; set; }

    public SaleEntity? Sale { get; set; }

    public string SaleNumber { get; set; } = string.Empty;

    /// <summary>Set when this row represents a credit note raised by a refund.</summary>
    public Guid? RefundId { get; set; }

    public SaleRefundEntity? Refund { get; set; }

    public string? RefundNumber { get; set; }

    /// <summary>Invoice number presented to the device. Reuses the ClubStack sale number.</summary>
    public string InvoiceNumber { get; set; } = string.Empty;

    /// <summary>FiscalInvoice, CreditNote or DebitNote.</summary>
    public string DocumentKind { get; set; } = nameof(FiscalDocumentKind.FiscalInvoice);

    /// <summary>Original fiscalised invoice number for a credit/debit note.</summary>
    public string? OriginalInvoiceNumber { get; set; }

    /// <summary>
    /// <see cref="FiscalisationState"/> as a string so the persisted value stays
    /// readable and does not depend on enum member reordering.
    /// </summary>
    public string Status { get; set; } = nameof(FiscalisationState.Pending);

    public string? Code { get; set; }

    public string? Message { get; set; }

    public string? ErrorInfo { get; set; }

    public string? DeviceId { get; set; }

    public string? DeviceSerialNumber { get; set; }

    public string? FiscalDayNo { get; set; }

    public string? ReceiptCounter { get; set; }

    public string? ReceiptGlobalNo { get; set; }

    public string? ReceiptId { get; set; }

    public string? OperationId { get; set; }

    public string? DeviceSignature { get; set; }

    /// <summary>FDMS/server signature, populated once the device reports a synchronised state.</summary>
    public string? ServerSignature { get; set; }

    public string? QrUrl { get; set; }

    public bool PendingSync { get; set; }

    /// <summary>
    /// How the ClubStack tender breakdown was represented to the engine. Recorded
    /// explicitly so the aggregate sent to the device is never mistaken for the
    /// authoritative ClubStack payment record.
    /// </summary>
    public string? PaymentMapping { get; set; }

    /// <summary>ClubStack tender rows preserved verbatim as a JSON snapshot.</summary>
    public string TendersJson { get; set; } = "[]";

    /// <summary>Engine-side validation messages, preserved verbatim.</summary>
    public string ValidationWarningsJson { get; set; } = "[]";

    public int AttemptCount { get; set; }

    public DateTimeOffset? SubmittedAtUtc { get; set; }

    public DateTimeOffset? FiscalisedAtUtc { get; set; }

    public DateTimeOffset? LastAttemptAtUtc { get; set; }

    public string? CorrelationId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
