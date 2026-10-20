using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RecipeApp.Api.Data;
using RecipeApp.Api.Extraction;

namespace RecipeApp.Api.Services;

public record SuggestedPlanEntry(
    DateOnly Date,
    MealSlot Slot,
    Guid RecipeId,
    string RecipeTitle,
    int? PrepMinutes,
    int? CookMinutes);

public record SuggestPlanResponse(string Source, List<SuggestedPlanEntry> Entries, string? Warning);

/// <summary>
/// Proposes a week of meals for one or more slots. The AI path sends only
/// numbered titles/tags — roughly 2k tokens regardless of slot count — and
/// returns slot/day→index picks. Everything is validated against the real
/// library and missing meals are filled deterministically, so a quota or model
/// failure still produces a usable plan (with a warning).
/// </summary>
public class MealPlanSuggester(AppDbContext db, GroqClient groq, ILogger<MealPlanSuggester> log)
{
    /// <summary>Keeps the prompt well inside the free tier's 8k tokens/minute.</summary>
    private const int MaxRecipes = 200;

    public async Task<SuggestPlanResponse> SuggestAsync(
        DateOnly week, IReadOnlyCollection<MealSlot> slots, CancellationToken ct)
    {
        var monday = ShoppingListService.StartOfWeek(week);
        var selected = slots.Count == 0
            ? [MealSlot.Dinner]
            : slots.Distinct().OrderBy(s => s).ToList();

        var recipes = await LoadRecipesAsync(ct);
        if (recipes.Count == 0)
        {
            return new SuggestPlanResponse("fallback", [], "Der er ingen opskrifter at vælge imellem endnu.");
        }

        var picks = new Dictionary<(MealSlot Slot, int Day), RecipeCandidate>();
        string? warning = null;
        var source = "fallback";

        if (groq.Enabled)
        {
            try
            {
                var content = await groq.CompleteJsonAsync(
                    SystemPrompt, BuildRecipeList(recipes, selected), BuildSchema(recipes.Count, selected), ct);
                picks = ParsePicks(content, recipes, selected, out var note);
                warning = note;
                if (picks.Count > 0)
                {
                    source = "ai";
                }
            }
            catch (GroqException ex)
            {
                // Already user-facing (quota reset text included).
                warning = ex.Message;
                log.LogInformation("Meal plan AI unavailable: {Message}", ex.Message);
            }
            catch (JsonException ex)
            {
                log.LogWarning(ex, "Meal plan AI returned malformed JSON");
                warning = "AI-forslaget kunne ikke læses – planen er lavet uden AI.";
            }
        }
        else
        {
            warning = "AI er ikke konfigureret – planen er lavet uden AI.";
        }

        var entries = await FillAsync(monday, selected, recipes, picks, ct);
        return new SuggestPlanResponse(source, entries, warning);
    }

    private async Task<List<RecipeCandidate>> LoadRecipesAsync(CancellationToken ct)
    {
        var rows = await db.Recipes.AsNoTracking()
            .Include(r => r.Tags).ThenInclude(rt => rt.Tag)
            .OrderByDescending(r => r.UpdatedAt)
            .Take(MaxRecipes)
            .ToListAsync(ct);

        return rows.Select(r => new RecipeCandidate(
                r.Id,
                r.Title,
                r.PrepMinutes,
                r.CookMinutes,
                r.Tags.Select(t => t.Tag.Name).OrderBy(n => n).ToList()))
            .ToList();
    }

    private static string BuildRecipeList(IReadOnlyList<RecipeCandidate> recipes, List<MealSlot> slots)
    {
        var sb = new StringBuilder();
        sb.Append("Slots: ").Append(string.Join(", ", slots.Select(s => $"{SlotName(s)} ({SlotValue(s)})"))).Append('\n');
        sb.Append("Recipes:\n");
        for (var i = 0; i < recipes.Count; i++)
        {
            var r = recipes[i];
            sb.Append(i + 1).Append(". ").Append(r.Title);
            if (r.Tags.Count > 0)
            {
                sb.Append(" | ").Append(string.Join(", ", r.Tags));
            }
            if (r.TotalMinutes is { } minutes)
            {
                sb.Append(" | ").Append(minutes).Append(" min");
            }
            sb.Append('\n');
        }
        return sb.ToString();
    }

    private const string SystemPrompt =
        """
        You plan a Danish household's week, Monday to Sunday.
        Rules:
        - For EACH requested slot, return exactly one pick per day: day 0 = Monday ... day 6 = Sunday.
        - Slot values: 1 = breakfast, 2 = lunch, 3 = dinner, 4 = extra/dessert.
        - Use each recipe index at most once across the whole plan when possible.
        - Prefer recipes of 35 minutes or less on weekdays (days 0-4); longer recipes suit the weekend.
        - Vary the tags/cuisines across the week.
        - Use only indices from the recipe list and do not invent recipes.
        - Return ONLY JSON matching the schema.
        """;

