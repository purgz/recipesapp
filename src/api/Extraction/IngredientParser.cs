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
        @"^(?<qty>\d+\s+\d+\s*/\s*\d+|\d+\s*/\s*\d+|\d+\s*[½¼¾⅓⅔]|\d+[.,]\d+|\d+|[½¼¾⅓⅔])?\s*(?<unit>[a-zæøåA-ZÆØÅ]+\.?)?\s*(?<name>\S.*|)$",
        RegexOptions.ExplicitCapture)]
    private static partial Regex Line();

    /// <summary>"3½", "1 ½" — a whole number immediately followed by a unicode fraction.</summary>
    [GeneratedRegex(@"^(?<whole>\d+)\s*(?<frac>[½¼¾⅓⅔])$", RegexOptions.ExplicitCapture)]
    private static partial Regex MixedUnicodeFraction();

    [GeneratedRegex(@"^(?<lo>\d+)\s*[-–~]\s*\d+\s*", RegexOptions.ExplicitCapture)]
    private static partial Regex Range();

    /// <summary>"(180 g)", "(2.6 lb)", "(~4 pieces)" — parens that only hold numbers/units.</summary>
    [GeneratedRegex(@"\(\s*[~≈]?\s*[\d.,/½¼¾\s]+(?:g|kg|mg|ml|dl|cl|l|grams?|kilograms?|millilit(?:er|re)s?|oz|lbs?|fl\.?\s?oz|tsp|tbsp|cups?|pieces?|pcs?)\b\.?\s*\)", RegexOptions.IgnoreCase)]
    private static partial Regex ConversionParen();

    /// <summary>RecipeTin style "1.3 kg / 2.6 lb chuck beef" — imperial tail right after the unit.</summary>
    [GeneratedRegex(@"^\s*/\s*[\d.,]+\s*(?:lbs|lb|oz)\b\.?\s*", RegexOptions.IgnoreCase)]
    private static partial Regex LeadingImperial();

    /// <summary>"2.2 lb / 1 kg" swapped order, appearing inside the name.</summary>
    [GeneratedRegex(@"\(\s*/?\s*[\d.,]+\s*(?:lbs|lb|oz)\b\.?\s*\)", RegexOptions.IgnoreCase)]
    private static partial Regex InlineImperial();

    [GeneratedRegex(@"^\s*(\d+(?:[.,]\d+)?)", RegexOptions.ExplicitCapture)]
    private static partial Regex LeadingNumber();

    public static IngredientEntry Parse(string raw)
    {
        var text = raw.Trim().TrimStart('-', '•', '*', '·').Trim();

        // Ranges like "20 - 25 corn tortillas" or "2-3 gulerødder" → take the lower bound.
        var range = Range().Match(text);
        if (range.Success)
        {
            text = range.Groups["lo"].Value + " " + text[range.Length..].TrimStart();
        }

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
            return new IngredientEntry(qty, unitRaw.ToLowerInvariant(), CleanName(string.IsNullOrWhiteSpace(nameRaw) ? unitRaw : nameRaw));
        }

        // qty recognized but no known unit → the token belongs to the name
        return new IngredientEntry(qty, null, CleanName(m.Groups["unit"].Success ? $"{m.Groups["unit"].Value} {nameRaw}".Trim() : text));
    }

    /// <summary>
    /// Strips scrape noise common in (especially English) ingredient lines:
    /// parenthetical conversions "(180 g)", "(2.6 lb)", "(~4 pieces)",
    /// leading "/ 2.6 lb" conversion tails, and stray punctuation.
    /// Recipe instructions inside parentheses like "(skåret i tern)" survive.
    /// </summary>
    private static string CleanName(string name)
    {
        var s = ConversionParen().Replace(name, " ");
        s = LeadingImperial().Replace(s, " ");
        s = InlineImperial().Replace(s, " ");
        s = s.Replace(", ,", ",").Replace("(,", "(").Replace(", )", ")");
        s = System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ").Trim(' ', ',');
        s = System.Text.RegularExpressions.Regex.Replace(s, @"\s+([,.;:])", "$1");
        s = System.Text.RegularExpressions.Regex.Replace(s, @"\(\s*\)", " ");
        return s.Trim(' ', ',');
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
            return FractionValue(raw[0]);
        }

        // "3½" / "1 ½" — whole number + unicode fraction
        var mixedUnicode = MixedUnicodeFraction().Match(raw);
        if (mixedUnicode.Success &&
            double.TryParse(mixedUnicode.Groups["whole"].Value, out var mixedWhole))
        {
            return mixedWhole + FractionValue(mixedUnicode.Groups["frac"].Value[0]);
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

    private static double? FractionValue(char symbol) => symbol switch
    {
        '½' => 0.5,
        '¼' => 0.25,
        '¾' => 0.75,
        '⅓' => 1.0 / 3,
        '⅔' => 2.0 / 3,
        _ => null,
    };

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
