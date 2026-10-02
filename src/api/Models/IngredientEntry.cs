namespace RecipeApp.Api.Data;

public record IngredientEntry(double? Qty, string? Unit, string Name)
{
    public string Display => Qty is null
        ? $"{Unit} {Name}".Trim()
        : string.IsNullOrEmpty(Unit) ? $"{Qty} {Name}" : $"{Qty} {Unit} {Name}";
}
