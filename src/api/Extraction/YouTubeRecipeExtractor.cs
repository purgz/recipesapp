using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RecipeApp.Api.Extraction;

/// <summary>
/// YouTube-only v1: oEmbed for title/channel/thumbnail (no API key, no quota)
/// plus best-effort scrape of the watch page for description and captions.
/// Anything that resists parsing lands in RawText for manual structuring —
/// never in fake structured steps.
/// </summary>
public partial class YouTubeRecipeExtractor(IHttpClientFactory httpFactory) : IRecipeExtractor
{
    public int Priority => 10;

    public bool CanHandle(string url) => TryGetVideoId(url) is not null;

    public async Task<RecipeDraft> ExtractAsync(string url, CancellationToken ct)
    {
        var videoId = TryGetVideoId(url)!;
        var draft = new RecipeDraft { SourceUrl = $"https://www.youtube.com/watch?v={videoId}" };

        var client = httpFactory.CreateClient("scrape");

        // 1) oEmbed metadata — most reliable part
        try
        {
            var oembedUrl =
                $"https://www.youtube.com/oembed?format=json&url={Uri.EscapeDataString($"https://www.youtube.com/watch?v={videoId}")}";
            using var resp = await client.GetAsync(oembedUrl, ct);
            if (resp.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
                draft.Title ??= doc.RootElement.TryGetProperty("title", out var t) ? t.GetString() : null;
                var thumb = doc.RootElement.TryGetProperty("thumbnail_url", out var th) ? th.GetString() : null;
                draft.ImageUrl ??= thumb;
            }
            else
            {
                draft.Warnings.Add($"oEmbed svarede {(int)resp.StatusCode}.");
            }
        }
        catch (HttpRequestException ex)
        {
            draft.Warnings.Add($"oEmbed fejlede: {ex.Message}");
        }

        // 2) Watch page: description + captions (bot walls may block — best effort)
        try
        {
            var watchUrl = $"https://www.youtube.com/watch?v={videoId}&hl=da&bpctr=9999999999&has_verified=1";
            using var req = new HttpRequestMessage(HttpMethod.Get, watchUrl);
            req.Headers.TryAddWithoutValidation("Cookie", "CONSENT=YES+cb; SOCS=CAI");
            using var resp = await client.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode)
            {
                var html = await resp.Content.ReadAsStringAsync(ct);
                var player = ExtractAssignment(html, "ytInitialPlayerResponse");
                if (player is not null)
                {
                    using var doc = JsonDocument.Parse(player);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("videoDetails", out var vd))
                    {
                        draft.Description ??= JsonLd.Str(vd, "shortDescription");
                        if (draft.ImageUrl is null && JsonLd.Str(vd, "thumbnailUrl") is { } tu)
                        {
                            draft.ImageUrl = $"https://i.ytimg.com/vi/{videoId}/maxresdefault.jpg";
                        }
                    }

                    draft.RawTextKind = "transcript";
                    draft.RawText = await FetchTranscript(root, client, ct);
                    if (draft.RawText is null && draft.Description is { Length: > 100 })
                    {
                        draft.RawTextKind = "description";
                        draft.RawText = draft.Description;
                    }
                }
                else
                {
                    draft.Warnings.Add("Kunne ikke læse afspillerdata — indsæt ingredienser og trin manuelt.");
                }
            }
            else
            {
                draft.Warnings.Add($"YouTube-siden svarede {(int)resp.StatusCode}.");
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            draft.Warnings.Add($"YouTube-udtrækning fejlede: {ex.Message}). Sørg for at støtte manuel formatering.");
        }

