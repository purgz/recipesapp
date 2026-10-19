namespace RecipeApp.Api.Extraction;

/// <summary>
/// Fetches a page at most once per request and shares the HTML between
/// extractors. The fallback chain would otherwise download the same page twice
/// (JSON-LD miss → microdata fallback), doubling latency and load on the
/// source site for exactly the old WordPress blogs that need the fallback.
/// </summary>
public sealed class ScrapeFetcher(IHttpClientFactory httpFactory, ILogger<ScrapeFetcher> log)
{
    private const long MaxHtmlBytes = 8 * 1024 * 1024;

    private readonly Dictionary<string, Task<FetchResult>> _cache = new(StringComparer.Ordinal);

    public Task<FetchResult> FetchAsync(string url, CancellationToken ct)
    {
        if (_cache.TryGetValue(url, out var cached))
        {
            log.LogDebug("Reusing scraped HTML for {Url}", url);
            return cached;
        }

        var task = FetchCoreAsync(url, ct);
        _cache[url] = task;
        return task;
    }

    private async Task<FetchResult> FetchCoreAsync(string url, CancellationToken ct)
    {
        using var resp = await httpFactory.CreateClient("scrape")
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);

        if (!resp.IsSuccessStatusCode)
        {
            return new FetchResult(null, $"Hentning mislykkedes (HTTP {(int)resp.StatusCode}).");
        }

        if (resp.Content.Headers.ContentLength > MaxHtmlBytes)
        {
            return new FetchResult(null, "Siden er for stor til at kunne indlæses.");
        }

        var html = await resp.Content.ReadAsStringAsync(ct);
        return html.Length == 0
            ? new FetchResult(null, "Siden returnerede intet indhold.")
            : new FetchResult(html, null);
    }
}

public record FetchResult(string? Html, string? Error);
