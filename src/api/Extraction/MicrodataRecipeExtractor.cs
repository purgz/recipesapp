using HtmlAgilityPack;

namespace RecipeApp.Api.Extraction;

/// <summary>
/// Fallback for sites that mark up recipes with schema.org *microdata*
/// (itemprop/itemtype attributes) instead of JSON-LD — common on Danish
/// WordPress food blogs (e.g. valdemarsro.dk). Section headers like
/// &lt;li class="ingredient-header"&gt; don't carry itemprop and are ignored
/// automatically. Steps may live in nested &lt;p&gt;/&lt;li&gt; children.
/// </summary>
public class MicrodataRecipeExtractor(ScrapeFetcher fetcher) : IRecipeExtractor
{
    public int Priority => 30;

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

        var doc = new HtmlDocument();
        doc.LoadHtml(fetched.Html);

        var scope = doc.DocumentNode
            .SelectNodes("//*[contains(translate(@itemtype,'RECIPE','recipe'),'recipe')]")
            ?.FirstOrDefault(n => (n.GetAttributeValue("itemtype", "") ?? "").Contains("schema.org", StringComparison.OrdinalIgnoreCase));

        CollectFromNode(draft, doc, scope);
        if (scope is null)
        {
            // Some themes scatter itemprop attributes without a Recipe itemscope.
            CollectFromNode(draft, doc, doc.DocumentNode);
        }

        if (!draft.HasData)
        {
            draft.Warnings.Add("Ingen microdata-opskrift fundet.");
        }
        return draft;
    }

    private static void CollectFromNode(RecipeDraft draft, HtmlDocument doc, HtmlNode? scope)
    {
        if (scope is null)
        {
            return;
        }

        // Only treat scattered itemprops as belonging to the page if a Recipe
        // scope exists; otherwise the document root just yields junk.
        var hasIngredients = scope.SelectSingleNode(".//*[@itemprop='recipeIngredient']") is not null;
        var hasInstructions = scope.SelectSingleNode(".//*[@itemprop='recipeInstructions']") is not null;
        if (!hasIngredients && !hasInstructions)
        {
            return;
        }

        // itemprop="name" can be a breadcrumb/site node on WP themes, so prefer og:title/h1.
        draft.Title ??= Text(doc.DocumentNode.SelectSingleNode("//meta[@property='og:title']"))
                     ?? Text(doc.DocumentNode.SelectSingleNode("//h1"))
                     ?? Text(scope.SelectSingleNode(".//*[@itemprop='name']"));

        draft.Description ??= Text(doc.DocumentNode.SelectSingleNode("//meta[@property='og:description']"), "content")
                           ?? Text(doc.DocumentNode.SelectSingleNode("//meta[@name='description']"), "content");

        draft.ImageUrl ??= Text(doc.DocumentNode.SelectSingleNode("//meta[@property='og:image']"), "content")
                        ?? scope.SelectSingleNode(".//img")?.GetAttributeValue("src", null);

        if (scope.SelectNodes(".//*[@itemprop='recipeIngredient']") is { } ingNodes)
        {
            foreach (var ing in ingNodes)
            {
                var raw = HtmlEntity.DeEntitize(ing.InnerText.Clean());
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    draft.Ingredients.Add(IngredientParser.Parse(raw));
                }
            }
        }

        if (scope.SelectNodes(".//*[@itemprop='recipeInstructions']") is { } insNodes)
        {
            foreach (var ins in insNodes)
            {
                var parts = ins.SelectNodes("./p")
                           ?? ins.SelectNodes("./li")
                           ?? ins.SelectNodes("./ol")
                           ?? ins.SelectNodes("./ul");
                if (parts.Count == 0)
                {
                    TryAddStep(draft, ins.InnerText);
                    continue;
                }
                foreach (var child in parts)
                {
                    if (child.Name is "ol" or "ul")
                    {
                        if (child.SelectNodes("./li") is { } lis)
                        {
                            foreach (var li in lis)
                            {
                                TryAddStep(draft, li.InnerText);
                            }
                        }
                        continue;
                    }
                    TryAddStep(draft, child.InnerText);
                }
            }
        }

        var yieldText = Text(scope.SelectSingleNode(".//*[@itemprop='recipeYield']"));
        if (yieldText is not null)
        {
            draft.Servings ??= FirstInt(yieldText);
        }

        draft.PrepMinutes ??= Minutes(scope, "prepTime");
        draft.CookMinutes ??= Minutes(scope, "cookTime") ?? Minutes(scope, "totalTime");
    }

    private static void TryAddStep(RecipeDraft draft, string text)
    {
        var clean = HtmlEntity.DeEntitize(text.Clean());
        if (!string.IsNullOrWhiteSpace(clean))
        {
            draft.Steps.Add(clean);
        }
    }

    private static string? Text(HtmlNode? node, string? attr = null)
    {
        if (node is null)
        {
            return null;
        }
        var value = string.IsNullOrWhiteSpace(attr)
            ? HtmlEntity.DeEntitize(node.InnerText.Clean())
            : HtmlEntity.DeEntitize(node.GetAttributeValue(attr, "").Clean());
        return value.Length == 0 ? null : value;
    }

    private static int? Minutes(HtmlNode scope, string prop)
    {
        var node = scope.SelectSingleNode($".//*[@itemprop='{prop}']");
        var raw = node?.GetAttributeValue("content", null) ?? node?.GetAttributeValue("datetime", null) ?? node?.InnerText;
        return raw is null ? null : Duration.Parse(raw.Clean());
    }

    private static int? FirstInt(string? s)
    {
        var m = System.Text.RegularExpressions.Regex.Match(s ?? "", @"\d+");
        return m.Success ? int.Parse(m.Value) : null;
    }
}

/// <summary>Handles ISO-8601 durations (PT1H30M) plus "1 time 15 min" style leftovers.</summary>
file static class Duration
{
    public static int? Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }
        try
        {
            var ts = System.Xml.XmlConvert.ToTimeSpan(raw);
            var minutes = (int)Math.Round(ts.TotalMinutes);
            return minutes > 0 ? minutes : null;
        }
        catch (FormatException)
        {
            var m = System.Text.RegularExpressions.Regex.Match(raw, @"(?:(\d+)\s*time)?\D*(?:(\d+)\s*min)?");
            if (m.Success && (m.Groups[1].Success || m.Groups[2].Success))
            {
                var hours = m.Groups[1].Success ? int.Parse(m.Groups[1].Value) : 0;
                var mins = m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 0;
                return hours * 60 + mins;
            }
            return null;
        }
    }
}

file static class TextCleanup
{
    public static string Clean(this string s) =>
        System.Text.RegularExpressions.Regex.Replace(
            s.Replace("\u00A0", " ").Replace("\t", " "),
            @"\s+", " ").Trim();
}
