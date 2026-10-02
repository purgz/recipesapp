using System.Text.RegularExpressions;
using RecipeApp.Api.Data;

namespace RecipeApp.Api.Extraction;

/// <summary>
/// Splits a raw scraped ingredient line into {qty, unit, name}.
/// Danish units survive verbatim — never translate or convert them here.
/// Best effort: anything that doesn't parse goes into Name untouched.
/// </summary>
public static partial class IngredientParser
{
    private static readonly HashSet<string> Units =
    [
        "g", "kg", "mg", "ml", "cl", "dl", "l", "tsk", "spsk", "stk", "stykker",
        "bakke", "bakker", "bundt", "bundter", "dåse", "dåser", "båd", "både",
        "pakke", "pakker", "pose", "poser", "flaske", "flasker", "glas",
        "knivspids", "skive", "skiver", "fed", "fedd", "potte", "pletter",
        "håndfuld", "tsp", "tbsp", "cup", "cups", "oz", "lb",
        "liter", "gram", "kvist", "kviste", "stilk", "stilke", "blade",
    ];

    [GeneratedRegex(
        @"^(?<qty>\d+[.,]\d+|\d+\s+\d+\s*/\s*\d+|\d+\s*/\s*\d+|[½¼¾⅓⅔])?\s*(?<unit>[a-zæøåA-ZÆØÅ]+\.?)?\s*(?<name>\S.*|)$",
        RegexOptions.ExplicitCapture)]
    private static partial Regex Line();

    [GeneratedRegex(@"^\s*(\d+(?:[.,]\d+)?)", RegexOptions.ExplicitCapture)]
    private static partial Regex LeadingNumber();

    public static IngredientEntry Parse(string raw)
    {
        var text = raw.Trim().TrimStart('-', '•', '*', '·').Trim();

        var m = Line().Match(text);
        if (!m.Success)
        {
            return new IngredientEntry(null, null, text);
        }

        var qty = ParseQty(m.Groups["qty"].Value);
        var unitRaw = m.Groups["unit"].Value.TrimEnd('.');
        var nameRaw = m.Groups["name"].Value.Trim();

        // Only treat the middle token as a unit if we actually recognized it
        // (e.g. "2 store æbler": "store" is not a unit, so qty must stay + name grows).
        if (qty is not null && Units.Contains(unitRaw.ToLowerInvariant()))
        {
            return new IngredientEntry(qty, unitRaw.ToLowerInvariant(), string.IsNullOrWhiteSpace(nameRaw) ? unitRaw : nameRaw);
        }

        // qty recognized but no known unit → the token belongs to the name
        return new IngredientEntry(qty, null, m.Groups["unit"].Success ? $"{m.Groups["unit"].Value} {nameRaw}".Trim() : text);
    }

    private static double? ParseQty(string raw)
    {
        raw = raw.Trim();
        if (raw.Length == 0)
        {
            return null;
        }

        // Unicode fractions
        if (raw.Length == 1 && "½¼¾⅓⅔".Contains(raw, StringComparison.Ordinal))
        {
            return raw switch
            {
                "½" => 0.5,
                "¼" => 0.25,
                "¾" => 0.75,
                "⅓" => 1.0 / 3,
                "⅔" => 2.0 / 3,
                _ => null,
            };
        }

        // "1 1/2", "1 1/2" mixed number
        var mixedParts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (mixedParts.Length == 2 &&
            double.TryParse(mixedParts[0], out var whole) &&
            mixedParts[1].Contains('/'))
        {
            if (TryParseFraction(mixedParts[1], out var frac))
            {
                return whole + frac;
            }
        }

        if (raw.Contains('/') && TryParseFraction(raw, out var f))
        {
            return f;
        }

        if (double.TryParse(raw.Replace(',', '.'), out var d))
        {
            return d;
        }

        return null;
    }

    private static bool TryParseFraction(string raw, out double value)
    {
        value = 0;
        var parts = raw.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 ||
            !double.TryParse(parts[0].Trim(), out var num) ||
            !double.TryParse(parts[1].Trim(), out var den) ||
            den == 0)
        {
            return false;
        }

        value = num / den;
        return true;
    }

    /// <summary>dto-friendly shareable parse for scraping/paste flows.</summary>
    public static IngredientEntry? FirstNumber(string raw)
    {
        var m = LeadingNumber().Match(raw);
        return m.Success
            ? new IngredientEntry(double.Parse(m.Groups[1].Value.Replace(',', '.')), null, raw.Trim())
            : null;
    }
}
