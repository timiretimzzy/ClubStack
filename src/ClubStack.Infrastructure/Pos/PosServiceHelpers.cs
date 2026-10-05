using ClubStack.Application.Pos;
using ClubStack.Domain.Inventory;
using ClubStack.Domain.Pos;
using ClubStack.Infrastructure.Persistence.Entities;

namespace ClubStack.Infrastructure.Pos;

/// <summary>JSON contract used for refund payloads and idempotent result snapshots.</summary>
internal static class PosJson
{
    public static readonly System.Text.Json.JsonSerializerOptions Options = new(System.Text.Json.JsonSerializerDefaults.Web);

    public static string Serialize<T>(T value) => System.Text.Json.JsonSerializer.Serialize(value, Options);

    public static T Deserialize<T>(string json) => System.Text.Json.JsonSerializer.Deserialize<T>(json, Options) ?? throw new InvalidOperationException("The stored payload could not be deserialized.");
}

/// <summary>Computed pricing of a single sale line (pre-discount, discount and tax).</summary>
internal sealed record ComputedSaleLine(
    decimal OriginalUnitPrice,
    decimal UnitPrice,
    decimal DiscountedUnitPrice,
    SaleDiscountType? DiscountType,
    decimal? DiscountValue,
    decimal DiscountAmount,
    decimal TaxableBase,
    decimal TaxAmount,
    decimal LineTotal);

/// <summary>Shared provisioning helpers for the POS and cashiering services.</summary>
internal static class PosServiceHelpers
{
    public static TEnum ParseEnum<TEnum>(string? value, string fieldName)
        where TEnum : struct, Enum
        => ClubStack.Infrastructure.Inventory.InventoryServiceHelpers.ParseEnum<TEnum>(value, fieldName);

    public static IDictionary<string, string[]> FieldError(string fieldName, string message)
        => ClubStack.Infrastructure.Inventory.InventoryServiceHelpers.FieldError(fieldName, message);

    public static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static (int Page, int PageSize) NormalizePage(int page, int pageSize)
        => ClubStack.Infrastructure.Inventory.InventoryServiceHelpers.NormalizePage(page, pageSize);

    public static void EnsureGreaterThanZero(decimal value, string fieldName)
    {
        if (value <= 0m)
        {
            throw new ClubStack.Application.Exceptions.ValidationException(
                "The value is invalid.",
                FieldError(fieldName, "The value must be greater than zero."));
        }
    }

    public static void EnsureNotNegative(decimal value, string fieldName)
    {
        if (value < 0m)
        {
            throw new ClubStack.Application.Exceptions.ValidationException(
                "The value is invalid.",
                FieldError(fieldName, "The value must not be negative."));
        }
    }

    /// <summary>Resolves and validates a member's display name (preferred over legal).</summary>
    public static string MemberDisplayName(MemberEntity member)
        => member.Person is { } person && person.PreferredName.Length > 0 ? person.PreferredName : member.Person?.LegalName ?? string.Empty;

    /// <summary>
    /// Computes the discounted/effective unit price, discount amount, tax split and
    /// final line total for one sale line (D5 POS pricing hierarchy + tax). The
    /// discount is applied to the effective unit price before tax; the tax treatment
    /// and rate come from the product at sale time and are snapshotted onto the line.
    /// </summary>
    public static ComputedSaleLine ComputeLinePricing(
        decimal quantity,
        decimal originalUnitPrice,
        decimal effectiveUnitPrice,
        SaleDiscountLineInput? discount,
        ProductTaxTreatment treatment,
        decimal? taxRate)
    {
        SaleDiscountType? parsedType = null;
        decimal? discountValue = null;
        var discounted = PosPricing.Money(effectiveUnitPrice);

        if (discount is not null)
        {
            parsedType = ParseEnum<SaleDiscountType>(discount.DiscountType, "discountType");
            discountValue = discount.Value;
            if (discount.Value <= 0m)
            {
                throw new ClubStack.Application.Exceptions.ValidationException(
                    "The discount is invalid.",
                    FieldError("discountValue", "A discount value must be greater than zero."));
            }

            if (parsedType == SaleDiscountType.Percent && discount.Value > 100m)
            {
                throw new ClubStack.Application.Exceptions.ValidationException(
                    "The discount is invalid.",
                    FieldError("discountValue", "A percentage discount cannot exceed 100."));
            }

            discounted = PosPricing.ApplyDiscount(effectiveUnitPrice, parsedType.Value, discount.Value);
        }

        var discountAmount = PosPricing.Money((effectiveUnitPrice - discounted) * quantity);
        var (taxableBase, taxAmount, lineTotal) = PosPricing.ComputeTaxedLine(quantity, discounted, treatment, taxRate ?? 0m);

        return new ComputedSaleLine(
            PosPricing.Money(originalUnitPrice),
            PosPricing.Money(effectiveUnitPrice),
            discounted,
            parsedType,
            discountValue,
            discountAmount,
            taxableBase,
            taxAmount,
            lineTotal);
    }
}

/// <summary>Mappings between POS entities and application DTOs.</summary>
internal static class PosMappings
{
    public static PosOutletDto ToOutletDto(PosOutletEntity e)
        => new(e.Id, e.Code, e.Name, e.Notes, e.Status.ToString(), e.CreatedAtUtc, e.UpdatedAtUtc);

