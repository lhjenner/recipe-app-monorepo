using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using RecipeApp.Api.ComponentTests.Auth;
using RecipeApp.Api.Grocery;
using RecipeApp.Api.Infrastructure.Persistence;

namespace RecipeApp.Api.ComponentTests.Grocery;

[Binding]
public sealed class GroceryRecipeAdditionSteps(ApiAuthContext context)
{
    private Guid _recipeId;
    private Guid _ingredientId;
    private Guid _mushroomId;
    private uint _recipeVersion;
    private uint _listVersion;
    private Guid[] _sourceItemIds = [];

    [Given("an active Recipe with eligible, excluded, archived, and non-Ingredient items")]
    public async Task SeedMixedRecipeAsync()
    {
        await using var scope = context.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var owner = await GroceryRecipeTestData.CurrentOwnerAsync(context, db);
        var now = DateTime.UtcNow;
        var tomatoes = GroceryRecipeTestData.CreateIngredient(owner.Id, "Tomato", now, archived: true);
        var excluded = GroceryRecipeTestData.CreateIngredient(owner.Id, "Salt", now, excluded: true);
        var mushrooms = GroceryRecipeTestData.CreateIngredient(owner.Id, "Mushroom", now);
        var recipe = GroceryRecipeTestData.CreateRecipe(owner.Id, "Tomato and Mushroom Bowl", now);
        var step = GroceryRecipeTestData.CreateStep(owner.Id, 0);
        var tomatoTablespoon = GroceryRecipeTestData.CreateIngredientItem(
            owner.Id, tomatoes.Id, 0, "Sliced tomato", "1", "tbsp");
        var tomatoTeaspoons = GroceryRecipeTestData.CreateIngredientItem(
            owner.Id, tomatoes.Id, 1, "Tomato", "2", "tsp");
        var excludedItem = GroceryRecipeTestData.CreateIngredientItem(
            owner.Id, excluded.Id, 2, "Salt", "1", "tsp");
        var mushroomCount = GroceryRecipeTestData.CreateIngredientItem(
            owner.Id, mushrooms.Id, 3, "Mushrooms", "5", "mushrooms");
        var mushroomWeight = GroceryRecipeTestData.CreateIngredientItem(
            owner.Id, mushrooms.Id, 4, "Mushroom", "250", "g");
        step.Items.Add(GroceryRecipeTestData.CreateTypedItem(owner.Id, 5, RecipeStepItemKind.Heading, "For serving"));
        step.Items.Add(GroceryRecipeTestData.CreateTypedItem(owner.Id, 6, RecipeStepItemKind.Note, "Season to taste"));
        step.Items.Add(tomatoTablespoon);
        step.Items.Add(tomatoTeaspoons);
        step.Items.Add(excludedItem);
        step.Items.Add(mushroomCount);
        step.Items.Add(mushroomWeight);
        recipe.Steps.Add(step);
        db.AddRange(tomatoes, excluded, mushrooms, recipe);
        await db.SaveChangesAsync();

        _recipeId = recipe.Id;
        _ingredientId = tomatoes.Id;
        _mushroomId = mushrooms.Id;
        _recipeVersion = recipe.Version;
        _sourceItemIds = [tomatoTablespoon.Id, tomatoTeaspoons.Id];
    }

    [Given("an active Recipe with countable onion demand")]
    public async Task SeedOnionRecipeAsync()
    {
        await using var scope = context.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var owner = await GroceryRecipeTestData.CurrentOwnerAsync(context, db);
        var now = DateTime.UtcNow;
        var onion = GroceryRecipeTestData.CreateIngredient(owner.Id, "Onion", now);
        var recipe = GroceryRecipeTestData.CreateRecipe(owner.Id, "Onion Soup", now);
        var step = GroceryRecipeTestData.CreateStep(owner.Id, 0);
        var item = GroceryRecipeTestData.CreateIngredientItem(owner.Id, onion.Id, 0, "Onion", "2", "onions");
        step.Items.Add(item);
        recipe.Steps.Add(step);
        db.AddRange(onion, recipe);
        await db.SaveChangesAsync();

        _recipeId = recipe.Id;
        _ingredientId = onion.Id;
        _recipeVersion = recipe.Version;
        _sourceItemIds = [item.Id];
    }

