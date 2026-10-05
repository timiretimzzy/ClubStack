using System.Xml.Linq;
using ClubStack.Hardware.Fiscalization;
using ClubStack.Infrastructure.Fiscalization;

namespace ClubStack.Application.Tests;

/// <summary>
/// The engine document mapping is a pure function of ClubStack's own calculated
/// amounts, so it is verified here without a device, database, or network.
/// </summary>
public sealed class FiscalisationXmlBuilderTests
{
    private static FiscalLine Line(
        string name1,
        decimal quantity,
        decimal price,
        decimal amount,
        decimal tax,
        decimal taxRate,
        int taxId,
        string? name2 = null,
        string itemCode = "") =>
        new()
        {
            ItemCode = itemCode,
            Name1 = name1,
            Name2 = name2,
            Quantity = quantity,
            Price = price,
            Amount = amount,
            Tax = tax,
            TaxRate = taxRate,
            TaxId = taxId,
        };

    [Fact]
    public void BuildItems_emits_one_item_element_per_line_in_order()
    {
        var xml = FiscalisationXmlBuilder.BuildItems(
        [
            Line("First", 1m, 10m, 10m, 1.5m, 0.15m, 515),
            Line("Second", 2m, 5m, 10m, 1.5m, 0.15m, 515),
        ]);

        var items = XElement.Parse(xml).Elements("ITEM").ToArray();

        Assert.Equal(2, items.Length);
        Assert.Equal("1", items[0].Element("HH")!.Value);
        Assert.Equal("2", items[1].Element("HH")!.Value);
    }

    [Fact]
    public void BuildItems_writes_money_with_two_decimals_and_rate_with_four()
    {
        var xml = FiscalisationXmlBuilder.BuildItems(
            [Line("Widget", 1.5m, 10m, 15m, 2.25m, 0.15m, 515)]);

        var item = XElement.Parse(xml).Element("ITEM")!;

        Assert.Equal("1.50", item.Element("QTY")!.Value);
        Assert.Equal("10.00", item.Element("PRICE")!.Value);
        Assert.Equal("15.00", item.Element("AMT")!.Value);
        Assert.Equal("2.25", item.Element("TAX")!.Value);
        Assert.Equal("0.1500", item.Element("TAXR")!.Value);
    }

    [Fact]
    public void BuildItems_uses_invariant_culture_regardless_of_the_host_locale()
    {
        var original = Thread.CurrentThread.CurrentCulture;
        try
        {
            // A culture that uses ',' as the decimal separator would corrupt the payload
            // if the builder relied on the ambient culture.
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");

            var xml = FiscalisationXmlBuilder.BuildItems(
                [Line("Widget", 1m, 10.5m, 10.5m, 1.58m, 0.15m, 515)]);

            var item = XElement.Parse(xml).Element("ITEM")!;
            Assert.Equal("10.50", item.Element("AMT")!.Value);
            Assert.Equal("1.58", item.Element("TAX")!.Value);
            Assert.Equal("0.1500", item.Element("TAXR")!.Value);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = original;
        }
    }

    [Fact]
    public void BuildItems_carries_the_resolved_tax_identifier_verbatim()
    {
        var xml = FiscalisationXmlBuilder.BuildItems(
            [Line("Widget", 1m, 10m, 10m, 0m, 0m, 1)]);

        Assert.Equal("1", XElement.Parse(xml).Element("ITEM")!.Element("TAXID")!.Value);
    }

    [Fact]
    public void BuildItems_falls_back_to_a_stable_hs_code_when_the_product_has_none()
    {
        var xml = FiscalisationXmlBuilder.BuildItems(
            [Line("Widget", 1m, 10m, 10m, 0m, 0m, 1, itemCode: "   ")]);

        Assert.Equal(
            FiscalisationXmlBuilder.DefaultHsCode,
            XElement.Parse(xml).Element("ITEM")!.Element("ITEMCODE")!.Value);
    }

    [Fact]
    public void BuildItems_keeps_an_explicit_hs_code()
    {
        var xml = FiscalisationXmlBuilder.BuildItems(
            [Line("Widget", 1m, 10m, 10m, 0m, 0m, 1, itemCode: "62010000")]);

        Assert.Equal("62010000", XElement.Parse(xml).Element("ITEM")!.Element("ITEMCODE")!.Value);
    }

    [Fact]
    public void BuildItems_repeats_name1_as_name2_when_no_description_is_supplied()
    {
        var xml = FiscalisationXmlBuilder.BuildItems(
            [Line("Membership", 1m, 10m, 10m, 0m, 0m, 1)]);

        var item = XElement.Parse(xml).Element("ITEM")!;
        Assert.Equal("Membership", item.Element("ITEMNAME1")!.Value);
        Assert.Equal("Membership", item.Element("ITEMNAME2")!.Value);
    }

    [Fact]
    public void BuildItems_escapes_characters_that_would_break_the_payload()
    {
        var xml = FiscalisationXmlBuilder.BuildItems(
            [Line("A&B <Ltd> \"quoted\" 'single'", 1m, 10m, 10m, 0m, 0m, 1)]);

        // Parsing proves the document is well formed rather than merely string-matching.
        var name = XElement.Parse(xml).Element("ITEM")!.Element("ITEMNAME1")!.Value;
        Assert.Equal("A&B <Ltd> \"quoted\" 'single'", name);
    }

    [Fact]
    public void BuildItems_truncates_names_to_the_engine_limit()
    {
        var longName = new string('x', 80);

        var xml = FiscalisationXmlBuilder.BuildItems(
            [Line(longName, 1m, 10m, 10m, 0m, 0m, 1)]);

        var item = XElement.Parse(xml).Element("ITEM")!;
        Assert.Equal(60, item.Element("ITEMNAME1")!.Value.Length);
        Assert.Equal(60, item.Element("ITEMNAME2")!.Value.Length);
    }

    [Fact]
    public void BuildItems_on_an_empty_document_still_produces_a_well_formed_container()
    {
        var xml = FiscalisationXmlBuilder.BuildItems([]);

        var root = XElement.Parse(xml);
        Assert.Equal("ITEMS", root.Name.LocalName);
        Assert.Empty(root.Elements("ITEM"));
    }

    [Fact]
    public void BuildCurrencies_emits_a_single_aggregate_currency_line()
    {
        var xml = XElement.Parse(FiscalisationXmlBuilder.BuildCurrencies("ZWL", 115m));

        var currency = xml.Element("Currency")!;
        Assert.Equal("CurrenciesReceived", xml.Name.LocalName);
        Assert.Equal("ZWL", currency.Element("Name")!.Value);
        Assert.Equal("115.00", currency.Element("Amount")!.Value);
        Assert.Equal("1", currency.Element("Rate")!.Value);
    }

    [Fact]
    public void BuildCurrencies_defaults_to_zwl_when_no_currency_is_known()
    {
        var xml = XElement.Parse(FiscalisationXmlBuilder.BuildCurrencies("  ", 10m));

        Assert.Equal("ZWL", xml.Element("Currency")!.Element("Name")!.Value);
    }

    [Fact]
    public void BuildCurrencies_preserves_the_grand_total_clusterstack_already_calculated()
    {
        var xml = XElement.Parse(FiscalisationXmlBuilder.BuildCurrencies("ZWL", 1000.005m));

        // Two-decimal money formatting, matching how ClubStack stores totals.
        Assert.Equal("1000.01", xml.Element("Currency")!.Element("Amount")!.Value);
    }
}
