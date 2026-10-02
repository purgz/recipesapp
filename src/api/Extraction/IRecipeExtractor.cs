namespace RecipeApp.Api.Extraction;

/// <summary>
/// One link in the extraction chain. Never call an extractor directly from a
/// controller — always go through <see cref="ExtractionPipeline"/>, which runs
/// all extractors whose <see cref="CanHandle"/> matches, lowest Priority first,
/// and stops at the first one that produced usable data.
/// </summary>
public interface IRecipeExtractor
{
    /// <summary>Lower runs earlier. SchemaOrg = 20 so YouTube-specific handling wins on YouTube URLs.</summary>
    int Priority { get; }

    bool CanHandle(string url);

    Task<RecipeDraft> ExtractAsync(string url, CancellationToken ct);
}