    [When("I preview the Recipe for grocery addition")]
    public async Task PreviewRecipeAsync()
    {
        context.Response?.Dispose();
        context.Response = await context.Client.GetAsync(
            $"/api/grocery-list/recipes/{_recipeId}/preview");
        context.Response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var preview = JsonDocument.Parse(await context.Response.Content.ReadAsStringAsync());
        _recipeVersion = preview.RootElement.GetProperty("recipeVersion").GetUInt32();
        _listVersion = preview.RootElement.GetProperty("listVersion").GetUInt32();
    }

    [Given("an active Recipe with one unitless apple")]
    public async Task SeedUnitlessAppleAsync()
    {
        await SeedOnionRecipeAsync();
        await using var scope = context.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ingredient = await db.Ingredients.SingleAsync(value => value.Id == _ingredientId);
        ingredient.Name = "Apple";
        ingredient.NormalizedName = "apple";
        var source = await db.RecipeStepItems.SingleAsync(value => value.Id == _sourceItemIds[0]);
        source.Text = "Apple";
        source.QuantityText = "1";
        source.UnitText = null;
        source.NotesText = "(cubed)";
        await db.SaveChangesAsync();
    }

    [Then("two apples are required without changing the original source text")]
    public async Task AssertUnitlessAppleCountAsync()
    {
        context.Response!.StatusCode.Should().Be(HttpStatusCode.Created);
        using var list = await GroceryRecipeTestData.ReadListAsync(context);
        var item = list.RootElement.GetProperty("items").EnumerateArray().Single();
        item.GetProperty("outstandingPurchaseQuantity").GetDecimal().Should().Be(2);
        item.GetProperty("requirementAmount").GetDecimal().Should().Be(2);
        var contributions = item.GetProperty("contributions").EnumerateArray().ToArray();
        contributions.Should().HaveCount(2);
        foreach (var contribution in contributions)
        {
            contribution.GetProperty("originalAmountText").GetString().Should().Be("1");
            contribution.GetProperty("originalUnitText").ValueKind.Should().Be(JsonValueKind.Null);
            contribution.GetProperty("originalNotesText").GetString().Should().Be("(cubed)");
            contribution.GetProperty("unresolvedReason").ValueKind.Should().Be(JsonValueKind.Null);
        }
    }

    [Then("the preview includes only eligible source items and makes no writes")]
    public async Task AssertRecipePreviewAsync()
    {
        using var preview = JsonDocument.Parse(await context.Response!.Content.ReadAsStringAsync());
        var root = preview.RootElement;
        var included = root.GetProperty("included").EnumerateArray().ToArray();
        included.Should().HaveCount(4);
        included.Should().Contain(item => item.GetProperty("ingredientIsArchived").GetBoolean());
        included.Select(item => item.GetProperty("recipeStepItemId").GetGuid())
            .Should().Contain(_sourceItemIds);
        included.Where(item => item.GetProperty("ingredientId").GetGuid() == _ingredientId)
            .Select(item => item.GetProperty("originalAmountText").GetString())
            .Should().Equal("1", "2");
        included.Where(item => item.GetProperty("ingredientId").GetGuid() == _ingredientId)
            .Select(item => item.GetProperty("originalNameText").GetString())
            .Should().Equal("Sliced tomato", "Tomato");
        root.GetProperty("excluded").GetArrayLength().Should().Be(3);

        using var list = await GroceryRecipeTestData.ReadListAsync(context);
        list.RootElement.GetProperty("id").ValueKind.Should().Be(JsonValueKind.Null);
        list.RootElement.GetProperty("items").GetArrayLength().Should().Be(0);
    }

    [When("I add the reviewed Recipe")]
    public Task AddReviewedRecipeAsync() =>
        GroceryRecipeTestData.AddRecipeAsync(context, _recipeId, _listVersion, _recipeVersion, confirmRepeat: false);

    [Then("the preview reports outstanding Recipe demand")]
    public async Task AssertOutstandingRecipeDemandAsync()
    {
        using var preview = JsonDocument.Parse(await context.Response!.Content.ReadAsStringAsync());
        preview.RootElement.GetProperty("hasOutstandingAddition").GetBoolean().Should().BeTrue();
    }

