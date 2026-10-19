using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RecipeApp.Api.Data;

namespace RecipeApp.Api.Services;

public record ShoppingItemDto(Guid Id, string Name, string? Unit, double? Quantity, List<string> RecipeTitles, int MealCount, bool Checked, bool AddedManually);
public record ShoppingListDto(DateOnly WeekStart, DateTime GeneratedAt, List<ShoppingItemDto> Items);
public record PlanEntryDto(Guid? Id, DateOnly Date, MealSlot Slot, Guid? RecipeId, string? RecipeTitle);

/// <summary>
/// Builds the shopping list for a week by aggregating every ingredient of every
/// planned meal, matching on name+unit exactly (per AGENTS.md — never fuzzy).
/// A recipe planned on two days doubles its ingredient quantities; quantities
/// that some occurrences lack stay null (counted, not summed). Checked state is
/// preserved across regenerations so the household can de/select as they shop.
/// MealCount = number of planned meals needing the item (titles are de-duped).
/// </summary>
public class ShoppingListService(AppDbContext db)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public async Task<DateOnly> EnsureGenerated(DateOnly week, CancellationToken ct)
    {
        var monday = StartOfWeek(week);

        var entries = await db.MealPlanEntries
            .AsNoTracking()
            .Include(e => e.Recipe)
            .Where(e => e.Date >= monday && e.Date <= monday.AddDays(6))
            .ToListAsync(ct);

        var list = await db.ShoppingLists
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.WeekStart == monday, ct);

        // Checked state and hand-added items are user input — carry them across
        // regenerations instead of dropping them when the meal plan changes.
        var oldChecked = new Dictionary<string, bool>(StringComparer.Ordinal);
        if (list is not null)
        {
            foreach (var existing in list.Items)
            {
                // Assignment (not Add) tolerates legacy rows that differ only
                // by case, which the case-sensitive unique index allows.
                oldChecked[Key(existing.Name, existing.Unit)] = existing.Checked;
            }
        }
        var manual = list?.Items.Where(i => i.AddedManually).ToList() ?? [];

        if (list is null)
        {
            list = new ShoppingList { WeekStart = monday };
            db.ShoppingLists.Add(list);
        }
        else
        {
            foreach (var generated in list.Items.Where(i => !i.AddedManually).ToList())
            {
                list.Items.Remove(generated);
            }
        }
        list.GeneratedAt = DateTime.UtcNow;

        foreach (var (name, unit, qty, titles, meals) in Aggregate(entries))
        {
            var manualMatch = manual.FirstOrDefault(m => Key(m.Name, m.Unit) == Key(name, unit));
            if (manualMatch is not null)
            {
                // Keep the user's own wording/quantity, but enrich it with the
                // recipes that need it so it still shows up in the grouped view.
                manualMatch.Quantity ??= qty;
                manualMatch.RecipeTitlesJson = JsonSerializer.Serialize(titles, JsonOpts);
                manualMatch.MealCount = meals;
                continue;
            }

            list.Items.Add(new ShoppingListItem
            {
                Name = name,
                Unit = unit,
                Quantity = qty,
                RecipeTitlesJson = JsonSerializer.Serialize(titles, JsonOpts),
                MealCount = meals,
                Checked = oldChecked.GetValueOrDefault(Key(name, unit), false),
                AddedManually = false,
            });
        }

        await db.SaveChangesAsync(ct);
        return monday;
    }

    /// <summary>Normalized aggregation key: name+unit, case/whitespace/trailing-dot insensitive.</summary>
    private static string Key(string name, string? unit) =>
        $"{name.Trim().ToLowerInvariant()}|{(unit ?? "").Trim().TrimEnd('.').ToLowerInvariant()}";

    public static DateOnly StartOfWeek(DateOnly any)
    {
        var offset = ((int)any.DayOfWeek - (int)DayOfWeek.Monday + 14) % 7;
        return any.AddDays(-offset);
    }

    private static List<(string Name, string? Unit, double? Qty, List<string> Titles, int Meals)> Aggregate(
        List<MealPlanEntry> entries)
    {
        var buckets = new Dictionary<string, List<(double? qty, string title, IngredientEntry ing)>>(StringComparer.Ordinal);

        foreach (var e in entries)
        {
            List<IngredientEntry>? ings;
            try
            {
                ings = JsonSerializer.Deserialize<List<IngredientEntry>>(e.Recipe.IngredientsJson, JsonOpts);
            }
            catch (JsonException)
            {
                continue;
            }
            foreach (var ing in ings ?? [])
            {
                var name = ing.Name.Trim();
                if (name.Length == 0)
                {
                    continue;
                }
                var key = Key(name, ing.Unit);
                if (!buckets.TryGetValue(key, out var bucket))
                {
                    bucket = [];
                    buckets[key] = bucket;
                }
                bucket.Add((ing.Qty, e.Recipe.Title, ing));
            }
        }

        var result = new List<(string, string?, double?, List<string>, int)>();
        foreach (var bucket in buckets.Values)
        {
            var first = bucket[0].ing;
            var titles = bucket.Select(b => b.title).Distinct(StringComparer.Ordinal).ToList();
            double? qty = bucket.All(b => b.qty is not null) ? bucket.Sum(b => b.qty!.Value) : null;
            result.Add((first.Name, first.Unit, qty, titles, bucket.Count));
        }
        return result.OrderBy(r => r.Item1, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static ShoppingListDto ToDto(ShoppingList list) => new(
        list.WeekStart,
        list.GeneratedAt,
        list.Items
            .OrderBy(i => i.Checked)
            .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .Select(i => new ShoppingItemDto(
                i.Id,
                i.Name,
                i.Unit,
                i.Quantity,
                TryDeserializeTitles(i.RecipeTitlesJson) ?? [],
                i.MealCount,
                i.Checked,
                i.AddedManually))
            .ToList());

    private static List<string>? TryDeserializeTitles(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, JsonOpts);
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
