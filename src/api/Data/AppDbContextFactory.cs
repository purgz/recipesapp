using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RecipeApp.Api.Data;

/// <summary>
/// Lets `dotnet ef` build migrations without spinning up the web host
/// (mandatory: CreateBuilder throws DirectoryNotFound when wwwroot is absent).
/// The physical DB path here is irrelevant at design time — only the model is.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=App_Data/app.db")
            .Options;
        return new AppDbContext(options);
    }
}
