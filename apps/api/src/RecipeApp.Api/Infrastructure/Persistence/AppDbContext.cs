using Microsoft.EntityFrameworkCore;
using RecipeApp.Api.Auth;
using RecipeApp.Api.Auth.PasswordReset;

namespace RecipeApp.Api.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<AuthSession> AuthSessions => Set<AuthSession>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<SupermarketChain> SupermarketChains => Set<SupermarketChain>();
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<IngredientStoreMapping> IngredientStoreMappings => Set<IngredientStoreMapping>();
    public DbSet<ProductLink> ProductLinks => Set<ProductLink>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
