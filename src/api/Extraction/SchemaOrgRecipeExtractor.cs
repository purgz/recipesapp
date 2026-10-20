using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;

namespace RecipeApp.Api.Extraction;

/// <summary>schema.org/Recipe JSON-LD → RecipeDraft</summary>
public partial class SchemaOrgRecipeExtractor(ScrapeFetcher fetcher) : IRecipeExtractor
{
    public int Priority => 20;

    public bool CanHandle(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) &&
        (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps);

    public async Task<RecipeDraft> ExtractAsync(string url, CancellationToken ct)
    {
        var draft = new RecipeDraft { SourceUrl = url };

        var fetched = await fetcher.FetchAsync(url, ct);
        if (fetched.Html is null)
        {
            draft.Warnings.Add(fetched.Error!);
            return draft;
        }

        var html = fetched.Html;

        var nodes = JsonLd.FindRecipeNodes(html);
        if (nodes.Count == 0)
        {
            draft.Warnings.Add("Fandt ingen schema.org Recipe-data (JSON-LD). Udfyld manuel formular.");
            return draft;
        }

        MapNode(draft, nodes[0]);
        if (!draft.HasData)
        {
            draft.Warnings.Add("schema.org data var tom. Udfyld manuel formular.");
        }
        return draft;
    }

    private void MapNode(RecipeDraft draft, JsonElement node)
    {
        draft.Title ??= JsonLd.Str(node, "name");
        draft.Description ??= JsonLd.Str(node, "description");
        draft.ImageUrl ??= JsonLd.FirstImage(node);

        foreach (var y in JsonLd.Values(node, "recipeYield"))
        {
            draft.Servings ??= FirstInt(y.ToString());
        }

        draft.PrepMinutes ??= ParseDuration(JsonLd.Str(node, "prepTime"));
        draft.CookMinutes ??= ParseDuration(JsonLd.Str(node, "cookTime")) ?? ParseDuration(JsonLd.Str(node, "totalTime"));

        foreach (var ing in JsonLd.Values(node, "recipeIngredient"))
        {
            var raw = ing.ToString();
            if (!string.IsNullOrWhiteSpace(raw))
            {
                draft.Ingredients.Add(IngredientParser.Parse(raw));
            }
        }

        foreach (var s in JsonLd.Values(node, "recipeInstructions"))
        {
            CollectStep(s, draft);
        }

        // Categories carry the meal type ("Dessert", "Aftensmad"); keywords add
        // the site's free-form tags. Normalize both into one tag vocabulary.
        var rawTags = JsonLd.Values(node, "recipeCategory")
            .Concat(JsonLd.Values(node, "recipeCuisine"))
            .Concat(JsonLd.Values(node, "keywords"))
            .Select(v => v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : v.ToString());
        draft.Tags = TagNormalizer.Normalize(rawTags);
    }

    private static void CollectStep(JsonElement s, RecipeDraft draft)
    {
        switch (s.ValueKind)
        {
            case JsonValueKind.String:
                if (!string.IsNullOrWhiteSpace(s.GetString()))
                {
                    draft.Steps.Add(s.GetString()!.Trim());
                }
                return;
            case JsonValueKind.Object:
                var text = JsonLd.Str(s, "text");
                if (!string.IsNullOrWhiteSpace(text))
                {
                    draft.Steps.Add(text.Trim());
                    return;
                }
                foreach (var sub in JsonLd.Values(s, "itemListElement"))
                {
                    CollectStep(sub, draft);
                }
                return;
        }
    }

    private static int? FirstInt(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            return null;
        }
        var m = Int().Match(s);
        return m.Success ? int.Parse(m.Value) : null;
    }

    private static int? ParseDuration(string? iso)
    {
        if (string.IsNullOrWhiteSpace(iso))
        {
            return null;
        }
        try
        {
            var ts = XmlConvert.ToTimeSpan(iso);
            var minutes = (int)Math.Round(ts.TotalMinutes);
            return minutes > 0 ? minutes : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    [GeneratedRegex(@"\d+")]
    private static partial Regex Int();
}
