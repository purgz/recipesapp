using RecipeApp.Api.Data;

namespace RecipeApp.Api.Extraction;

/// <summary>
/// Result of running the extraction chain over a URL. Every field is optional;
/// anything missing gets filled in manually in the edit form.
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

    public List<string> Warnings { get; set; } = [];

    public bool HasData =>
        !string.IsNullOrWhiteSpace(Title)
        || Ingredients.Count > 0
        || Steps.Count > 0
        || !string.IsNullOrWhiteSpace(ImageUrl)
        || Tags.Count > 0;
}
