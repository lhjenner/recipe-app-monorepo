using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using RecipeApp.Api.ComponentTests.Auth;
using RecipeApp.Api.Grocery;
using RecipeApp.Api.Infrastructure.Persistence;

namespace RecipeApp.Api.ComponentTests.Grocery;

[Binding]
public sealed class GroceryRecipeRemovalSteps(ApiAuthContext context)
{
    private Guid _recipeId;
    private Guid _ingredientId;
    private uint _recipeVersion;
    private string _ingredientName = "Onion";
    private string _purchaseUnit = "onion";

    [Given("an active Recipe with countable onion demand to remove")]
    public Task SeedOnionRecipeAsync() => SeedSingleIngredientRecipeAsync(
        "Onion", "Onion Soup", "2", "onions", "onion");

    [Given("an active Recipe with measured flour demand to remove")]
    public Task SeedFlourRecipeAsync() => SeedSingleIngredientRecipeAsync(
        "Flour", "Flour Bread", "1", "cup", "item");

    [When("I add this Recipe before adding a manual increment")]
    public Task AddRecipeBeforeManualDemandAsync() =>
        GroceryRecipeTestData.AddRecipeAsync(context, _recipeId, 0, _recipeVersion, confirmRepeat: false);

    [When("I add one manual increment")]
    public async Task AddManualIncrementAsync()
    {
        using var list = await GroceryRecipeTestData.ReadListAsync(context);
        var version = list.RootElement.GetProperty("version").GetUInt32();
        var command = new GroceryManualEntryCommand(
            Guid.CreateVersion7(),
            version,
            [new GroceryManualEntryRequest(
                _ingredientName,
                null,
                1,
                _purchaseUnit,
                IngredientId: _ingredientId)],
            []);
        context.Response?.Dispose();
        context.Response = await context.Client.PostAsJsonAsync("/api/grocery-list/items", command);
        context.Response!.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using var problem = JsonDocument.Parse(await context.Response.Content.ReadAsStringAsync());
        var reviews = JsonSerializer.Deserialize<GroceryDuplicateReview[]>(
            problem.RootElement.GetProperty("duplicateReviews").GetRawText(),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        context.Response.Dispose();
        context.Response = await context.Client.PostAsJsonAsync(
            "/api/grocery-list/items",
            command with { ReviewedDuplicates = reviews });
        context.Response!.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [When("I remove the Recipe's outstanding requirements")]
    public async Task RemoveRecipeRequirementsAsync()
    {
        using var list = await GroceryRecipeTestData.ReadListAsync(context);
        var listVersion = list.RootElement.GetProperty("version").GetUInt32();
        context.Response?.Dispose();
        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/grocery-list/recipes/{_recipeId}/contributions")
        {
            Content = JsonContent.Create(
                new GroceryRecipeRemovalCommand(Guid.CreateVersion7(), listVersion))
        };
        context.Response = await context.Client.SendAsync(request);
    }

    [Then("manual demand remains without Recipe demand")]
    public async Task AssertManualDemandRemainsAsync()
    {
        context.Response!.StatusCode.Should().Be(HttpStatusCode.OK);
        using var list = await GroceryRecipeTestData.ReadListAsync(context);
        var item = list.RootElement.GetProperty("items").EnumerateArray()
            .Single(value => value.GetProperty("ingredientId").GetGuid() == _ingredientId);
        item.GetProperty("outstandingPurchaseQuantity").GetDecimal().Should().Be(1);
        var contributions = item.GetProperty("contributions").EnumerateArray().ToArray();
        contributions.Should().ContainSingle(value =>
            value.GetProperty("origin").GetString() == "manual"
            && value.GetProperty("state").GetString() == "outstanding");
        contributions.Should().ContainSingle(value =>
            value.GetProperty("origin").GetString() == "recipe"
            && value.GetProperty("state").GetString() == "cancelled");
    }

    [Then("the surviving manual line contains both increments")]
    public async Task AssertTwoManualIncrementsAsync()
    {
        context.Response!.StatusCode.Should().Be(HttpStatusCode.Created);
        using var list = await GroceryRecipeTestData.ReadListAsync(context);
        var items = list.RootElement.GetProperty("items").EnumerateArray().ToArray();
        items.Should().ContainSingle();
        items[0].GetProperty("outstandingPurchaseQuantity").GetDecimal().Should().Be(2);
    }

    private async Task SeedSingleIngredientRecipeAsync(
        string ingredientName,
        string recipeName,
        string amount,
        string unit,
        string manualPurchaseUnit)
    {
        await using var scope = context.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var owner = await GroceryRecipeTestData.CurrentOwnerAsync(context, db);
        var now = DateTime.UtcNow;
        var ingredient = GroceryRecipeTestData.CreateIngredient(owner.Id, ingredientName, now);
        var recipe = GroceryRecipeTestData.CreateRecipe(owner.Id, recipeName, now);
        var step = GroceryRecipeTestData.CreateStep(owner.Id, 0);
        step.Items.Add(GroceryRecipeTestData.CreateIngredientItem(
            owner.Id, ingredient.Id, 0, ingredientName, amount, unit));
        recipe.Steps.Add(step);
        db.AddRange(ingredient, recipe);
        await db.SaveChangesAsync();

        _recipeId = recipe.Id;
        _ingredientId = ingredient.Id;
        _recipeVersion = recipe.Version;
        _ingredientName = ingredient.Name;
        _purchaseUnit = manualPurchaseUnit;
    }
}
