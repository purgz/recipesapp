using Microsoft.EntityFrameworkCore;
using RecipeApp.Api.Data;
using System.Text.Json;

namespace RecipeApp.Api.Services;

public static class RecipeEndpoints
{
    private static readonly string[] AllowedImageExtensions = [".jpg", ".jpeg", ".png", ".webp"];
    private static readonly string ImageDirName = Path.Combine("App_Data", "images");

    public static IEndpointRouteBuilder MapRecipeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/recipes").WithTags("Recipes");

        group.MapGet("/", List);
        group.MapGet("/{id:guid}", Get);
        group.MapPost("/", Create);
        group.MapPut("/{id:guid}", Update);
        group.MapDelete("/{id:guid}", Delete);
        group.MapPost("/{id:guid}/image", UploadImage).DisableAntiforgery();
        group.MapDelete("/{id:guid}/image", DeleteImage);
        group.MapPost("/{id:guid}/comments", AddComment);
        group.MapDelete("/{id:guid}/comments/{commentId:guid}", DeleteComment);

        return app;
    }

    private static async Task<IResult> List(AppDbContext db, string? search, string? tag, CancellationToken ct)
    {
        var q = db.Recipes.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(tag))
        {
            q = q.Where(r => r.Tags.Any(rt => rt.Tag.Name == tag.Trim().ToLowerInvariant()));
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            q = q.Where(r => EF.Functions.Like(r.Title, $"%{search.Trim()}%"));
        }

        var items = await q
            .OrderByDescending(r => r.UpdatedAt)
            .Select(r => new RecipeSummaryDto(
                r.Id,
                r.Title,
                r.Description,
                r.ImageUploadedPath ?? r.ImageOriginalUrl,
                r.Tags.Select(rt => rt.Tag.Name).OrderBy(t => t).ToList(),
                r.Servings,
                r.PrepMinutes,
                r.CookMinutes,
                r.IsSeasonalHighlight,
                r.UpdatedAt))
            .ToListAsync(ct);
        return Results.Ok(items);
    }

    private static async Task<IResult> Get(AppDbContext db, Guid id, CancellationToken ct)
    {
        var r = await db.Recipes.AsNoTracking()
            .Include(x => x.Tags).ThenInclude(rt => rt.Tag)
            .Include(x => x.Comments)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        return r is null ? Results.NotFound() : Results.Ok(ToDto(r));
    }

    private static async Task<IResult> Create(AppDbContext db, RecipeInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Title))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["title"] = ["Titel er påkrævet."] });
        }

        var recipe = new Recipe
        {
            Title = input.Title.Trim(),
            Description = input.Description,
            SourceUrl = input.SourceUrl,
            ImageOriginalUrl = input.ImageOriginalUrl,
            Servings = Math.Clamp(input.Servings, 1, 99),
            PrepMinutes = input.PrepMinutes,
            CookMinutes = input.CookMinutes,
            IngredientsJson = JsonSerializer.Serialize(input.Ingredients),
            StepsJson = JsonSerializer.Serialize(input.Steps),
            IsSeasonalHighlight = input.IsSeasonalHighlight,
        };
        await UpsertTags(db, recipe, input.Tags, ct);

        db.Recipes.Add(recipe);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/recipes/{recipe.Id}", ToDto(recipe));
    }

    private static async Task<IResult> Update(AppDbContext db, Guid id, RecipeInput input, CancellationToken ct)
    {
        var recipe = await db.Recipes
            .Include(x => x.Tags).ThenInclude(rt => rt.Tag)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (recipe is null)
        {
            return Results.NotFound();
        }
        if (string.IsNullOrWhiteSpace(input.Title))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["title"] = ["Titel er påkrævet."] });
        }

        recipe.Title = input.Title.Trim();
        recipe.Description = input.Description;
        recipe.SourceUrl = input.SourceUrl;
        recipe.ImageOriginalUrl = input.ImageOriginalUrl;
        recipe.Servings = Math.Clamp(input.Servings, 1, 99);
        recipe.PrepMinutes = input.PrepMinutes;
        recipe.CookMinutes = input.CookMinutes;
        recipe.IngredientsJson = JsonSerializer.Serialize(input.Ingredients);
        recipe.StepsJson = JsonSerializer.Serialize(input.Steps);
        recipe.IsSeasonalHighlight = input.IsSeasonalHighlight;
        recipe.UpdatedAt = DateTime.UtcNow;

        recipe.Tags.Clear();
        await UpsertTags(db, recipe, input.Tags, ct);

        await db.SaveChangesAsync(ct);
        return Results.Ok(ToDto(recipe));
    }

    private static async Task<IResult> Delete(AppDbContext db, Guid id, CancellationToken ct)
    {
        var rows = await db.Recipes.Where(r => r.Id == id).ExecuteDeleteAsync(ct);
        return rows == 0 ? Results.NotFound() : Results.NoContent();
    }

    private static async Task<IResult> UploadImage(AppDbContext db, IWebHostEnvironment env, Guid id, IFormFile file, CancellationToken ct)
    {
        var recipe = await db.Recipes.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (recipe is null)
        {
            return Results.NotFound();
        }
        if (file.Length is 0 or > 5 * 1024 * 1024)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = ["Billedet skal være 1 byte–5 MB."] });
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (file.Length > 0 && !AllowedImageExtensions.Contains(ext))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = ["Kun jpg, png og webp er tilladt."] });
        }

        var dir = Path.Combine(env.ContentRootPath, ImageDirName);
        Directory.CreateDirectory(dir);
        var name = $"{Guid.NewGuid():N}{ext}";
        await using (var fs = File.Create(Path.Combine(dir, name)))
        {
            await file.CopyToAsync(fs, ct);
        }

        DeleteImageFile(env, recipe.ImageUploadedPath);
        recipe.ImageUploadedPath = $"/images/{name}";
        recipe.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { imagePath = recipe.ImageUploadedPath });
    }

    private static async Task<IResult> DeleteImage(AppDbContext db, IWebHostEnvironment env, Guid id, CancellationToken ct)
    {
        var recipe = await db.Recipes.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (recipe is null)
        {
            return Results.NotFound();
        }
        DeleteImageFile(env, recipe.ImageUploadedPath);
        recipe.ImageUploadedPath = null;
        recipe.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static void DeleteImageFile(IWebHostEnvironment env, string? path)
    {
        if (string.IsNullOrEmpty(path) || !path.StartsWith("/images/", StringComparison.Ordinal))
        {
            return;
        }
        var full = Path.Combine(env.ContentRootPath, ImageDirName, path["/images/".Length..]);
        if (File.Exists(full))
        {
            try { File.Delete(full); } catch (IOException) { /* leave orphan on failure */ }
        }
    }

    private static async Task<IResult> AddComment(AppDbContext db, Guid id, CommentInput input, CancellationToken ct)
    {
        var recipe = await db.Recipes.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (recipe is null)
        {
            return Results.NotFound();
        }
        if (string.IsNullOrWhiteSpace(input.Text))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["text"] = ["Kommentaren må ikke være tom."] });
        }

        var comment = new RecipeComment
        {
            RecipeId = id,
            Author = string.IsNullOrWhiteSpace(input.Author) ? "husholdning" : input.Author.Trim(),
            Text = input.Text.Trim(),
        };
        db.Comments.Add(comment);
        recipe.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/recipes/{id}", new CommentDto(comment.Id, comment.Author, comment.Text, comment.CreatedAt));
    }

    private static async Task<IResult> DeleteComment(AppDbContext db, Guid id, Guid commentId, CancellationToken ct)
    {
        var rows = await db.Comments.Where(c => c.Id == commentId && c.RecipeId == id).ExecuteDeleteAsync(ct);
        return rows == 0 ? Results.NotFound() : Results.NoContent();
    }

    private static async Task UpsertTags(AppDbContext db, Recipe recipe, IEnumerable<string>? tagNames, CancellationToken ct)
    {
        if (tagNames is null)
        {
            return;
        }
        foreach (var name in tagNames
                     .Where(t => !string.IsNullOrWhiteSpace(t))
                     .Select(t => t.Trim().ToLowerInvariant())
                     .Where(t => t.Length is > 1 and <= 60)
                     .Distinct(StringComparer.Ordinal))
        {
            var tag = await db.Tags.FirstOrDefaultAsync(t => t.Name == name, ct);
            if (tag is null)
            {
                tag = new Tag { Name = name };
                db.Tags.Add(tag);
            }
            recipe.Tags.Add(new RecipeTag { RecipeId = recipe.Id, TagId = tag.Id, Recipe = recipe, Tag = tag });
        }
    }

    private static RecipeDto ToDto(Recipe r) => new(
        r.Id,
        r.Title,
        r.Description,
        r.SourceUrl,
        r.ImageUploadedPath,
        r.ImageOriginalUrl,
        r.ImageUploadedPath ?? r.ImageOriginalUrl,
        r.Servings,
        r.PrepMinutes,
        r.CookMinutes,
        DeserializeIngredients(r.IngredientsJson),
        JsonDocument.Parse(r.StepsJson).RootElement.EnumerateArray()
            .Select(s => s.ToString())
            .ToList(),
        r.Tags.Select(rt => rt.Tag.Name).OrderBy(t => t).ToList(),
        r.IsSeasonalHighlight,
        r.Comments
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new CommentDto(c.Id, c.Author, c.Text, c.CreatedAt))
            .ToList(),
        r.CreatedAt,
        r.UpdatedAt);

    private static List<IngredientEntry> DeserializeIngredients(string json)
    {
        try
        {
            var parsed = JsonSerializer.Deserialize<List<IngredientEntry>>(json);
            return parsed ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
