namespace RecipeApp.Api.Data;

public enum MealSlot
{
    Breakfast = 1,
    Lunch = 2,
    Dinner = 3,
    Extra = 4,
}

public class Recipe
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? SourceUrl { get; set; }
    public string? Description { get; set; }
    public string? ImageUploadedPath { get; set; }
    public string? ImageOriginalUrl { get; set; }
    public int Servings { get; set; } = 4;
    public int? PrepMinutes { get; set; }
    public int? CookMinutes { get; set; }
    public string IngredientsJson { get; set; } = "[]";
    public string StepsJson { get; set; } = "[]";
    public bool IsSeasonalHighlight { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<RecipeTag> Tags { get; set; } = [];
    public List<RecipeComment> Comments { get; set; } = [];
}

public class Tag
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<RecipeTag> Recipes { get; set; } = [];
}

public class RecipeTag
{
    public Guid RecipeId { get; set; }
    public Recipe Recipe { get; set; } = null!;
    public Guid TagId { get; set; }
    public Tag Tag { get; set; } = null!;
}

public class RecipeComment
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public Recipe Recipe { get; set; } = null!;
    public string Author { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class MealPlanEntry
{
    public Guid Id { get; set; }
    public DateOnly Date { get; set; }
    public MealSlot Slot { get; set; }
    public Guid RecipeId { get; set; }
    public Recipe Recipe { get; set; } = null!;
}

public class ShoppingList
{
    public Guid Id { get; set; }
    public DateOnly WeekStart { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public List<ShoppingListItem> Items { get; set; } = [];
}

public class ShoppingListItem
{
    public Guid Id { get; set; }
    public Guid ShoppingListId { get; set; }
    public ShoppingList ShoppingList { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public double? Quantity { get; set; }
    public string RecipeTitlesJson { get; set; } = "[]";
    public bool Checked { get; set; }
    public bool AddedManually { get; set; }
}
