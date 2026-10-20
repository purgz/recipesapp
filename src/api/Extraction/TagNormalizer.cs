namespace RecipeApp.Api.Extraction;

/// <summary>
/// Turns raw schema.org category/keyword values into a small, consistent tag
/// set. Meal-type categories are mapped to canonical tags (dessert, dinner,
/// breakfast, lunch, snack, side dish) so they can be filtered on; every other
/// keyword is kept lowercased and trimmed. Both JSON-LD and microdata go
/// through here so imports from either path look the same.
/// </summary>
public static class TagNormalizer
{
    public const int MaxTags = 8;
    public const int MaxTagLength = 40;

    private static readonly Dictionary<string, string> Categories = new(StringComparer.OrdinalIgnoreCase)
    {
        // dessert
        ["dessert"] = "dessert",
        ["desserts"] = "dessert",
        ["desserter"] = "dessert",
        ["sweet"] = "dessert",
        ["sweets"] = "dessert",
        ["kage"] = "dessert",
        ["kager"] = "dessert",
        ["bagværk"] = "dessert",
        ["pandekager"] = "dessert",
        ["ice cream"] = "dessert",
        ["is"] = "dessert",

        // dinner
        ["dinner"] = "dinner",
        ["aftensmad"] = "dinner",
        ["hovedret"] = "dinner",
        ["hovedretter"] = "dinner",
        ["main course"] = "dinner",
        ["main dish"] = "dinner",
        ["middag"] = "dinner",
        ["supper"] = "dinner",

        // breakfast
        ["breakfast"] = "breakfast",
        ["morgenmad"] = "breakfast",
        ["brunch"] = "breakfast",

        // lunch
        ["lunch"] = "lunch",
        ["frokost"] = "lunch",

        // snack
        ["snack"] = "snack",
        ["snacks"] = "snack",
        ["mellemmåltid"] = "snack",
        ["mellemmåltider"] = "snack",

        // side
        ["side dish"] = "side dish",
        ["tilbehør"] = "side dish",
    };

    public static List<string> Normalize(IEnumerable<string> rawValues)
    {
        var tags = new List<string>();
        foreach (var value in rawValues)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            foreach (var part in value.Split([',', ';', '/', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var tag = Canonical(part);
                if (tag is not null && !tags.Contains(tag, StringComparer.Ordinal))
                {
                    tags.Add(tag);
                    if (tags.Count == MaxTags)
                    {
                        return tags;
                    }
                }
            }
        }
        return tags;
    }

    private static string? Canonical(string raw)
    {
        var text = raw.Trim().ToLowerInvariant();
        if (text.Length is < 2 or > MaxTagLength)
        {
            return null;
        }
        return Categories.TryGetValue(text, out var canonical) ? canonical : text;
    }
}
