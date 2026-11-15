using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using RecipeApp.Api.Grocery;
using RecipeApp.Api.Infrastructure.Persistence;
using RecipeApp.Api.ComponentTests.Auth;
using RecipeApp.Api.ComponentTests.Recipes;

namespace RecipeApp.Api.ComponentTests.Grocery;

[Binding]
public sealed class GroceryLongSourceTextSteps(ApiAuthContext context)
{
    private Guid _ownerId;
    private Guid _recipeId;
    private Guid _ingredientId;
    private string _connectionString = string.Empty;
    private string _longRecipeName = string.Empty;
    private string _longUnitText = string.Empty;

    [Given("an owner with a long-named recipe and grocery list")]
    public async Task SeedLongRecipeAndGroceryListAsync()
    {
        await using var scope = context.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _connectionString = db.Database.GetConnectionString()!;
        var owner = RecipePersistenceTestData.CreateUser("grocery-long-recipe");
        _ownerId = owner.Id;
        _recipeId = Guid.CreateVersion7();
        _ingredientId = Guid.CreateVersion7();
        _longRecipeName = new string('R', 501);
        _longUnitText = new string('u', 500);
        var now = DateTime.UtcNow;

        db.Users.Add(owner);
        db.Recipes.Add(new Recipe
        {
            Id = _recipeId,
            OwnerUserId = _ownerId,
            Name = _longRecipeName,
            NormalizedName = _longRecipeName.ToLowerInvariant(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        db.Ingredients.Add(new Ingredient
        {
            Id = _ingredientId,
            OwnerUserId = _ownerId,
            Name = "Synthetic ingredient",
            NormalizedName = "synthetic ingredient",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        db.GroceryLists.Add(new GroceryList
        {
            OwnerUserId = _ownerId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        await db.SaveChangesAsync();
    }

    [When("an unresolved recipe contribution with a long unit is persisted")]
    public async Task PersistLongRecipeContributionAsync()
    {
        await using var db = OpenDatabase();
        var listId = await db.GroceryLists
            .Where(list => list.OwnerUserId == _ownerId)
            .Select(list => list.Id)
            .SingleAsync();
        var now = DateTime.UtcNow;
        var addition = new GroceryRecipeAddition
        {
            GroceryListId = listId,
            OwnerUserId = _ownerId,
            RecipeId = _recipeId,
            RecipeNameSnapshot = _longRecipeName,
            RecipeVersion = 1,
            AddedAtUtc = now
        };
        var item = new GroceryItem
        {
            GroceryListId = listId,
            OwnerUserId = _ownerId,
            IngredientId = _ingredientId,
            NameSnapshot = "Synthetic ingredient",
            NormalizedName = "synthetic ingredient",
            ConsolidationKey = GroceryConsolidationKey.ForUnresolvedIngredient(
                _ingredientId,
                _longUnitText),
            PurchaseUnit = "item",
            State = GroceryItemState.Outstanding,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        var contribution = new GroceryContribution
        {
            GroceryListId = listId,
            GroceryItemId = item.Id,
            OwnerUserId = _ownerId,
            Origin = GroceryContributionOrigin.Recipe,
            RecipeAdditionId = addition.Id,
            OriginalNameText = "Synthetic ingredient",
            OriginalAmountText = "1",
            OriginalUnitText = _longUnitText,
            UnresolvedReason = "The unit is not supported.",
            SourceRecipeId = _recipeId,
            SourceRecipeNameSnapshot = _longRecipeName,
            State = GroceryContributionState.Outstanding,
            CreatedAtUtc = now
        };

        db.AddRange(addition, item, contribution);
        await db.SaveChangesAsync();
    }

    [Then("the full recipe name and unit are retained with a bounded key")]
    public async Task AssertLongRecipeContributionIsPreservedAsync()
    {
        await using var db = OpenDatabase();
        (await db.GroceryRecipeAdditions.SingleAsync())
            .RecipeNameSnapshot.Should().Be(_longRecipeName);
        var contribution = await db.GroceryContributions.SingleAsync();
        contribution.OriginalUnitText.Should().Be(_longUnitText);
        contribution.SourceRecipeNameSnapshot.Should().Be(_longRecipeName);
        (await db.GroceryItems.SingleAsync()).ConsolidationKey.Should().HaveLength(64);
    }

    private AppDbContext OpenDatabase() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_connectionString)
            .Options);
}
