using System.Text.Json;
using ClubStack.Application.Pos;
using ClubStack.Domain.Inventory;
using ClubStack.Hardware.Fiscalization;
using ClubStack.Infrastructure.Persistence.Entities;
using ClubStack.Infrastructure.Pos;

namespace ClubStack.Infrastructure.Fiscalization;

/// <summary>
/// The stored data cannot produce an honest fiscal document — for example a refund line
/// snapshot that carries tax on some lines but not others.
///
/// This is deliberately distinct from a device failure: retrying cannot fix it, because the
/// data itself is inconsistent. The processor records it as a permanent failure so an
/// authorised operator can resolve the record, rather than deferring it forever or letting
/// an untyped exception escape.
/// </summary>
internal sealed class FiscalDocumentBuildException : Exception
{
    public FiscalDocumentBuildException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// Builds the <see cref="FiscalDocument"/> for a committed sale or refund.
///
/// Every monetary and tax value is copied from the persisted D5 sale snapshot. Nothing
/// is re-priced, re-rated or re-rounded here: ClubStack is authoritative for the amounts
/// and tax treatment (PM DECISION 3), and the engine is handed those figures.
/// </summary>
internal static class FiscalDocumentBuilder
{
    /// <summary>HS code used when the product carries none.</summary>
    private const string DefaultHsCode = "62010000";

    private const string CreditNoteLineName = "Credit note";

    public static FiscalDocument Build(
        SaleEntity sale,
        SaleRefundEntity? refund,
        FiscalDeviceConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(sale);
        ArgumentNullException.ThrowIfNull(configuration);

        var isCreditNote = refund is not null;
        var creditNote = refund!;
        var refundLines = isCreditNote ? ReadRefundLines(creditNote) : [];
        return new FiscalDocument
        {
            Kind = isCreditNote ? FiscalDocumentKind.CreditNote : FiscalDocumentKind.FiscalInvoice,
            CurrencyCode = sale.CurrencyCode,
            CustomerName = sale.MemberName ?? "Walk-in Customer",
            InvoiceNumber = isCreditNote ? creditNote.RefundNumber : sale.SaleNumber,
            OriginalInvoiceNumber = isCreditNote ? sale.SaleNumber : null,
            Comment = isCreditNote
                ? $"Credit note for sale {sale.SaleNumber} - {creditNote.Reason}"
                : $"ClubStack sale {sale.SaleNumber}",
            Total = isCreditNote ? creditNote.Amount : sale.GrandTotal,
            TaxTotal = isCreditNote
                ? CalculateRefundTax(sale, creditNote, refundLines)
                : sale.TaxTotal,
            Lines = BuildLines(sale, refund, refundLines, configuration),
            Tenders = BuildTenders(sale, refund),
        };
    }

    private static IReadOnlyList<SaleRefundLineDto> ReadRefundLines(SaleRefundEntity refund)
        => JsonSerializer.Deserialize<IReadOnlyList<SaleRefundLineDto>>(refund.LinesJson, PosJson.Options) ?? [];

    private static IReadOnlyList<FiscalLine> BuildLines(
        SaleEntity sale,
        SaleRefundEntity? refund,
        IReadOnlyList<SaleRefundLineDto> refundLines,
        FiscalDeviceConfiguration configuration)
    {
        if (refund is not null && refundLines.Count > 0)
        {
            var state = ClassifyRefundTaxSnapshot(refundLines);

            if (state == RefundTaxSnapshotState.Authoritative)
            {
                // Authoritative path (D5.5). Each credit-note line carries the tax
                // treatment, rate and amount of the sale line it reverses, and resolves
                // its device tax identifier from that treatment. A mixed-rate invoice
                // therefore credits each line into its own bracket.
                return refundLines
                    .Select(l => new FiscalLine
                    {
                        ItemCode = string.IsNullOrWhiteSpace(l.ProductSku) ? DefaultHsCode : l.ProductSku,
                        Name1 = CreditNoteLineName,
                        Name2 = l.TaxRateName ?? l.ProductName,
                        Quantity = l.Quantity,
                        Price = l.UnitPrice,
                        Amount = l.LineTotal,
                        Tax = l.TaxAmount,
                        TaxRate = l.TaxRate ?? 0m,
                        TaxId = TaxIdFor(ParseTreatment(l.TaxTreatment), configuration),
                    })
                    .ToArray();
            }

            // Legacy path. Refunds written before D5.5 carry no per-line tax, so the only
            // tax available is the invoice's. Keep the previous proportional treatment so
            // those credit notes still balance; new refunds never take this branch.
            if (state == RefundTaxSnapshotState.Inconsistent)
            {
                throw new FiscalDocumentBuildException(
                    "The stored refund line snapshot carries tax on some lines but not all, so the credit note cannot be built from authoritative data.");
            }

            var share = sale.GrandTotal <= 0m ? 0m : refund.Amount / sale.GrandTotal;
            var taxTotal = decimal.Round(sale.TaxTotal * share, 2, MidpointRounding.AwayFromZero);
            var lineTax = decimal.Round(taxTotal / refundLines.Count, 2, MidpointRounding.AwayFromZero);

            return refundLines
                .Select(l => new FiscalLine
                {
                    ItemCode = string.IsNullOrWhiteSpace(l.ProductSku) ? DefaultHsCode : l.ProductSku,
                    Name1 = CreditNoteLineName,
                    Name2 = l.ProductName,
                    Quantity = l.Quantity,
                    Price = l.UnitPrice,
                    Amount = l.LineTotal,
                    Tax = lineTax,
                    TaxRate = 0m,
                    TaxId = configuration.StandardRatedTaxId,
                })
                .ToArray();
        }

        return sale.Lines
            .Select(line => new FiscalLine
            {
                ItemCode = string.IsNullOrWhiteSpace(line.ProductSku) ? DefaultHsCode : line.ProductSku,
                Name1 = line.ProductName,
                Name2 = line.TaxRateName,
                Quantity = line.Quantity,
                Price = line.UnitPrice,
                Amount = line.LineTotal,
                Tax = line.TaxAmount,
                TaxRate = line.TaxRate ?? 0m,
                TaxId = TaxIdFor(line.TaxTreatment, configuration),
            })
            .ToArray();
    }

