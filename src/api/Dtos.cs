using RecipeApp.Api.Data;

namespace RecipeApp.Api;

public record RecipeSummaryDto(
    Guid Id,
    string Title,
    string? Description,
    string? ImageUrl,
    List<string> Tags,
    int Servings,
    int? PrepMinutes,
    int? CookMinutes,
    DateTime UpdatedAt);

public record RecipeDto(
    Guid Id,
    string Title,
    string? Description,
    string? SourceUrl,
    string? ImageUploadedPath,
    string? ImageOriginalUrl,
    string? ImageUrl,
    int Servings,
    int? PrepMinutes,
    int? CookMinutes,
    List<IngredientEntry> Ingredients,
    List<string> Steps,
    List<string> Tags,
    List<CommentDto> Comments,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record RecipeInput(
    string Title,
    string? Description,
    string? SourceUrl,
    string? ImageOriginalUrl,
    int Servings,
    int? PrepMinutes,
    int? CookMinutes,
    List<IngredientEntry> Ingredients,
    List<string> Steps,
    List<string> Tags);

public record CommentDto(Guid Id, string Author, string Text, DateTime CreatedAt);

public record CommentInput(string Author, string Text);
