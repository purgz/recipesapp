using Microsoft.EntityFrameworkCore;

namespace RecipeApp.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<RecipeComment> Comments => Set<RecipeComment>();
    public DbSet<MealPlanEntry> MealPlanEntries => Set<MealPlanEntry>();
    public DbSet<ShoppingList> ShoppingLists => Set<ShoppingList>();
    public DbSet<ShoppingListItem> ShoppingListItems => Set<ShoppingListItem>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Recipe>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.Title).HasMaxLength(300).IsRequired();
            e.Property(r => r.SourceUrl).HasMaxLength(2000);
            e.Property(r => r.ImageOriginalUrl).HasMaxLength(2000);
            e.HasIndex(r => r.Title);
        });

        b.Entity<Tag>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Name).HasMaxLength(60).IsRequired();
            e.HasIndex(t => t.Name).IsUnique();
        });

        b.Entity<RecipeTag>(e =>
        {
            e.HasKey(rt => new { rt.RecipeId, rt.TagId });
            e.HasOne(rt => rt.Recipe).WithMany(r => r.Tags).HasForeignKey(rt => rt.RecipeId);
            e.HasOne(rt => rt.Tag).WithMany(t => t.Recipes).HasForeignKey(rt => rt.TagId);
        });

        b.Entity<RecipeComment>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Author).HasMaxLength(100);
            e.Property(c => c.Text).HasMaxLength(5000).IsRequired();
            e.HasOne(c => c.Recipe).WithMany(r => r.Comments).HasForeignKey(c => c.RecipeId);
        });

        b.Entity<MealPlanEntry>(e =>
        {
            e.HasKey(m => m.Id);
            e.HasIndex(m => new { m.Date, m.Slot }).IsUnique();
            e.HasOne(m => m.Recipe).WithMany().HasForeignKey(m => m.RecipeId);
        });

        b.Entity<ShoppingList>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasIndex(s => s.WeekStart).IsUnique();
        });

        b.Entity<ShoppingListItem>(e =>
        {
            e.HasKey(i => i.Id);
            e.Property(i => i.Name).HasMaxLength(300).IsRequired();
            e.HasOne(i => i.ShoppingList).WithMany(l => l.Items).HasForeignKey(i => i.ShoppingListId);
            e.HasIndex(i => new { i.ShoppingListId, i.Name, i.Unit }).IsUnique();
        });
    }
}