    [When("I try to add the Recipe again without confirmation")]
    public Task TryUnconfirmedRepeatAsync() =>
        GroceryRecipeTestData.AddRecipeAsync(context, _recipeId, _listVersion, _recipeVersion, confirmRepeat: false);

    [Then("the repeat attempt is rejected without changing demand")]
    public async Task AssertUnconfirmedRepeatRejectedAsync()
    {
        context.Response!.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var list = await GroceryRecipeTestData.ReadListAsync(context);
        list.RootElement.GetProperty("items")[0]
            .GetProperty("outstandingPurchaseQuantity").GetDecimal().Should().Be(2);
        await using var scope = context.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.GroceryRecipeAdditions.CountAsync()).Should().Be(1);
    }

    [When("I confirm adding another Recipe's worth")]
    public Task ConfirmRecipeRepeatAsync() =>
        GroceryRecipeTestData.AddRecipeAsync(context, _recipeId, _listVersion, _recipeVersion, confirmRepeat: true);

    [Then("a distinct Recipe addition is recorded")]
    public async Task AssertDistinctRecipeAdditionAsync()
    {
        context.Response!.StatusCode.Should().Be(HttpStatusCode.Created);
        using var list = await GroceryRecipeTestData.ReadListAsync(context);
        list.RootElement.GetProperty("items")[0]
            .GetProperty("outstandingPurchaseQuantity").GetDecimal().Should().Be(4);
        await using var scope = context.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.GroceryRecipeAdditions.CountAsync()).Should().Be(2);
    }

    [Then("compatible requirements consolidate and preserve their Recipe sources")]
    public async Task AssertConsolidatedRecipeItemsAsync()
    {
        context.Response!.StatusCode.Should().Be(HttpStatusCode.Created);
        using var list = await GroceryRecipeTestData.ReadListAsync(context);
        var items = list.RootElement.GetProperty("items").EnumerateArray().ToArray();
        items.Should().HaveCount(3);

        var tomato = items.Single(item => item.GetProperty("ingredientId").GetGuid() == _ingredientId);
        tomato.GetProperty("requirementAmount").GetDecimal().Should().Be(25);
        tomato.GetProperty("requirementUnit").GetString().Should().Be("ml");
        tomato.GetProperty("outstandingPurchaseQuantity").GetDecimal().Should().Be(1);
        var contributions = tomato.GetProperty("contributions").EnumerateArray().ToArray();
        contributions.Should().HaveCount(2);
        contributions.Select(item => item.GetProperty("recipeStepItemId").GetGuid())
            .Should().BeEquivalentTo(_sourceItemIds);
        contributions.Select(item => item.GetProperty("recipeNameSnapshot").GetString())
            .Should().OnlyContain(value => value == "Tomato and Mushroom Bowl");

        var mushroomItems = items.Where(item => item.GetProperty("ingredientId").GetGuid() == _mushroomId).ToArray();
        mushroomItems.Should().HaveCount(2);
        mushroomItems.Select(item => item.GetProperty("dimension").GetString())
            .Should().BeEquivalentTo("count", "mass");
        mushroomItems.Single(item => item.GetProperty("dimension").GetString() == "count")
            .GetProperty("outstandingPurchaseQuantity").GetDecimal().Should().Be(5);
        mushroomItems.Single(item => item.GetProperty("dimension").GetString() == "mass")
            .GetProperty("requirementAmount").GetDecimal().Should().Be(250);

        await using var scope = context.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var source = await db.RecipeStepItems
            .Where(item => _sourceItemIds.Contains(item.Id))
            .OrderBy(item => item.Position)
            .ToArrayAsync();
        source.Select(item => item.QuantityText).Should().Equal("1", "2");
        var storedSourceNames = await db.GroceryContributions
            .Where(item => _sourceItemIds.Contains(item.SourceRecipeStepItemId!.Value))
            .OrderBy(item => item.CreatedAtUtc)
            .Select(item => item.OriginalNameText)
            .ToArrayAsync();
        storedSourceNames.Should().Contain("Sliced tomato");
    }
}
