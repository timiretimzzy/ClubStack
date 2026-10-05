using System.Globalization;
using System.Xml.Linq;
using ClubStack.Hardware.Fiscalization;
using Newtonsoft.Json.Linq;

namespace ClubStack.Infrastructure.Fiscalization;

/// <summary>
/// Result of invoking the legacy FiscalStackZW facade. Every facade method returns a
/// JSON envelope string: <c>{"Code":"1"|"0","Message":"...","Data":"&lt;payload&gt;"}</c>,
/// where <c>Data</c> is itself a JSON string containing XML or JSON.
/// </summary>
internal sealed record FiscalStackEnvelope(string Code, string Message, string Data)
{
    public bool Ok => string.Equals(Code, "1", StringComparison.Ordinal);

    public static FiscalStackEnvelope Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new FiscalisationException("FiscalStackZW returned an empty response.");
        }

        try
        {
            var json = JObject.Parse(raw);
            return new FiscalStackEnvelope(
                json.Value<string>("Code") ?? "0",
                json.Value<string>("Message") ?? string.Empty,
                json.Value<string>("Data") ?? string.Empty);
        }
        catch (Exception ex) when (ex is not FiscalisationException)
        {
            throw new FiscalisationException($"FiscalStackZW returned an unparseable response: {ex.Message}", ex);
        }
    }
}

internal static class FiscalStackPayload
{
    public static XDocument Xml(FiscalStackEnvelope envelope)
    {
        if (string.IsNullOrWhiteSpace(envelope.Data))
        {
            throw new FiscalisationException($"FiscalStackZW returned no XML payload: {envelope.Message}");
        }

        try
        {
            return XDocument.Parse(envelope.Data);
        }
        catch (Exception ex)
        {
            throw new FiscalisationException($"FiscalStackZW returned malformed XML: {ex.Message}", ex);
        }
    }

    public static JObject Json(FiscalStackEnvelope envelope)
    {
        if (string.IsNullOrWhiteSpace(envelope.Data))
        {
            throw new FiscalisationException($"FiscalStackZW returned no JSON payload: {envelope.Message}");
        }

        try
        {
            return JObject.Parse(envelope.Data);
        }
        catch (Exception ex)
        {
            throw new FiscalisationException($"FiscalStackZW returned malformed JSON: {ex.Message}", ex);
        }
    }

    public static string? Value(XDocument doc, string name) => Value(doc.Root, name);

    public static string? Value(XElement? parent, string name)
    {
        var element = parent?.Element(name);
        if (element is null)
        {
            return null;
        }

        var value = element.Value;
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public static bool Flag(XDocument doc, string name) => bool.TryParse(Value(doc, name), out var flag) && flag;

    public static int? Number(XDocument doc, string name)
        => int.TryParse(Value(doc, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;

    public static decimal? Decimal(XDocument doc, string name) => DecimalOf(Value(doc, name));

    public static decimal? Decimal(XElement parent, string name) => DecimalOf(Value(parent, name));

    private static decimal? DecimalOf(string? raw)
        => decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? value : null;
}

public sealed class FiscalisationException : Exception
{
    public FiscalisationException(string message) : base(message)
    {
    }

    public FiscalisationException(string message, Exception inner) : base(message, inner)
    {
    }
}
