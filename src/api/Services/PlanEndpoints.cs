using Microsoft.EntityFrameworkCore;
using RecipeApp.Api.Data;
using RecipeApp.Api.Services;
using System.Text.Json;

namespace RecipeApp.Api.Services;

public static class PlanEndpoints
{
    public static IEndpointRouteBuilder MapPlanEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/plan", List);
        app.MapPut("/api/plan/entry", Upsert);
        app.MapDelete("/api/plan/entry", Delete);
        app.MapPost("/api/plan/suggest", Suggest);
        app.MapPut("/api/plan/entries", ApplyEntries);

        app.MapGet("/api/shopping", GetList);
        app.MapPost("/api/shopping/generate", Generate);
        app.MapPost("/api/shopping/item", AddCustom);
        app.MapPatch("/api/shopping/item/{id:guid}", SetChecked);
        app.MapDelete("/api/shopping/item/{id:guid}", RemoveItem);

        return app;
    }

    private static DateOnly WeekOf(HttpContext ctx)
    {
        var raw = ctx.Request.Query["week"].ToString();
        if (DateOnly.TryParseExact(raw, "yyyy-MM-dd", out var parsed))
        {
            return ShoppingListService.StartOfWeek(parsed);
        }
        return ShoppingListService.StartOfWeek(DateOnly.FromDateTime(DateTime.UtcNow));
    }

    private static async Task<IResult> List(HttpContext ctx, AppDbContext db, CancellationToken ct)
    {
        var monday = WeekOf(ctx);
        var entries = await db.MealPlanEntries
            .AsNoTracking()
            .Where(e => e.Date >= monday && e.Date <= monday.AddDays(6))
            .Select(e => new PlanEntryDto(e.Id, e.Date, e.Slot, e.RecipeId, e.Recipe.Title))
            .ToListAsync(ct);
        return Results.Ok(entries);
    }

    private static async Task<IResult> Upsert(
        AppDbContext db, ShoppingListService shopping,
        UpsertEntryRequest req, CancellationToken ct)
    {
        if (req.RecipeId is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["recipeId"] = ["recipeId skal angives."] });
        }
        var recipe = await db.Recipes.FirstOrDefaultAsync(r => r.Id == req.RecipeId, ct);
        if (recipe is null)
        {
            return Results.NotFound(new { error = "opskrift findes ikke" });
        }

        var entry = await db.MealPlanEntries
            .FirstOrDefaultAsync(e => e.Date == req.Date && e.Slot == req.Slot, ct);
        if (entry is null)
        {
            entry = new MealPlanEntry { Date = req.Date, Slot = req.Slot };
            db.MealPlanEntries.Add(entry);
        }
        entry.RecipeId = req.RecipeId.Value;

        await db.SaveChangesAsync(ct);
        await shopping.EnsureGenerated(req.Date, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> Delete(
        AppDbContext db, ShoppingListService shopping,
        DateOnly date, MealSlot slot, CancellationToken ct)
    {
        await db.MealPlanEntries.Where(e => e.Date == date && e.Slot == slot).ExecuteDeleteAsync(ct);
        await shopping.EnsureGenerated(date, ct);
        return Results.NoContent();
    }

    /// <summary>Preview only: nothing is saved until the client applies it.</summary>
    private static async Task<IResult> Suggest(
        MealPlanSuggester suggester, SuggestPlanRequest? req, CancellationToken ct)
    {
        var week = req?.Week ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var slots = req?.Slots is { Count: > 0 } selected ? selected : new List<MealSlot> { MealSlot.Dinner };
        return Results.Ok(await suggester.SuggestAsync(week, slots, ct));
    }

    /// <summary>Bulk apply so a suggested week costs one shopping-list regeneration.</summary>
    private static async Task<IResult> ApplyEntries(
        AppDbContext db, ShoppingListService shopping,
        ApplyPlanRequest req, CancellationToken ct)
    {
        if (req.Entries.Count == 0)
        {
            return Results.NoContent();
        }

        var ids = req.Entries.Select(e => e.RecipeId).Distinct().ToList();
        var known = await db.Recipes.Where(r => ids.Contains(r.Id)).Select(r => r.Id).ToListAsync(ct);
        var missing = ids.Except(known).ToList();
        if (missing.Count > 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["entries"] = ["En eller flere opskrifter findes ikke længere."],
            });
        }

        foreach (var input in req.Entries)
        {
            var entry = await db.MealPlanEntries
                .FirstOrDefaultAsync(e => e.Date == input.Date && e.Slot == input.Slot, ct);
            if (entry is null)
            {
                entry = new MealPlanEntry { Date = input.Date, Slot = input.Slot };
                db.MealPlanEntries.Add(entry);
            }
            entry.RecipeId = input.RecipeId;
        }

        await db.SaveChangesAsync(ct);

        foreach (var monday in req.Entries.Select(e => ShoppingListService.StartOfWeek(e.Date)).Distinct())
        {
            await shopping.EnsureGenerated(monday, ct);
        }
        return Results.NoContent();
    }

    private static async Task<IResult> GetList(HttpContext ctx, AppDbContext db, CancellationToken ct)
    {
        var monday = WeekOf(ctx);
        var list = await db.ShoppingLists.AsNoTracking()
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.WeekStart == monday, ct);
        if (list is null)
        {
            return Results.NotFound();
        }
        return Results.Ok(ShoppingListService.ToDto(list));
    }

    private static async Task<IResult> Generate(
        AppDbContext db, ShoppingListService shopping,
        GenerateRequest? req, CancellationToken ct)
    {
        var monday = await shopping.EnsureGenerated(req?.Week ?? DateOnly.FromDateTime(DateTime.UtcNow), ct);
        var list = await db.ShoppingLists.AsNoTracking()
            .Include(s => s.Items)
            .FirstAsync(s => s.WeekStart == monday, ct);
        return Results.Ok(ShoppingListService.ToDto(list));
    }

    private static async Task<IResult> AddCustom(
        AppDbContext db, AddItemRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["name"] = ["Navn skal angives."] });
        }
        var monday = ShoppingListService.StartOfWeek(req.Week);
        var list = await db.ShoppingLists.Include(s => s.Items).FirstOrDefaultAsync(s => s.WeekStart == monday, ct);
        if (list is null)
        {
            list = new ShoppingList { WeekStart = monday };
            db.ShoppingLists.Add(list);
        }
        if (list.Items.Any(i => string.Equals(i.Name, req.Name.Trim(), StringComparison.OrdinalIgnoreCase) && (i.Unit ?? "") == (req.Unit ?? "")))
        {
            return Results.Conflict(new { error = "Varen findes allerede på listen." });
        }
        var item = new ShoppingListItem
        {
            Name = req.Name.Trim(),
            Unit = req.Unit,
            Quantity = req.Quantity,
            RecipeTitlesJson = "[]",
            Checked = false,
            AddedManually = true,
        };
        list.Items.Add(item);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/shopping/item/{item.Id}", ShoppingListService.ToDto(list));
    }

    private static async Task<IResult> SetChecked(AppDbContext db, Guid id, SetCheckedRequest req, CancellationToken ct)
    {
        var item = await db.ShoppingListItems.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (item is null)
        {
            return Results.NotFound();
        }
        item.Checked = req.Checked;
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> RemoveItem(AppDbContext db, Guid id, CancellationToken ct)
    {
        var rows = await db.ShoppingListItems.Where(i => i.Id == id).ExecuteDeleteAsync(ct);
        return rows == 0 ? Results.NotFound() : Results.NoContent();
    }

    public record UpsertEntryRequest(DateOnly Date, MealSlot Slot, Guid? RecipeId);
    public record SuggestPlanRequest(DateOnly? Week, List<MealSlot>? Slots);
    public record ApplyPlanRequest(List<ApplyPlanEntry> Entries);
    public record ApplyPlanEntry(DateOnly Date, MealSlot Slot, Guid RecipeId);
    public record AddItemRequest(DateOnly Week, string Name, string? Unit, double? Quantity);
    public record SetCheckedRequest(bool Checked);
    public record GenerateRequest(DateOnly Week);
}
