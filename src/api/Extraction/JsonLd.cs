using System.Text.Json;
using System.Text.RegularExpressions;

namespace RecipeApp.Api.Extraction;

public static class JsonLd
{
    public static List<JsonElement> FindRecipeNodes(string html)
    {
        var result = new List<JsonElement>();
        var matches = Regex.Matches(
            html,
            "<script[^>]*type\\s*=\\s*[\"']application/ld\\+json[\"'][^>]*>(.*?)</script>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        foreach (Match m in matches)
        {
            JsonElement root;
            try
            {
                using var doc = JsonDocument.Parse(m.Groups[1].Value.Trim());
                root = doc.RootElement.Clone();
            }
            catch (JsonException)
            {
                continue;
            }

            Collect(root, result);
        }

        return result;
    }

    private static void Collect(JsonElement el, List<JsonElement> acc)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
                if (IsType(el, "Recipe"))
                {
                    acc.Add(el);
                    return;
                }
                if (el.TryGetProperty("@graph", out var graph) && graph.ValueKind == JsonValueKind.Array)
                {
                    foreach (var g in graph.EnumerateArray())
                    {
                        Collect(g, acc);
                    }
                }
                return;
            case JsonValueKind.Array:
                foreach (var item in el.EnumerateArray())
                {
                    Collect(item, acc);
                }
                return;
        }
    }

    private static bool IsType(JsonElement el, string expected)
    {
        if (!el.TryGetProperty("@type", out var t))
        {
            return false;
        }
        if (t.ValueKind == JsonValueKind.String)
        {
            return string.Equals(t.GetString(), expected, StringComparison.OrdinalIgnoreCase);
        }
        if (t.ValueKind == JsonValueKind.Array)
        {
            return t.EnumerateArray().Any(x =>
                x.ValueKind == JsonValueKind.String &&
                string.Equals(x.GetString(), expected, StringComparison.OrdinalIgnoreCase));
        }
        return false;
    }

    public static string? Str(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) &&
        v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    /// <summary>Returns the property as a homogeneous object list; halves string scalars into single-item lists.</summary>
    public static List<JsonElement> Values(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var v))
        {
            return [];
        }
        return v.ValueKind switch
        {
            JsonValueKind.Array => v.EnumerateArray().ToList(),
            JsonValueKind.Object or JsonValueKind.String or JsonValueKind.Number => [v],
            _ => [],
        };
    }

    /// <summary>schema.org image can be a string, an object with a url, or arrays of either — take the first url.</summary>
    public static string? FirstImage(JsonElement el)
    {
        foreach (var v in Values(el, "image"))
        {
            var url = v.ValueKind == JsonValueKind.String
                ? v.GetString()
                : v.ValueKind == JsonValueKind.Object ? (Str(v, "url") ?? Str(v, "contentUrl")) : null;
            if (!string.IsNullOrWhiteSpace(url))
            {
                return url;
            }
        }
        return null;
    }
}
