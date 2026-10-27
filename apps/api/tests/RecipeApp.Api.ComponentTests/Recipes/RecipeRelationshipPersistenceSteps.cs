using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using RecipeApp.Api.ComponentTests.Auth;
using RecipeApp.Api.Infrastructure.Persistence;

namespace RecipeApp.Api.ComponentTests.Recipes;

[Binding]
public sealed class RecipeRelationshipPersistenceSteps(ApiAuthContext context)
{
    private Guid _ownerId;
    private Guid _otherOwnerId;
    private Guid _ingredientId;
    private int _rejectedWrites;

    [Given("a recipe and an ingredient owned by different users")]
    public async Task SeedCrossOwnerEntitiesAsync()
    {
        await using var scope = context.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var owner = RecipePersistenceTestData.CreateUser("recipe-owner");
        var otherOwner = RecipePersistenceTestData.CreateUser("recipe-other-owner");
        _ownerId = owner.Id;
        _otherOwnerId = otherOwner.Id;
        db.Users.AddRange(owner, otherOwner);
        var ingredient = new Ingredient
        {
            Id = Guid.CreateVersion7(),
            OwnerUserId = _otherOwnerId,
            Name = "Other User Ingredient",
            NormalizedName = "other user ingredient",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _ingredientId = ingredient.Id;
        db.Ingredients.Add(ingredient);
        await db.SaveChangesAsync();
    }

    [When("an ingredient item links across owners")]
    public async Task SaveCrossOwnerItemAsync()
    {
        var recipe = RecipePersistenceTestData.CreateRecipe(_ownerId, null);
        var step = CreateStep(_ownerId, 0);
        step.Items.Add(RecipePersistenceTestData.CreateIngredientItem(_ownerId, _ingredientId, 0));
        recipe.Steps.Add(step);
        await SaveAndAssertRejectedAsync(recipe);
    }

    [Then("the database rejects the relationship")]
    public void AssertCrossOwnerRejected() =>
        _rejectedWrites.Should().Be(1);

    [When("a structural item carries an ingredient reference")]
    public async Task SaveMismatchedStructuralItemAsync()
    {
        var recipe = RecipePersistenceTestData.CreateRecipe(_ownerId, null);
        var step = CreateStep(_ownerId, 0);
        step.Items.Add(new RecipeStepItem
        {
            Id = Guid.CreateVersion7(),
            OwnerUserId = _ownerId,
            Position = 0,
            Kind = RecipeStepItemKind.Heading,
            IngredientId = _ingredientId,
            Text = "Invalid heading"
        });
        recipe.Steps.Add(step);
        await SaveAndAssertRejectedAsync(recipe);
    }

    [Then("the database rejects the item kind and reference mismatch")]
    public void AssertKindMismatchRejected() =>
        _rejectedWrites.Should().Be(2);

    [When("an ordered child uses a negative position")]
    public async Task SaveNegativePositionAsync()
    {
        var recipe = RecipePersistenceTestData.CreateRecipe(_ownerId, null);
        recipe.Steps.Add(CreateStep(_ownerId, -1));
        await SaveAndAssertRejectedAsync(recipe);
    }

    [Then("the database rejects the invalid position")]
    public void AssertNegativePositionRejected() =>
        _rejectedWrites.Should().Be(3);

    [When("two steps use the same parent position")]
    public async Task SaveDuplicatePositionsAsync()
    {
        var recipe = RecipePersistenceTestData.CreateRecipe(_ownerId, null);
        recipe.Steps.Add(CreateStep(_ownerId, 0));
        recipe.Steps.Add(CreateStep(_ownerId, 0));
        await SaveAndAssertRejectedAsync(recipe);
    }

    [Then("the database rejects duplicate ordering")]
    public void AssertDuplicatePositionRejected() =>
        _rejectedWrites.Should().Be(4);

    private async Task SaveAndAssertRejectedAsync(Recipe recipe)
    {
        await using var scope = context.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Recipes.Add(recipe);
        await RecipePersistenceTestData.AssertWriteRejectedAsync(db);
        ++_rejectedWrites;
    }

    private static RecipeStep CreateStep(Guid ownerId, int position) => new()
    {
        Id = Guid.CreateVersion7(),
        OwnerUserId = ownerId,
        Position = position,
        Instructions = ""
    };
}