    private static JsonElement BuildSchema(int count, List<MealSlot> slots) => JsonDocument.Parse(
        $$"""
        {
          "type": "object",
          "properties": {
            "picks": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "slot":  { "type": "integer", "enum": [{{string.Join(",", slots.Select(SlotValue))}}] },
                  "day":   { "type": "integer", "minimum": 0, "maximum": 6 },
                  "index": { "type": "integer", "minimum": 1, "maximum": {{count}} }
                },
                "required": ["slot", "day", "index"],
                "additionalProperties": false
              }
            }
          },
          "required": ["picks"],
          "additionalProperties": false
        }
        """).RootElement.Clone();

    private static Dictionary<(MealSlot Slot, int Day), RecipeCandidate> ParsePicks(
        string content, IReadOnlyList<RecipeCandidate> recipes, List<MealSlot> slots, out string? warning)
    {
        warning = null;
        var result = new Dictionary<(MealSlot, int), RecipeCandidate>();
        var expected = slots.Count * 7;

        using var doc = JsonDocument.Parse(content);
        if (!doc.RootElement.TryGetProperty("picks", out var picks) || picks.ValueKind != JsonValueKind.Array)
        {
            warning = "AI-forslaget havde et uventet format – resten er fyldt ud automatisk.";
            return result;
        }

        var used = new HashSet<Guid>();
        foreach (var pick in picks.EnumerateArray())
        {
            if (!pick.TryGetProperty("slot", out var slotEl) || !slotEl.TryGetInt32(out var slotValue) ||
                !Enum.IsDefined(typeof(MealSlot), slotValue))
            {
                continue;
            }
            var slot = (MealSlot)slotValue;
            if (!slots.Contains(slot))
            {
                continue;
            }

            if (!pick.TryGetProperty("day", out var dayEl) || !dayEl.TryGetInt32(out var day) || day is < 0 or > 6)
            {
                continue;
            }
            if (!pick.TryGetProperty("index", out var indexEl) || !indexEl.TryGetInt32(out var index))
            {
                continue;
            }
            if (index < 1 || index > recipes.Count || result.ContainsKey((slot, day)))
            {
                continue;
            }

            var candidate = recipes[index - 1];
            if (!used.Add(candidate.Id))
            {
                continue;
            }
            result[(slot, day)] = candidate;
        }

        if (result.Count < expected)
        {
            warning = "AI-forslaget dækkede ikke hele planen – resten er fyldt ud automatisk.";
        }
        return result;
    }

    private async Task<List<SuggestedPlanEntry>> FillAsync(
        DateOnly monday,
        List<MealSlot> slots,
        List<RecipeCandidate> recipes,
        Dictionary<(MealSlot Slot, int Day), RecipeCandidate> picks,
        CancellationToken ct)
    {
        var weekEnd = monday.AddDays(6);
        var plannedThisWeek = await db.MealPlanEntries
            .Where(e => e.Date >= monday && e.Date <= weekEnd)
            .Select(e => e.RecipeId)
            .ToListAsync(ct);

        var lastPlanned = await db.MealPlanEntries
            .Where(e => e.RecipeId != Guid.Empty)
            .GroupBy(e => e.RecipeId)
            .Select(g => new { Id = g.Key, Last = g.Max(x => x.Date) })
            .ToDictionaryAsync(x => x.Id, x => x.Last, ct);

        var aiPicks = new HashSet<Guid>(picks.Values.Select(p => p.Id));
        var pool = recipes
            .Where(r => !aiPicks.Contains(r.Id))
            .OrderBy(r => plannedThisWeek.Contains(r.Id) ? 1 : 0)
            .ThenBy(r => lastPlanned.TryGetValue(r.Id, out var last) ? last : DateOnly.MinValue)
            .ToList();

        var entries = new List<SuggestedPlanEntry>(slots.Count * 7);

        foreach (var slot in slots)
        {
            Guid? previous = null;
            for (var day = 0; day < 7; day++)
            {
                RecipeCandidate? choice;
                if (picks.TryGetValue((slot, day), out var aiPick) && aiPick.Id != previous)
                {
                    choice = aiPick;
                }
                else
                {
                    // Deterministic fallback: least recently planned first,
                    // never the same recipe on consecutive days if avoidable.
                    choice = pool.FirstOrDefault(r => r.Id != previous)
                             ?? recipes.FirstOrDefault(r => r.Id != previous)
                             ?? recipes[0];
                    if (choice is not null)
                    {
                        pool.Remove(choice);
                    }
                }

                if (choice is null)
                {
                    break;
                }

                previous = choice.Id;
                entries.Add(new SuggestedPlanEntry(
                    monday.AddDays(day), slot, choice.Id, choice.Title, choice.PrepMinutes, choice.CookMinutes));
            }
        }

        return entries;
    }

    private static int SlotValue(MealSlot slot) => (int)slot;

    private static string SlotName(MealSlot slot) => slot switch
    {
        MealSlot.Breakfast => "breakfast",
        MealSlot.Lunch => "lunch",
        MealSlot.Dinner => "dinner",
        _ => "extra/dessert",
    };

    private sealed record RecipeCandidate(
        Guid Id,
        string Title,
        int? PrepMinutes,
        int? CookMinutes,
        List<string> Tags)
    {
        public int? TotalMinutes =>
            PrepMinutes is null && CookMinutes is null ? null : (PrepMinutes ?? 0) + (CookMinutes ?? 0);
    }
}
