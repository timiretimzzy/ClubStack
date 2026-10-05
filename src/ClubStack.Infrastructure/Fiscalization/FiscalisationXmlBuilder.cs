using System.Globalization;
using System.Text;
using ClubStack.Hardware.Fiscalization;

namespace ClubStack.Infrastructure.Fiscalization;

/// <summary>
/// Builds the item and currency XML the FiscalStackZW engine expects.
/// Element names and ordering mirror the proven FiscalStackZWPOS.App mapping
/// (ITEMCODE, ITEMNAME1, ITEMNAME2, QTY, PRICE, AMT, TAX, TAXR, TAXID). Values are
/// taken from amounts ClubStack has already calculated; nothing is re-derived or
/// re-priced here.
/// </summary>
public static class FiscalisationXmlBuilder
{
    /// <summary>
    /// HS code used when a ClubStack product carries none. The engine normalises
    /// unknown codes to its own default and emits a validation warning, so a stable
    /// non-empty placeholder is preferable to an empty element.
    /// </summary>
    public const string DefaultHsCode = "10000000";
    public static string BuildItems(IReadOnlyList<FiscalLine> lines)
    {
        var sb = new StringBuilder("<ITEMS>");
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            sb.Append("<ITEM>");
            sb.Append("<HH>").Append((i + 1).ToString(CultureInfo.InvariantCulture)).Append("</HH>");
            sb.Append("<ITEMCODE>").Append(Esc(NullIfBlank(line.ItemCode, DefaultHsCode))).Append("</ITEMCODE>");
            sb.Append("<ITEMNAME1>").Append(Esc(Truncate(line.Name1, 60))).Append("</ITEMNAME1>");
            sb.Append("<ITEMNAME2>").Append(Esc(Truncate(NullIfBlank(line.Name2, line.Name1), 60))).Append("</ITEMNAME2>");
            sb.Append("<QTY>").Append(Money(line.Quantity)).Append("</QTY>");
            sb.Append("<PRICE>").Append(Money(line.Price)).Append("</PRICE>");
            sb.Append("<AMT>").Append(Money(line.Amount)).Append("</AMT>");
            sb.Append("<TAX>").Append(Money(line.Tax)).Append("</TAX>");
            sb.Append("<TAXR>").Append(Rate(line.TaxRate)).Append("</TAXR>");
            sb.Append("<TAXID>").Append(line.TaxId.ToString(CultureInfo.InvariantCulture)).Append("</TAXID>");
            sb.Append("</ITEM>");
        }

        sb.Append("</ITEMS>");
        return sb.ToString();
    }

    /// <summary>
    /// The engine's supported payment representation is a single aggregate currency
    /// line: it carries no tender type. DECISION 2 requires the full ClubStack
    /// tender breakdown to be preserved untouched, so it is persisted alongside this
    /// document rather than being forced into it.
    /// </summary>
    public static string BuildCurrencies(string currencyCode, decimal total)
        => $"<CurrenciesReceived><Currency><Name>{Esc(NullIfBlank(currencyCode, "ZWL"))}</Name>"
           + $"<Amount>{Money(total)}</Amount><Rate>1</Rate></Currency></CurrenciesReceived>";

    public static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    public static string Rate(decimal value) => value.ToString("0.0000", CultureInfo.InvariantCulture);

    private static string NullIfBlank(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Length <= max ? value : value[..max];
    }

    private static string Esc(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            switch (c)
            {
                case '&': sb.Append("&amp;"); break;
                case '<': sb.Append("&lt;"); break;
                case '>': sb.Append("&gt;"); break;
                case '"': sb.Append("&quot;"); break;
                case '\'': sb.Append("&apos;"); break;
                default: sb.Append(c); break;
            }
        }

        return sb.ToString();
    }
}
