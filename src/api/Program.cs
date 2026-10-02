using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using RecipeApp.Api.Data;
using RecipeApp.Api.Extraction;
using RecipeApp.Api.Services;

var builder = WebApplication.CreateBuilder(args);

Directory.CreateDirectory(Path.Combine(builder.Environment.ContentRootPath, "App_Data"));
Directory.CreateDirectory(Path.Combine(builder.Environment.ContentRootPath, "App_Data", "images"));

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddOptions<ExtractLlmOptions>()
    .BindConfiguration("ExtractLlm");

builder.Services
    .AddHttpClient("scrape", c =>
    {
        c.Timeout = TimeSpan.FromSeconds(20);
        c.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0 Safari/537.36");
        c.DefaultRequestHeaders.AcceptLanguage.ParseAdd("da-DK,da;q=0.9,en;q=0.8");
    });

builder.Services.AddScoped<IRecipeExtractor, YouTubeRecipeExtractor>();
builder.Services.AddScoped<IRecipeExtractor, SchemaOrgRecipeExtractor>();
builder.Services.AddScoped<IRecipeExtractor, MicrodataRecipeExtractor>();
builder.Services.AddScoped<ExtractionPipeline>();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "recipeapp.auth";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        o.ExpireTimeSpan = TimeSpan.FromDays(365);
        o.SlidingExpiration = true;
        // SPA owns navigation — API calls must get 401/403, never a redirect to /Account/Login.
        o.Events.OnRedirectToLogin = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        o.Events.OnRedirectToAccessDenied = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });

// Everything requires the household cookie by default; opt out with AllowAnonymous.
builder.Services.AddAuthorization(o =>
    o.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

var app = builder.Build();

app.UseStaticFiles();

var imagesPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "images");
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(imagesPath),
    RequestPath = "/images",
});

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

app.MapPost("/api/auth/login", async (HttpContext ctx, LoginRequest req, IConfiguration cfg) =>
{
    var password = cfg["Household:Password"];
    if (string.IsNullOrEmpty(password))
    {
        return Results.Problem(
            title: "Household password not configured",
            detail: "Set Household:Password via dotnet user-secrets (dev) or environment/appsettings (prod).",
            statusCode: 500);
    }

    if (string.IsNullOrEmpty(req.Password) || !string.Equals(req.Password, password, StringComparison.Ordinal))
    {
        return Results.Unauthorized();
    }

    var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "household")],
        CookieAuthenticationDefaults.AuthenticationScheme);
    await ctx.SignInAsync(new ClaimsPrincipal(identity));
    return Results.NoContent();
}).AllowAnonymous();

app.MapPost("/api/auth/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync();
    return Results.NoContent();
});

app.MapGet("/api/auth/me", (ClaimsPrincipal user) =>
    Results.Ok(new { authenticated = user.Identity?.IsAuthenticated == true })).AllowAnonymous();

app.MapPost("/api/extract", async (ExtractUrlRequest req, ExtractionPipeline pipeline, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Url))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["url"] = ["URL skal angives."] });
    }
    var draft = await pipeline.ExtractAsync(req.Url, ct);
    return Results.Ok(draft);
});

app.MapRecipeEndpoints();

app.MapGet("/api/tags", async (AppDbContext db, CancellationToken ct) =>
    Results.Ok(await db.Tags.Select(t => t.Name).OrderBy(n => n).ToListAsync(ct)));

app.MapFallback("/api/{**_}", () => Results.NotFound());
app.MapFallbackToFile("index.html").AllowAnonymous();

app.Run();

public record LoginRequest(string Password);
public record ExtractUrlRequest(string Url);
