using System.Net;
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
public sealed class RecipeDetailSteps(ApiAuthContext context)
{
    private Guid _recipeId;
    private readonly List<Guid> _inaccessibleIds = [];
    private readonly List<HttpResponseMessage> _inaccessibleResponses = [];

    [Given("the user owns a recipe with ordered detail content")]
    public async Task SeedDetailedRecipeAsync()
    {
        await using var scope = context.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var owner = await CurrentOwnerAsync(db);
        var ingredient = new Ingredient
        {
            Id = Guid.CreateVersion7(),
            OwnerUserId = owner.Id,
            Name = "Current Tomato",
            NormalizedName = "current tomato",
            ExcludeFromGroceryList = true,
            ArchivedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        var recipe = CreateRecipe(owner.Id, "Ordered Recipe", archived: false);
        recipe.SourceText = "Family recipe";
        recipe.SourceUrl = "https://example.test/recipe";
        recipe.ServingsText = "2-3";
        recipe.PrepTimeText = "about 10 minutes";
        recipe.CookTimeText = "1/2 hour";
        recipe.DateAddedAtUtc = DateTime.SpecifyKind(
            DateTime.Parse("2024-01-02T03:04:05"),
            DateTimeKind.Utc);
        recipe.Tags.Add(new RecipeTag { Position = 1, Text = "Dinner" });
        recipe.Tags.Add(new RecipeTag { Position = 0, Text = "Quick" });
        recipe.Tags.Add(new RecipeTag { Position = 2, Text = "Dinner" });

        var laterStep = new RecipeStep
        {
            OwnerUserId = owner.Id,
            Position = 1,
            Instructions = "Second step"
        };
        var firstStep = new RecipeStep
        {
            OwnerUserId = owner.Id,
            Position = 0,
            Instructions = "First line\r\nSecond line"
        };
        firstStep.Items.Add(new RecipeStepItem
        {
            OwnerUserId = owner.Id,
            Position = 2,
            Kind = RecipeStepItemKind.Note,
            Text = "Rest gently",
            QuantityText = "a little",
            NotesText = "keep covered"
        });
        firstStep.Items.Add(new RecipeStepItem
        {
            OwnerUserId = owner.Id,
            Position = 0,
            Kind = RecipeStepItemKind.Heading,
            Text = "For the base"
        });
        firstStep.Items.Add(new RecipeStepItem
        {
            OwnerUserId = owner.Id,
            Position = 1,
            Kind = RecipeStepItemKind.Ingredient,
            IngredientId = ingredient.Id,
            Text = "Old Tomato Snapshot",
            QuantityText = "1/2",
            UnitText = "cup",
            NotesText = "diced"
        });
        recipe.Steps.Add(laterStep);
        recipe.Steps.Add(firstStep);
        recipe.Image = new RecipeImage
        {
            ObjectKey = "private/opaque-recipe-image",
            ContentType = "image/png",
            ByteSize = 128,
            Sha256 = new string('a', 64),
            Width = 640,
            Height = 480
        };
        db.AddRange(ingredient, recipe);
        await db.SaveChangesAsync();
        _recipeId = recipe.Id;
    }

    [Given("the user owns an active recipe named {string} without an image")]
    public async Task SeedRecipeWithoutImageAsync(string name)
    {
        await using var scope = context.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var owner = await CurrentOwnerAsync(db);
        var recipe = CreateRecipe(owner.Id, name, archived: false);
        db.Recipes.Add(recipe);
        await db.SaveChangesAsync();
        _recipeId = recipe.Id;
    }

    [Given("there are active recipes owned by another user and an archived recipe owned by me")]
    public async Task SeedInaccessibleRecipesAsync()
    {
        await using var scope = context.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var owner = await CurrentOwnerAsync(db);
        var otherOwner = RecipePersistenceTestData.CreateUser("detail-other-owner");
        var archived = CreateRecipe(owner.Id, "Archived Detail", archived: true);
        var other = CreateRecipe(otherOwner.Id, "Private Detail", archived: false);
        db.AddRange(otherOwner, archived, other);
        await db.SaveChangesAsync();
        _inaccessibleIds.Add(Guid.CreateVersion7());
        _inaccessibleIds.Add(archived.Id);
        _inaccessibleIds.Add(other.Id);
    }

    [When("I request that recipe's detail")]
    public async Task RequestRecipeDetailAsync() =>
        context.Response = await context.Client.GetAsync($"/api/recipes/{_recipeId}");

    [When("I request a recipe detail without signing in")]
    public async Task RequestUnauthenticatedDetailAsync() =>
        context.Response = await context.Client.GetAsync($"/api/recipes/{Guid.CreateVersion7()}");

    [When("I request the inaccessible recipe details")]
    public async Task RequestInaccessibleRecipeDetailsAsync()
    {
        foreach (var id in _inaccessibleIds)
        {
            _inaccessibleResponses.Add(await context.Client.GetAsync($"/api/recipes/{id}"));
        }
    }

    [When("I request recipe detail using the legacy ID {string}")]
    public async Task RequestLegacyIdAsync(string legacyId) =>
        context.Response = await context.Client.GetAsync($"/api/recipes/{Uri.EscapeDataString(legacyId)}");

    [Then("the recipe detail contains ordered metadata, tags, steps, and typed items")]
    public async Task AssertRecipeDetailAsync()
    {
        context.Response!.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = await ReadResponseAsync();
        var detail = document.RootElement;
        detail.GetProperty("id").GetGuid().Should().Be(_recipeId);
        detail.GetProperty("id").GetString()![14].Should().Be('7');
        detail.GetProperty("name").GetString().Should().Be("Ordered Recipe");
        detail.GetProperty("sourceText").GetString().Should().Be("Family recipe");
        detail.GetProperty("sourceUrl").GetString().Should().Be("https://example.test/recipe");
        detail.GetProperty("servingsText").GetString().Should().Be("2-3");
        detail.GetProperty("prepTimeText").GetString().Should().Be("about 10 minutes");
        detail.GetProperty("cookTimeText").GetString().Should().Be("1/2 hour");
        detail.GetProperty("dateAddedAtUtc").GetDateTime().Should()
            .Be(DateTime.Parse("2024-01-02T03:04:05Z").ToUniversalTime());
        detail.TryGetProperty("legacyId", out _).Should().BeFalse();
        detail.TryGetProperty("ownerUserId", out _).Should().BeFalse();
        detail.TryGetProperty("importProvenance", out _).Should().BeFalse();

        detail.GetProperty("tags").EnumerateArray()
            .Select(tag => (tag.GetProperty("position").GetInt32(), tag.GetProperty("text").GetString()))
            .Should().Equal((0, "Quick"), (1, "Dinner"), (2, "Dinner"));
        var steps = detail.GetProperty("steps").EnumerateArray().ToArray();
        steps.Select(step => step.GetProperty("position").GetInt32()).Should().Equal(0, 1);
        steps.Select(step => step.GetProperty("instructions").GetString())
            .Should().Equal("First line\r\nSecond line", "Second step");
        var items = steps[0].GetProperty("items").EnumerateArray().ToArray();
        items.Select(item => item.GetProperty("position").GetInt32()).Should().Equal(0, 1, 2);
        items.Select(item => item.GetProperty("kind").GetString())
            .Should().Equal("heading", "ingredient", "note");
        items[0].GetProperty("text").GetString().Should().Be("For the base");
        items[1].GetProperty("text").GetString().Should().Be("Old Tomato Snapshot");
        items[1].GetProperty("ingredientName").GetString().Should().Be("Current Tomato");
        items[1].GetProperty("quantityText").GetString().Should().Be("1/2");
        items[1].GetProperty("unitText").GetString().Should().Be("cup");
        items[1].GetProperty("notesText").GetString().Should().Be("diced");
        items[2].GetProperty("quantityText").GetString().Should().Be("a little");
    }

    [Then("the recipe detail exposes only safe application image metadata")]
    public async Task AssertSafeImageMetadataAsync()
    {
        using var document = await ReadResponseAsync();
        var image = document.RootElement.GetProperty("image");
        image.GetProperty("url").GetString().Should().Be($"/api/recipes/{_recipeId}/image");
        image.GetProperty("width").GetInt32().Should().Be(640);
        image.GetProperty("height").GetInt32().Should().Be(480);
        image.GetProperty("sha256").GetString().Should().Be(new string('a', 64));
        image.TryGetProperty("objectKey", out _).Should().BeFalse();
        image.TryGetProperty("originalUrl", out _).Should().BeFalse();
    }

    [Then("the recipe detail image is null")]
    public async Task AssertNoImageAsync()
    {
        context.Response!.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = await ReadResponseAsync();
        document.RootElement.GetProperty("image").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Then("the recipe detail response is unauthorized")]
    public void AssertUnauthorized() =>
        context.Response!.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Then("every recipe detail response is not found")]
    public void AssertInaccessibleResponses()
    {
        _inaccessibleResponses.Should().HaveCount(3);
        _inaccessibleResponses.Select(response => response.StatusCode)
            .Should().OnlyContain(status => status == HttpStatusCode.NotFound);
        foreach (var response in _inaccessibleResponses)
        {
            response.Dispose();
        }
    }

    [Then("the recipe detail response is not found")]
    public void AssertNotFound() =>
        context.Response!.StatusCode.Should().Be(HttpStatusCode.NotFound);

    private async Task<JsonDocument> ReadResponseAsync() =>
        JsonDocument.Parse(await context.Response!.Content.ReadAsStringAsync());

    private async Task<User> CurrentOwnerAsync(AppDbContext db) =>
        await db.Users.AsNoTracking().SingleAsync(user =>
            user.Email == context.Email.Trim().ToLowerInvariant());

    private static Recipe CreateRecipe(Guid ownerUserId, string name, bool archived) =>
        new()
        {
            OwnerUserId = ownerUserId,
            Name = name,
            NormalizedName = name.ToLowerInvariant(),
            IsArchived = archived,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
}
