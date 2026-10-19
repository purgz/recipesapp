namespace RecipeApp.Api.Extraction;

/// <summary>
/// Runs all extractors that CanHandle(url), lowest Priority first, and stops
/// at the first result with usable data. If none succeed it returns an empty
/// draft whose Warnings carry each extractor's failure reason — the UI opens
/// the manual edit form either way.
/// Extraction is deterministic (no LLM by design); extractors share one
/// ScrapeFetcher so the page is downloaded at most once per request.
/// </summary>
public class ExtractionPipeline(IEnumerable<IRecipeExtractor> extractors, ILogger<ExtractionPipeline> log)
{
    public async Task<RecipeDraft> ExtractAsync(string url, CancellationToken ct)
    {
        var empty = new RecipeDraft { SourceUrl = url };

        var chain = extractors
            .Where(e => e.CanHandle(url))
            .OrderBy(e => e.Priority)
            .ToList();

        if (chain.Count == 0)
        {
            empty.Warnings.Add("Ingen udtrækker understøtter denne URL. Udfyld formularen manuelt.");
            return empty;
        }

        foreach (var extractor in chain)
        {
            try
            {
                var draft = await extractor.ExtractAsync(url, ct);
                if (draft.HasData)
                {
                    return draft;
                }
                log.LogInformation("Extractor {Type} produced no data for {Url}", extractor.GetType().Name, url);
                foreach (var w in draft.Warnings)
                {
                    if (!empty.Warnings.Contains(w))
                    {
                        empty.Warnings.Add(w);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Extractor {Type} failed for {Url}", extractor.GetType().Name, url);
                if (!empty.Warnings.Contains(ex.Message))
                {
                    empty.Warnings.Add(ex.Message);
                }
            }
        }

        empty.Warnings.Add("Kunne ikke udtrække opskriften. Udfyld formular manuelt.");
        return empty;
    }
}
