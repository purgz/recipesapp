using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.Extensions.Options;

namespace RecipeApp.Api.Extraction;

/// <summary>
/// Last-resort extractor for pages where neither JSON-LD nor microdata describe
/// the recipe. Sends the readable article text to Groq and maps the strict JSON
/// answer back into a RecipeDraft. Only active when Groq:ApiKey is configured;
/// ingredient lines are re-parsed locally so Danish units ("tsk", "spsk") are
/// never reinterpreted by the model.
/// </summary>
public partial class LlmRecipeExtractor(
    ScrapeFetcher fetcher,
    HttpClient http,
    IOptions<GroqOptions> options,
    ILogger<LlmRecipeExtractor> log) : IRecipeExtractor
{
    public const string DraftWarning = "AI-udtrukket via Groq – kontrollér indholdet.";

    private const int MaxInputChars = 16_000;
    private const int MaxOutputTokens = 1200;

    private static readonly Lock AttemptsLock = new();
    private static readonly Queue<DateTimeOffset> Attempts = new();

    private GroqOptions Options => options.Value;

    /// <summary>Runs after the deterministic chain has failed.</summary>
    public int Priority => 100;

    public bool CanHandle(string url) =>
        Options.Enabled &&
        Uri.TryCreate(url, UriKind.Absolute, out var u) &&
        (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps);

    public async Task<RecipeDraft> ExtractAsync(string url, CancellationToken ct)
    {
        var draft = new RecipeDraft { SourceUrl = url };

        if (!TryReserveAttempt())
        {
            draft.Warnings.Add($"AI-fallback er sat på pause (max {Options.MaxAttemptsPerHour}/time). Udfyld manuelt.");
            return draft;
        }

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
            content = await CallGroqAsync(text, ct);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            draft.Warnings.Add($"AI-udtræk tog for lang tid (>{Options.TimeoutSeconds}s). Udfyld manuelt.");
            return draft;
        }
        catch (HttpRequestException ex)
        {
            log.LogWarning(ex, "Groq extraction failed for {Url}", url);
            // Surface the provider's reason in the form; the key is never included.
            draft.Warnings.Add($"AI-udtræk fejlede: {Truncate(ex.Message, 240)}");
            return draft;
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Groq extraction failed for {Url}", url);
            draft.Warnings.Add($"AI-udtræk fejlede ({ex.GetType().Name}): {Truncate(ex.Message, 200)}");
            return draft;
        }

        MapContent(draft, content);

        if (draft.Ingredients.Count == 0 && draft.Steps.Count == 0)
        {
            draft.Warnings.Add("AI-udtræk gav hverken ingredienser eller trin. Udfyld manuelt.");
            return draft;
        }

        draft.Warnings.Add(DraftWarning);
        return draft;
    }

    private async Task<string> CallGroqAsync(string text, CancellationToken ct)
    {
        var tokenParam = "max_tokens";
        var response = await PostAsync(text, structured: true, tokenParam, ct);

        // Groq rejects `max_tokens` for some reasoning models; retry with the
        // newer name instead of failing outright.
        if (await RejectsAsync(response, "max_tokens"))
        {
            response.Dispose();
            tokenParam = "max_completion_tokens";
            log.LogInformation("Groq rejected max_tokens; retrying with {Param}", tokenParam);
            response = await PostAsync(text, structured: true, tokenParam, ct);
        }

        if (await RejectsAsync(response, "response_format", "json_schema"))
        {
            response.Dispose();
            log.LogInformation("Groq model {Model} rejected json_schema; retrying with json_object", Options.ModelValue);
            response = await PostAsync(text, structured: false, tokenParam, ct);
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                var hint = response.StatusCode == HttpStatusCode.TooManyRequests ? " (rate limit)" : "";
                throw new HttpRequestException(
                    $"Groq svarede {(int)response.StatusCode}{hint}: {Truncate(body, 300)}");
            }

            using var doc = JsonDocument.Parse(body);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (doc.RootElement.TryGetProperty("usage", out var usage))
            {
                log.LogInformation("Groq tokens: {Usage}", usage.ToString());
            }

            return StripFences(content ?? string.Empty);
        }
    }

    private Task<HttpResponseMessage> PostAsync(string text, bool structured, string tokenParam, CancellationToken ct)
    {
        var payload = new Dictionary<string, object?>
        {
            ["model"] = Options.ModelValue,
            ["temperature"] = 0,
            [tokenParam] = MaxOutputTokens,
            ["messages"] = new object[]
            {
                new { role = "system", content = SystemPrompt },
                new { role = "user", content = text },
            },
        };

        payload["response_format"] = structured
            ? new
            {
                type = "json_schema",
                json_schema = new
                {
                    name = "recipe",
                    strict = true,
                    schema = Schema,
                },
            }
            : new { type = "json_object" };

        var json = JsonSerializer.Serialize(payload);
        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        return http.SendAsync(request, ct);
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

    private static async Task<bool> RejectsAsync(HttpResponseMessage response, params string[] needles)
    {
        if ((int)response.StatusCode != 400)
        {
            return false;
        }
        var body = await response.Content.ReadAsStringAsync();
        return needles.Any(n => body.Contains(n, StringComparison.OrdinalIgnoreCase));
    }

    private bool TryReserveAttempt()
    {
        lock (AttemptsLock)
        {
            var cutoff = DateTimeOffset.UtcNow.AddHours(-1);
            while (Attempts.Count > 0 && Attempts.Peek() < cutoff)
            {
                Attempts.Dequeue();
            }
            if (Attempts.Count >= Options.MaxAttemptsPerHour)
            {
                return false;
            }
            Attempts.Enqueue(DateTimeOffset.UtcNow);
            return true;
        }
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

    private static string StripFences(string content)
    {
        var trimmed = content.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }
        var firstLine = trimmed.IndexOf('\n');
        var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        return firstLine >= 0 && lastFence > firstLine
            ? trimmed[(firstLine + 1)..lastFence].Trim()
            : trimmed;
    }

    private static string Truncate(string value, int length) =>
        value.Length <= length ? value : value[..length] + "…";

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
