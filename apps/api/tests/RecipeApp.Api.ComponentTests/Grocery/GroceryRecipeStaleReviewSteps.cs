using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using RecipeApp.Api.ComponentTests.Auth;
using RecipeApp.Api.Infrastructure.Persistence;

namespace RecipeApp.Api.ComponentTests.Grocery;

[Binding]
public sealed class GroceryRecipeStaleReviewSteps(ApiAuthContext context)
{
    private Guid _recipeId;
    private uint _recipeVersion;
    private uint _listVersion;

    [Given("an active Recipe with countable onion demand for stale-review testing")]
    public async Task SeedOnionRecipeAsync()
    {
        await using var scope = context.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var owner = await GroceryRecipeTestData.CurrentOwnerAsync(context, db);
        var now = DateTime.UtcNow;
        var onion = GroceryRecipeTestData.CreateIngredient(owner.Id, "Onion", now);
        var recipe = GroceryRecipeTestData.CreateRecipe(owner.Id, "Onion Soup", now);
        var step = GroceryRecipeTestData.CreateStep(owner.Id, 0);
        step.Items.Add(GroceryRecipeTestData.CreateIngredientItem(owner.Id, onion.Id, 0, "Onion", "2", "onions"));
        recipe.Steps.Add(step);
        db.AddRange(onion, recipe);
        await db.SaveChangesAsync();

        _recipeId = recipe.Id;
        _recipeVersion = recipe.Version;
    }

    [When("I preview the Recipe for grocery addition before it changes")]
    public async Task PreviewRecipeAsync()
    {
        context.Response?.Dispose();
        context.Response = await context.Client.GetAsync(
            $"/api/grocery-list/recipes/{_recipeId}/preview");
        context.Response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var preview = System.Text.Json.JsonDocument.Parse(
            await context.Response.Content.ReadAsStringAsync());
        _recipeVersion = preview.RootElement.GetProperty("recipeVersion").GetUInt32();
        _listVersion = preview.RootElement.GetProperty("listVersion").GetUInt32();
    }

    [When("the Recipe changes after preview")]
    public async Task ChangeRecipeAfterPreviewAsync()
    {
        await using var scope = context.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var recipe = await db.Recipes.SingleAsync(value => value.Id == _recipeId);
        recipe.Name = "Changed Onion Soup";
        recipe.NormalizedName = "changed onion soup";
        recipe.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    [When("I add the Recipe reviewed before its change")]
    public Task AddStaleRecipeAsync() =>
        GroceryRecipeTestData.AddRecipeAsync(context, _recipeId, _listVersion, _recipeVersion, confirmRepeat: false);

    [Then("the Recipe addition is rejected as stale without writes")]
    public async Task AssertStaleRecipeRejectedAsync()
    {
        context.Response!.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var list = await GroceryRecipeTestData.ReadListAsync(context);
        list.RootElement.GetProperty("items").GetArrayLength().Should().Be(0);
        await using var scope = context.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.GroceryRecipeAdditions.CountAsync()).Should().Be(0);
    }
}
