using RecipeApp.Api.Data;

namespace RecipeApp.Api.Extraction;

/// <summary>
/// Result of running the extraction chain over a URL. Every field is optional;
/// anything missing gets filled in manually in the edit form.
/// RawText carries material that resists structured parsing (e.g. a YouTube
/// transcript) for the user to structure by hand.
/// </summary>
public class RecipeDraft
{
    public string? SourceUrl { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public int? Servings { get; set; }
    public int? PrepMinutes { get; set; }
    public int? CookMinutes { get; set; }

    public List<IngredientEntry> Ingredients { get; set; } = [];
    public List<string> Steps { get; set; } = [];
    public List<string> Tags { get; set; } = [];

    /// <summary>Transcript or description text for manual structuring in the editor.</summary>
    public string? RawText { get; set; }

    /// <summary>What RawText contains: "transcript" | "description".</summary>
    public string? RawTextKind { get; set; }

    public List<string> Warnings { get; set; } = [];

    public bool HasData =>
        !string.IsNullOrWhiteSpace(Title)
        || Ingredients.Count > 0
        || Steps.Count > 0
        || !string.IsNullOrWhiteSpace(ImageUrl)
        || !string.IsNullOrWhiteSpace(RawText)
        || Tags.Count > 0;
}
