using System.Text.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace RecipeApp.Api.Extraction;

/// <summary>
/// Last-resort extractor for pages where neither JSON-LD nor microdata describe
/// the recipe. Sends the readable article text to Groq (via <see cref="GroqClient"/>)
/// and maps the strict JSON answer back into a RecipeDraft. Only active when
/// Groq:ApiKey is configured; ingredient lines are re-parsed locally so Danish
/// units ("tsk", "spsk") are never reinterpreted by the model.
/// </summary>
public partial class LlmRecipeExtractor(
    ScrapeFetcher fetcher,
    GroqClient groq,
    ILogger<LlmRecipeExtractor> log) : IRecipeExtractor
{
    public const string DraftWarning = "AI-udtrukket via Groq – kontrollér indholdet.";

    private const int MaxInputChars = 12_000;

    /// <summary>Runs after the deterministic chain has failed.</summary>
    public int Priority => 100;

    public bool CanHandle(string url) =>
        groq.Enabled &&
        Uri.TryCreate(url, UriKind.Absolute, out var u) &&
        (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps);

    public async Task<RecipeDraft> ExtractAsync(string url, CancellationToken ct)
    {
        var draft = new RecipeDraft { SourceUrl = url };

        var fetched = await fetcher.FetchAsync(url, ct);
        if (fetched.Html is null)
        {
            draft.Warnings.Add(fetched.Error!);
            return draft;
        }

        var text = ReadableText(fetched.Html);
        if (text.Length < 200)
        {
            draft.Warnings.Add("Siden havde for lidt læsbar tekst til AI-udtræk.");
            return draft;
        }

        string content;
        try
        {
            content = await groq.CompleteJsonAsync(SystemPrompt, text, Schema, ct);
        }
        catch (GroqException ex)
        {
            // Messages from GroqClient are already user-facing (incl. quota
            // resets), so show them verbatim in the import form.
            draft.Warnings.Add($"AI-udtræk fejlede: {ex.Message}");
            return draft;
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Groq extraction failed for {Url}", url);
            draft.Warnings.Add("AI-udtræk fejlede – prøv igen senere.");
            return draft;
        }

        try
        {
            MapContent(draft, content);
        }
        catch (JsonException ex)
        {
            log.LogWarning(ex, "Groq returned malformed JSON for {Url}", url);
            draft.Warnings.Add("AI-svaret kunne ikke læses – udfyld manuelt.");
            return draft;
        }

        if (draft.Ingredients.Count == 0 && draft.Steps.Count == 0)
        {
            draft.Warnings.Add("AI-udtræk gav hverken ingredienser eller trin. Udfyld manuelt.");
            return draft;
        }

        draft.Warnings.Add(DraftWarning);
        return draft;
    }

    private static readonly JsonElement Schema = JsonDocument.Parse("""
        {
          "type": "object",
          "properties": {
            "title":          { "type": ["string", "null"] },
            "description":    { "type": ["string", "null"] },
            "servings":       { "type": ["integer", "null"] },
            "prepMinutes":    { "type": ["integer", "null"] },
            "cookMinutes":    { "type": ["integer", "null"] },
            "ingredients":    { "type": "array", "items": { "type": "string" } },
            "steps":          { "type": "array", "items": { "type": "string" } },
            "tags":           { "type": "array", "items": { "type": "string" } }
          },
          "required": ["title", "description", "servings", "prepMinutes", "cookMinutes", "ingredients", "steps", "tags"],
          "additionalProperties": false
        }
        """).RootElement.Clone();

    private const string SystemPrompt =
        """
        You extract recipes from web page text into JSON.
        Rules:
        - Return ONLY JSON matching the schema, no markdown.
        - Copy ingredient lines and steps VERBATIM from the page. Never translate.
        - Keep Danish units and words exactly as written (tsk, spsk, g, båd, ...).
        - Use null for unknown numbers/fields; use [] for empty lists.
        - Tags: use the page's own categories/keywords (dessert, aftensmad, dinner, kage, ...).
        - Do not invent ingredients, quantities or steps.
        """;

    private static void MapContent(RecipeDraft draft, string content)
    {
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        draft.Title ??= Str(root, "title");
        draft.Description ??= Str(root, "description");
        draft.Servings ??= Int(root, "servings");
        draft.PrepMinutes ??= Int(root, "prepMinutes");
        draft.CookMinutes ??= Int(root, "cookMinutes");

        foreach (var line in Strings(root, "ingredients"))
        {
            var raw = line.Trim();
            if (raw.Length > 0)
            {
                draft.Ingredients.Add(IngredientParser.Parse(raw));
            }
        }

        foreach (var step in Strings(root, "steps"))
        {
            var clean = step.Trim();
            if (clean.Length > 0)
            {
                draft.Steps.Add(clean);
            }
        }

        draft.Tags = TagNormalizer.Normalize(Strings(root, "tags"));
        if (draft.Servings is <= 0)
        {
            draft.Servings = null;
        }
    }

    private static string? Str(JsonElement root, string prop) =>
        root.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    private static int? Int(JsonElement root, string prop) =>
        root.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i)
            ? i
            : null;

    private static IEnumerable<string> Strings(JsonElement root, string prop)
    {
        if (!root.TryGetProperty(prop, out var v) || v.ValueKind != JsonValueKind.Array)
        {
            return [];
        }
        return v.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString()!);
    }

    private static string ReadableText(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var junk = doc.DocumentNode.SelectNodes(
            "//script|//style|//noscript|//svg|//iframe|//nav|//header|//footer|//aside|//form");
        if (junk is not null)
        {
            foreach (var node in junk)
            {
                node.Remove();
            }
        }

        var root = doc.DocumentNode.SelectSingleNode("//article")
                   ?? doc.DocumentNode.SelectSingleNode("//main")
                   ?? doc.DocumentNode.SelectSingleNode("//body");

        var text = HtmlEntity.DeEntitize(root?.InnerText ?? string.Empty);
        text = Whitespace().Replace(text.Replace('\u00A0', ' '), " ").Trim();
        return text.Length > MaxInputChars ? text[..MaxInputChars] : text;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
