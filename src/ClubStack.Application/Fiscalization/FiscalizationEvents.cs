using ClubStack.Application.Pos;

namespace ClubStack.Application.Fiscalization;

/// <summary>Event types raised by the D5.5 fiscalisation slice.</summary>
public static class FiscalizationEventTypes
{
    /// <summary>
    /// Raised once a sale has been committed and a fiscalisation record exists in
    /// <c>Pending</c>. Carries only the identifiers needed to load and fiscalise the
    /// sale; no money is duplicated into the outbox payload.
    /// </summary>
    public const string FiscalisationRequired = "FiscalisationRequired";

    /// <summary>Raised once a refund is committed and requires a fiscal credit note.</summary>
    public const string CreditNoteRequired = "FiscalisationCreditNoteRequired";
}

/// <summary>
/// Identifiers of a sale that needs a fiscal document. Deliberately narrow so a
/// retry after a worker restart can reload authoritative state rather than trust a
/// stale payload.
/// </summary>
public sealed record FiscalisationRequiredEvent(
    Guid FiscalisationId,
    Guid SaleId,
    Guid? RefundId,
    string CorrelationId);