    public static PosRegisterDto ToRegisterDto(PosRegisterEntity e, Guid? currentSessionId = null)
        => new(
            e.Id,
            e.PosOutletId,
            e.PosOutlet.Code,
            e.PosOutlet.Name,
            e.Code,
            e.Name,
            e.InventoryLocationId,
            e.InventoryLocation.Code,
            e.InventoryLocation.Name,
            e.Status.ToString(),
            currentSessionId,
            e.CreatedAtUtc,
            e.UpdatedAtUtc);

    public static SaleLineDto ToSaleLineDto(SaleLineEntity l)
        => new(
            l.Id,
            l.ProductId,
            l.ProductSku,
            l.ProductName,
            l.Quantity,
            l.OriginalUnitPrice,
            l.UnitPrice,
            l.DiscountAmount,
            l.TaxableBase,
            l.TaxAmount,
            l.LineTotal,
            l.TaxTreatment.ToString(),
            l.TaxRateName,
            l.TaxRate,
            l.StockManaged);

    public static SalePaymentDto ToSalePaymentDto(SalePaymentEntity p)
        => new(
            p.Id,
            p.PaymentMethodCode,
            p.PaymentMethodName,
            p.Kind,
            p.Amount,
            p.ExternalReference,
            p.AccountTransactionId,
            p.AccountTransactionReference);

    public static SaleDiscountDto ToSaleDiscountDto(SaleDiscountEntity d)
        => new(
            d.Id,
            d.Level,
            d.LineId,
            d.DiscountType.ToString(),
            d.Value,
            d.Reason,
            d.AuthorizedByUserId,
            d.AppliedAtUtc);

    public static SaleDto ToSaleDto(SaleEntity s)
        => new(
            s.Id,
            s.SaleNumber,
            s.Status.ToString(),
            s.RegisterId,
            s.Register.Code,
            s.PosOutletId,
            s.Register.PosOutlet.Code,
            s.LocationId,
            s.Register.InventoryLocation.Code,
            s.CashierUserId,
            s.MemberId,
            s.MemberNumber,
            s.MemberName,
            s.CurrencyCode,
            s.Subtotal,
            s.DiscountTotal,
            s.TaxTotal,
            s.GrandTotal,
            s.Note,
            s.BusinessDateUtc,
            s.CompletedAtUtc,
            s.VoidedAtUtc,
            s.VoidReason,
            s.CreatedAtUtc,
            s.UpdatedAtUtc,
            s.Lines.Select(ToSaleLineDto).ToList(),
            s.Payments.Where(p => !p.IsRefund).Select(ToSalePaymentDto).ToList(),
            s.Discounts.Select(ToSaleDiscountDto).ToList(),
            s.Receipts.OrderByDescending(r => r.GeneratedAtUtc).Select(ToReceiptDto).FirstOrDefault());

    public static ReceiptDto ToReceiptDto(ReceiptEntity r)
        => new(
            r.Id,
            r.ReceiptNumber,
            r.SaleId,
            r.SaleNumber,
            r.Status,
            r.RegisterId,
            r.RegisterCode,
            r.PosOutletId,
            r.PosOutletCode,
            r.CashierUserId,
            r.MemberId,
            r.MemberNumber,
            r.MemberName,
            r.CurrencyCode,
            r.Subtotal,
            r.DiscountTotal,
            r.TaxTotal,
            r.GrandTotal,
            r.BusinessDateUtc,
            r.GeneratedAtUtc,
            r.Sale.Lines.Select(ToSaleLineDto).ToList(),
            r.Sale.Payments.Where(p => !p.IsRefund).Select(ToSalePaymentDto).ToList());

    public static SaleRefundDto ToRefundDto(SaleRefundEntity r)
        => new(
            r.Id,
            r.RefundNumber,
            r.SaleId,
            r.SaleNumber,
            r.Status,
            r.CurrencyCode,
            r.Amount,
            r.Reason,
            r.RefundedByUserId,
            r.RefundedAtUtc,
            r.CreatedAtUtc,
            PosJson.Deserialize<IReadOnlyList<SaleRefundLineDto>?>(r.LinesJson) ?? new List<SaleRefundLineDto>(),
            PosJson.Deserialize<IReadOnlyList<SaleRefundPaymentDto>?>(r.PaymentsJson) ?? new List<SaleRefundPaymentDto>());

    public static CashMovementDto ToCashMovementDto(CashMovementEntity m)
        => new(
            m.Id,
            m.SessionId,
            m.MovementType.ToString(),
            m.Amount,
            m.SignedAmount,
            m.Reason,
            m.CreatedByUserId,
            m.CreatedAtUtc);

    public static CashierSessionDto ToSessionDto(CashierSessionEntity s, decimal expectedCash, bool includeMovements = true)
        => new(
            s.Id,
            s.SessionNumber,
            s.RegisterId,
            s.Register.Code,
            s.CashierUserId,
            s.Status.ToString(),
            s.OpeningFloat,
            expectedCash,
            s.Status is CashierSessionStatus.Closed or CashierSessionStatus.Reconciled ? s.DeclaredCash : null,
            s.Variance,
            s.VarianceApproved,
            s.Note,
            s.OpenedAtUtc,
            s.StartedAtUtc,
            s.ClosingStartedAtUtc,
            s.ClosedAtUtc,
            s.ReconciledAtUtc,
            s.ReconciledByUserId,
            s.CreatedAtUtc,
            s.UpdatedAtUtc,
            includeMovements ? s.Movements.Select(ToCashMovementDto).ToList() : new List<CashMovementDto>());
}