    /// <summary>
    /// Decides whether a stored refund line snapshot carries authoritative per-line tax.
    /// A snapshot where only some lines have tax is corrupt: silently treating it as legacy
    /// would credit a line at zero tax, and silently treating it as authoritative would
    /// credit the untagged lines at zero tax too. Both are wrong, so it is rejected.
    /// </summary>
    private static RefundTaxSnapshotState ClassifyRefundTaxSnapshot(IReadOnlyList<SaleRefundLineDto> refundLines)
    {
        var tagged = refundLines.Count(l => l.TaxTreatment is not null);
        if (tagged == 0)
        {
            return RefundTaxSnapshotState.Legacy;
        }

        return tagged == refundLines.Count
            ? RefundTaxSnapshotState.Authoritative
            : RefundTaxSnapshotState.Inconsistent;
    }

    private static ProductTaxTreatment ParseTreatment(string? treatment)
        => Enum.TryParse<ProductTaxTreatment>(treatment, ignoreCase: true, out var parsed)
            ? parsed
            : ProductTaxTreatment.Exclusive;

    private static IReadOnlyList<FiscalTender> BuildTenders(SaleEntity sale, SaleRefundEntity? refund)
    {
        var tenders = new List<FiscalTender>();

        if (refund is not null)
        {
            var refundPayments = JsonSerializer
                .Deserialize<IReadOnlyList<SaleRefundPaymentDto>>(refund.PaymentsJson, PosJson.Options) ?? [];

            foreach (var tender in refundPayments)
            {
                tenders.Add(new FiscalTender(
                    tender.PaymentMethodCode,
                    tender.PaymentMethodName,
                    tender.Kind,
                    tender.Amount,
                    tender.ExternalReference));
            }

            return tenders;
        }

        foreach (var payment in sale.Payments.Where(p => !p.IsRefund))
        {
            tenders.Add(new FiscalTender(
                payment.PaymentMethodCode,
                payment.PaymentMethodName,
                payment.Kind,
                payment.Amount,
                payment.ExternalReference));
        }

        return tenders;
    }

    /// <summary>
    /// Tax component of a credit note.
    ///
    /// Authoritative (D5.5): the sum of the snapshotted per-line tax. Because an invoice's
    /// tax total is itself the sum of its line taxes, a refund of the whole sale credits
    /// exactly the tax the invoice charged, and a mixed-rate sale credits each line into
    /// the bracket it was charged in.
    ///
    /// Legacy: refunds written before D5.5 have no per-line tax, so the only available
    /// figure is the refund's proportional share of the invoice tax total.
    /// </summary>
    private static decimal CalculateRefundTax(
        SaleEntity sale,
        SaleRefundEntity refund,
        IReadOnlyList<SaleRefundLineDto> refundLines)
    {
        var state = ClassifyRefundTaxSnapshot(refundLines);
        if (state == RefundTaxSnapshotState.Authoritative)
        {
            return refundLines.Sum(l => l.TaxAmount);
        }

        if (state == RefundTaxSnapshotState.Inconsistent)
        {
            throw new FiscalDocumentBuildException(
                "The stored refund line snapshot carries tax on some lines but not all, so the credit note tax cannot be determined from authoritative data.");
        }

        if (sale.GrandTotal <= 0m)
        {
            return 0m;
        }

        return decimal.Round(sale.TaxTotal * (refund.Amount / sale.GrandTotal), 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Maps ClubStack's tax treatment onto the device's tax identifier. The identifiers
    /// come from the device configuration, never from a hard-coded value; 0 asks the
    /// engine to select from its own table when configuration is ambiguous.
    /// </summary>
    private static int TaxIdFor(ProductTaxTreatment treatment, FiscalDeviceConfiguration configuration) => treatment switch
    {
        ProductTaxTreatment.ZeroRated => configuration.ZeroRatedTaxId,
        _ => configuration.StandardRatedTaxId,
    };
}