        if (draft.ImageUrl is null)
        {
            draft.ImageUrl = $"https://i.ytimg.com/vi/{videoId}/hqdefault.jpg";
        }
        if (draft.RawText is null)
        {
            draft.Warnings.Add("Ingen undertekster tilgængelige — strukturér opskriften manuelt.");
        }
        return draft;
    }

    private static async Task<string?> FetchTranscript(JsonElement player, HttpClient client, CancellationToken ct)
    {
        if (!player.TryGetProperty("captions", out var caps) ||
            !caps.TryGetProperty("playerCaptionsTracklistRenderer", out var list))
        {
            return null;
        }
        if (!list.TryGetProperty("captionTracks", out var tracks) || tracks.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        JsonElement? pick = null;
        foreach (var pref in new[] { "da", "en" })
        {
            foreach (var t in tracks.EnumerateArray())
            {
                var lang = JsonLd.Str(t, "languageCode");
                if (lang == pref)
                {
                    pick = t;
                    break;
                }
            }
            if (pick is not null)
            {
                break;
            }
        }
        pick ??= tracks.EnumerateArray().FirstOrDefault();

        var baseUrl = pick is { } track ? JsonLd.Str(track, "baseUrl") : null;
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return null;
        }

        using var resp = await client.GetAsync(baseUrl + "&fmt=json3", ct);
        if (!resp.IsSuccessStatusCode)
        {
            return null;
        }

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        if (!doc.RootElement.TryGetProperty("events", out var events) || events.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var sb = new StringBuilder();
        foreach (var ev in events.EnumerateArray())
        {
            if (!ev.TryGetProperty("segs", out var segs) || segs.ValueKind != JsonValueKind.Array)
            {
                continue;
            }
            var line = new StringBuilder();
            foreach (var seg in segs.EnumerateArray())
            {
                line.Append(JsonLd.Str(seg, "utf8"));
            }
            if (line.Length > 0)
            {
                if (sb.Length > 0)
                {
                    sb.AppendLine();
                }
                sb.Append(line);
            }
        }
        return sb.Length == 0 ? null : sb.ToString();
    }

    /// <summary>Finds <c>varName = { ... }</c> with brace matching (string/escape aware).</summary>
    private static string? ExtractAssignment(string html, string varName)
    {
        var idx = Regex.Match(html, Regex.Escape(varName) + @"\s*=\s*\{").Index;
        if (idx == 0 && !html.Contains(varName))
        {
            return null;
        }
        var start = Regex.Match(html[(idx..)], Regex.Escape(varName) + @"\s*=\s*\{");
        if (!start.Success)
        {
            return null;
        }
        var braceStart = idx + start.Index + start.Value.LastIndexOf('{');

        var depth = 0;
        var inString = false;
        var escaped = false;
        for (var i = braceStart; i < html.Length; i++)
        {
            var c = html[i];
            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (c == '\\')
                {
                    escaped = true;
                }
                else if (c == '"')
                {
                    inString = false;
                }
                continue;
            }
            switch (c)
            {
                case '"': inString = true; break;
                case '{': depth++; break;
                case '}':
                    if (--depth == 0)
                    {
                        return html[braceStart..(i + 1)];
                    }
                    break;
            }
        }
        return null;
    }

    private static string? TryGetVideoId(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u) ||
            u.Host is not { } host ||
            !(host.Contains("youtube.com", StringComparison.OrdinalIgnoreCase) ||
              host.Contains("youtu.be", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        if (host.Contains("youtu.be", StringComparison.OrdinalIgnoreCase))
        {
            var seg = u.AbsolutePath.TrimStart('/');
            return IdRegex().IsMatch(seg) ? seg : null;
        }

        var v = u.Query.Length > 0 ? QueryPairs(u.Query).FirstOrDefault(kv => kv.Key == "v").Value : null;
        if (v is not null && IdRegex().IsMatch(v))
        {
            return v;
        }

        var m = PathRegex().Match(u.AbsolutePath);
        return m.Success ? m.Groups[1].Value : null;
    }

    private static List<KeyValuePair<string, string>> QueryPairs(string query)
    {
        var list = new List<KeyValuePair<string, string>>();
        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            list.Add(new(Uri.UnescapeDataString(kv[0]), kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : ""));
        }
        return list;
    }

    [GeneratedRegex(@"^[A-Za-z0-9_-]{6,}$")]
    private static partial Regex IdRegex();

    [GeneratedRegex(@"^/(?:shorts|embed|v)/([A-Za-z0-9_-]{6,})")]
    private static partial Regex PathRegex();
}
