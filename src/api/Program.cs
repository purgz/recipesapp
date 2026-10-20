using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using RecipeApp.Api.Data;
using RecipeApp.Api.Extraction;
using RecipeApp.Api.Services;

var builder = WebApplication.CreateBuilder(args);

Directory.CreateDirectory(Path.Combine(builder.Environment.ContentRootPath, "App_Data"));
Directory.CreateDirectory(Path.Combine(builder.Environment.ContentRootPath, "App_Data", "images"));
// UseStaticFiles throws when wwwroot is absent (fresh clone / after clean) — create it and only register if present.
Directory.CreateDirectory(Path.Combine(builder.Environment.ContentRootPath, "wwwroot"));

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Default")));

builder.Services
    .AddHttpClient("scrape", c =>
    {
        c.Timeout = TimeSpan.FromSeconds(20);
        c.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0 Safari/537.36");
        c.DefaultRequestHeaders.AcceptLanguage.ParseAdd("da-DK,da;q=0.9,en;q=0.8");
    });

builder.Services.AddScoped<ScrapeFetcher>();
builder.Services.AddScoped<IRecipeExtractor, SchemaOrgRecipeExtractor>();
builder.Services.AddScoped<IRecipeExtractor, MicrodataRecipeExtractor>();

// Optional last-resort LLM fallback; inert unless Groq:ApiKey is configured.
builder.Services.Configure<GroqOptions>(builder.Configuration.GetSection("Groq"));
builder.Services.AddHttpClient<LlmRecipeExtractor>((sp, http) =>
{
    var groq = sp.GetRequiredService<IOptions<GroqOptions>>().Value;
    http.BaseAddress = new Uri(groq.BaseUrl.TrimEnd('/') + "/");
    http.Timeout = TimeSpan.FromSeconds(groq.TimeoutSeconds);
    if (!string.IsNullOrWhiteSpace(groq.ApiKey))
    {
        http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", groq.ApiKey);
    }
});
builder.Services.AddScoped<IRecipeExtractor>(sp => sp.GetRequiredService<LlmRecipeExtractor>());

builder.Services.AddScoped<ExtractionPipeline>();
builder.Services.AddScoped<ShoppingListService>();

// The household password is the only credential — throttle brute-force attempts.
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddFixedWindowLimiter("login", opt =>
    {
        opt.PermitLimit = 10;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });
});

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

// Apply EF Core migrations on startup so a fresh volume/container works
// immediately (the deployed image has no dotnet-ef CLI available).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var migrationLog = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Migrations");
    for (var attempt = 1; ; attempt++)
    {
        try
        {
            db.Database.Migrate();
            break;
        }
        catch (Exception ex) when (attempt < 5)
        {
            migrationLog.LogWarning(ex, "Database migration attempt {Attempt} failed; retrying in 3s", attempt);
            await Task.Delay(TimeSpan.FromSeconds(3));
        }
    }
}

app.UseStaticFiles();

var imagesPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "images");
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(imagesPath),
    RequestPath = "/images",
});

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

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

    if (string.IsNullOrEmpty(req.Password) || !PasswordEquals(req.Password, password))
    {
        return Results.Unauthorized();
    }

    var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "household")],
        CookieAuthenticationDefaults.AuthenticationScheme);
    await ctx.SignInAsync(new ClaimsPrincipal(identity));
    return Results.NoContent();
}).AllowAnonymous().RequireRateLimiting("login");

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
app.MapPlanEndpoints();

app.MapGet("/api/tags", async (AppDbContext db, CancellationToken ct) =>
    Results.Ok(await db.Tags.Select(t => t.Name).OrderBy(n => n).ToListAsync(ct)));

app.MapFallback("/api/{**_}", () => Results.NotFound());
app.MapFallbackToFile("index.html").AllowAnonymous();

// Compare digests so the comparison is constant-time regardless of input length.
static bool PasswordEquals(string supplied, string expected) =>
    CryptographicOperations.FixedTimeEquals(
        SHA256.HashData(Encoding.UTF8.GetBytes(supplied)),
        SHA256.HashData(Encoding.UTF8.GetBytes(expected)));

app.Run();

public record LoginRequest(string Password);
public record ExtractUrlRequest(string Url);
