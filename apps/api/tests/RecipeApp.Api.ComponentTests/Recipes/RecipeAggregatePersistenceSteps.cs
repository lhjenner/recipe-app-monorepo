using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using RecipeApp.Api.Auth;
using RecipeApp.Api.ComponentTests.Auth;
using RecipeApp.Api.Infrastructure.Persistence;

namespace RecipeApp.Api.ComponentTests.Recipes;

[Binding]
public sealed class RecipeAggregatePersistenceSteps(ApiAuthContext context)
{
    private Guid _ingredientId;
    private Guid _recipeId;
    private User? _owner;
    private Ingredient? _ingredient;
    private Recipe? _recipe;

    [Given("a recipe owner, an archived ingredient, and a recipe with ordered content")]
    public Task SeedRecipeAggregateAsync()
    {
        var owner = RecipePersistenceTestData.CreateUser("recipe-owner");
        _owner = owner;
        var ingredient = new Ingredient
        {
            Id = Guid.CreateVersion7(),
            OwnerUserId = owner.Id,
            Name = "Archived Tomato",
            NormalizedName = "archived tomato",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            ArchivedAtUtc = DateTime.UtcNow
        };
        _ingredient = ingredient;
        _ingredientId = ingredient.Id;
        var recipe = RecipePersistenceTestData.CreateRecipe(owner.Id, "Legacy-Recipe-01");
        _recipe = recipe;
        _recipeId = recipe.Id;
        recipe.Name = "Tomato Soup";
        recipe.NormalizedName = "tomato soup";
        recipe.ServingsText = "2-3";
        recipe.PrepTimeText = "about 10 minutes";
        recipe.DateAddedAtUtc = DateTime.SpecifyKind(
            DateTime.Parse("2024-01-02T03:04:05"),
            DateTimeKind.Utc);

        var firstStep = new RecipeStep
        {
            Id = Guid.CreateVersion7(),
            OwnerUserId = owner.Id,
            Position = 0,
            Instructions = "First line\r\nSecond line"
        };
        firstStep.Items.Add(new RecipeStepItem
        {
            Id = Guid.CreateVersion7(),
            OwnerUserId = owner.Id,
            Position = 0,
            Kind = RecipeStepItemKind.Heading,
            Text = "For the base"
        });
        firstStep.Items.Add(RecipePersistenceTestData.CreateIngredientItem(owner.Id, ingredient.Id, 1));
        firstStep.Items.Add(RecipePersistenceTestData.CreateIngredientItem(owner.Id, ingredient.Id, 2));
        recipe.Steps.Add(firstStep);
        recipe.Steps.Add(new RecipeStep
        {
            Id = Guid.CreateVersion7(),
            OwnerUserId = owner.Id,
            Position = 1,
            Instructions = ""
        });
        recipe.Tags.Add(new RecipeTag { Id = Guid.CreateVersion7(), Position = 0, Text = "Dinner" });
        recipe.Tags.Add(new RecipeTag { Id = Guid.CreateVersion7(), Position = 1, Text = "Dinner" });
        recipe.Image = new RecipeImage
        {
            ObjectKey = "recipes/opaque-key",
            ContentType = "image/jpeg",
            ByteSize = 1024,
            Sha256 = new string('a', 64),
            Width = 640,
            Height = 480
        };
        recipe.ImportProvenance = new RecipeImportProvenance
        {
            SourceDocumentJson = """{"unknown":{"retained":true}}""",
            DocumentSha256 = new string('b', 64),
            SourceFileSha256 = new string('c', 64),
            MappingVersion = "1",
            DecisionFingerprint = new string('d', 64),
            LastAppliedRunId = Guid.CreateVersion7(),
            LastAppliedAtUtc = DateTime.UtcNow
        };
        return Task.CompletedTask;
    }

    [When("the recipe aggregate is saved and reloaded")]
    public async Task ReloadRecipeAsync()
    {
        await using var scope = context.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Users.Add(_owner!);
        db.Ingredients.Add(_ingredient!);
        db.Recipes.Add(_recipe!);
        await db.SaveChangesAsync();
    }

    [Then("recipe text, ordering, repeated ingredients, and optional metadata are preserved")]
    public async Task AssertRecipeAggregateAsync()
    {
        await using var scope = context.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var recipe = await db.Recipes.AsNoTracking()
            .Include(row => row.Steps).ThenInclude(step => step.Items)
            .Include(row => row.Tags)
            .Include(row => row.Image)
            .Include(row => row.ImportProvenance)
            .SingleAsync(row => row.Id == _recipeId);

        recipe.Id.Version.Should().Be(7);
        recipe.LegacyId.Should().Be("Legacy-Recipe-01");
        recipe.ServingsText.Should().Be("2-3");
        recipe.PrepTimeText.Should().Be("about 10 minutes");
        recipe.DateAddedAtUtc.Should().Be(DateTime.SpecifyKind(
            DateTime.Parse("2024-01-02T03:04:05"),
            DateTimeKind.Utc));
        recipe.Steps.OrderBy(step => step.Position).Select(step => step.Instructions)
            .Should().Equal("First line\r\nSecond line", "");
        var items = recipe.Steps.Single(step => step.Position == 0).Items.OrderBy(item => item.Position).ToArray();
        items.Select(item => item.Kind).Should().Equal(
            RecipeStepItemKind.Heading,
            RecipeStepItemKind.Ingredient,
            RecipeStepItemKind.Ingredient);
        items.Skip(1).Select(item => item.IngredientId).Should().Equal(_ingredientId, _ingredientId);
        items.Skip(1).Select(item => item.QuantityText).Should().Equal("1/2", "1/2");
        recipe.Tags.OrderBy(tag => tag.Position).Select(tag => tag.Text).Should().Equal("Dinner", "Dinner");
        recipe.Image.Should().Match<RecipeImage>(image =>
            image.ObjectKey == "recipes/opaque-key" && image.Width == 640 && image.Height == 480);
        using var source = JsonDocument.Parse(recipe.ImportProvenance!.SourceDocumentJson);
        source.RootElement.GetProperty("unknown").GetProperty("retained").GetBoolean().Should().BeTrue();

        var ingredient = await db.Ingredients.AsNoTracking().SingleAsync(row => row.Id == _ingredientId);
        ingredient.ArchivedAtUtc.Should().NotBeNull();
    }
}
