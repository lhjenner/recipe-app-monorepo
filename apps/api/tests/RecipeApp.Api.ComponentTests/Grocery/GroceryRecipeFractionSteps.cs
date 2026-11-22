using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using RecipeApp.Api.ComponentTests.Auth;
using RecipeApp.Api.Infrastructure.Persistence;

namespace RecipeApp.Api.ComponentTests.Grocery;

[Binding]
public sealed class GroceryRecipeFractionSteps(ApiAuthContext context)
{
    private Guid _recipeId;
    private Guid _oilId;
    private Guid _onionId;

    [Given("an active Recipe with fractional oil and onion requirements")]
    public async Task SeedFractionalRecipeAsync()
    {
        await using var scope = context.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var owner = await GroceryRecipeTestData.CurrentOwnerAsync(context, database);
        var now = DateTime.UtcNow;
        var oil = GroceryRecipeTestData.CreateIngredient(owner.Id, "Oil", now);
        var onion = GroceryRecipeTestData.CreateIngredient(owner.Id, "Onion", now);
        var recipe = GroceryRecipeTestData.CreateRecipe(owner.Id, "Fractional recipe", now);
        var step = GroceryRecipeTestData.CreateStep(owner.Id, 0);
        step.Items.Add(GroceryRecipeTestData.CreateIngredientItem(owner.Id, oil.Id, 0, "Oil", "1/3", "cup"));
        for (var index = 1; index <= 3; index++)
        {
            step.Items.Add(GroceryRecipeTestData.CreateIngredientItem(owner.Id, onion.Id, index, "Onion", "2/3", null));
        }
        recipe.Steps.Add(step);
        database.AddRange(oil, onion, recipe);
        await database.SaveChangesAsync();
        _recipeId = recipe.Id;
        _oilId = oil.Id;
        _onionId = onion.Id;
    }

    [When("I preview the fractional Recipe and add it")]
    public async Task AddFractionalRecipeAsync()
    {
        using var response = await context.Client.GetAsync($"/api/grocery-list/recipes/{_recipeId}/preview");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var preview = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = preview.RootElement;
        root.GetProperty("included").EnumerateArray().Should().OnlyContain(
            item => item.GetProperty("interpretationStatus").GetString() == "known");
        await GroceryRecipeTestData.AddRecipeAsync(context, _recipeId,
            root.GetProperty("listVersion").GetUInt32(), root.GetProperty("recipeVersion").GetUInt32(), false);
    }

    [Then("the measured requirement is rounded and the combined count needs two onions")]
    public async Task AssertFractionalRequirementsAsync()
    {
        context.Response!.StatusCode.Should().Be(HttpStatusCode.Created);
        using var list = await GroceryRecipeTestData.ReadListAsync(context);
        var items = list.RootElement.GetProperty("items").EnumerateArray().ToArray();
        var oil = items.Single(item => item.GetProperty("ingredientId").GetGuid() == _oilId);
        oil.GetProperty("requirementAmount").GetDecimal().Should().Be(83.33m);
        oil.GetProperty("requirementUnit").GetString().Should().Be("ml");
        var onion = items.Single(item => item.GetProperty("ingredientId").GetGuid() == _onionId);
        onion.GetProperty("outstandingPurchaseQuantity").GetDecimal().Should().Be(2);
        onion.GetProperty("requirementAmount").GetDecimal().Should().Be(2);
        onion.GetProperty("contributions").EnumerateArray().Should().OnlyContain(
            value => value.GetProperty("originalAmountText").GetString() == "2/3");
    }
}
