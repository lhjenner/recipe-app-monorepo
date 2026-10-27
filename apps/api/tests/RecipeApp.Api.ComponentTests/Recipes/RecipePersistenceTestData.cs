using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RecipeApp.Api.Auth;
using RecipeApp.Api.Infrastructure.Persistence;

namespace RecipeApp.Api.ComponentTests.Recipes;

internal static class RecipePersistenceTestData
{
    public static User CreateUser(string name) => new()
    {
        Id = Guid.CreateVersion7(),
        Email = $"{name}-{Guid.NewGuid():N}@example.test",
        PasswordHash = "test-hash",
        CreatedAtUtc = DateTime.UtcNow
    };

    public static Recipe CreateRecipe(Guid ownerId, string? legacyId) => new()
    {
        Id = Guid.CreateVersion7(),
        OwnerUserId = ownerId,
        LegacyId = legacyId,
        Name = "Recipe",
        NormalizedName = "recipe",
        CreatedAtUtc = DateTime.UtcNow,
        UpdatedAtUtc = DateTime.UtcNow
    };

    public static RecipeStepItem CreateIngredientItem(Guid ownerId, Guid ingredientId, int position) => new()
    {
        Id = Guid.CreateVersion7(),
        OwnerUserId = ownerId,
        Position = position,
        Kind = RecipeStepItemKind.Ingredient,
        IngredientId = ingredientId,
        Text = "Tomato",
        QuantityText = "1/2",
        UnitText = "cup"
    };

    public static async Task AssertWriteRejectedAsync(AppDbContext db)
    {
        var act = () => db.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
        db.ChangeTracker.Clear();
    }
}
