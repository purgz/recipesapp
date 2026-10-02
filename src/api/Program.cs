using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using RecipeApp.Api.Data;

var builder = WebApplication.CreateBuilder(args);

Directory.CreateDirectory(Path.Combine(builder.Environment.ContentRootPath, "App_Data"));

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Default")));

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
    });
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

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
});

app.MapPost("/api/auth/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync();
    return Results.NoContent();
});

app.MapGet("/api/auth/me", (ClaimsPrincipal user) =>
    Results.Ok(new { authenticated = user.Identity?.IsAuthenticated == true }));

app.MapFallback("/api/{**_}", () => Results.NotFound());
app.MapFallbackToFile("index.html");

app.Run();

public record LoginRequest(string Password);
