using System.Net;
using System.Net.Http.Json;
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
public sealed class GroceryPurchaseRecalculationSteps(ApiAuthContext context)
{
    private Guid _recipeId;
    private uint _recipeVersion;
    private Guid _eventId;

    [When("I purchase the recipe line and add the recipe again")]
    public async Task PurchaseAndAddAsync()
    {
        await using (var scope = context.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var owner = await GroceryRecipeTestData.CurrentOwnerAsync(context, db);
            var recipe = await db.Recipes.SingleAsync(value => value.OwnerUserId == owner.Id);
            _recipeId = recipe.Id;
            _recipeVersion = recipe.Version;
        }

        using var list = await GroceryRecipeTestData.ReadListAsync(context);
        var item = list.RootElement.GetProperty("items")[0];
        using var response = await context.Client.PostAsJsonAsync(
            $"/api/grocery-list/items/{item.GetProperty("id").GetGuid()}/purchases",
            new GroceryPurchaseRequest(Guid.CreateVersion7(),
                list.RootElement.GetProperty("version").GetUInt32(),
                item.GetProperty("outstandingPurchaseQuantity").GetDecimal(),
                item.GetProperty("purchaseUnit").GetString()!));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var receipt = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        _eventId = receipt.RootElement.GetProperty("resultIds")[0].GetGuid();
        await AddRecipeAsync(false);
    }

    [When("I undo that recipe purchase")]
    public async Task UndoAsync()
    {
        using var list = await GroceryRecipeTestData.ReadListAsync(context);
        using var response = await context.Client.PostAsJsonAsync(
            $"/api/grocery-list/purchase-history/{_eventId}/reverse",
            new GroceryCommandMetadata(Guid.CreateVersion7(), list.RootElement.GetProperty("version").GetUInt32()));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [When("I add the recipe once more")]
    public Task AddAgainAsync() => AddRecipeAsync(true);

    [Then("the recipe line retains both purchased and later quantities")]
    public async Task AssertCombinedAsync()
    {
        using var list = await GroceryRecipeTestData.ReadListAsync(context);
        list.RootElement.GetProperty("items")[0].GetProperty("outstandingPurchaseQuantity")
            .GetDecimal().Should().Be(2);
    }

    [Then("the quantity restored by undo still remains")]
    public async Task AssertRestoredAsync()
    {
        using var list = await GroceryRecipeTestData.ReadListAsync(context);
        list.RootElement.GetProperty("items")[0].GetProperty("outstandingPurchaseQuantity")
            .GetDecimal().Should().Be(1);
    }

    [When("I try to reduce the restored count below the Recipe floor")]
    public async Task ReduceBelowFloorAsync()
    {
        context.Response?.Dispose();
        context.Response = await UpdateQuantityAsync(3);
    }

    [Then("the count reduction is rejected")]
    public void AssertReductionRejected() =>
        context.Response!.StatusCode.Should().Be(HttpStatusCode.BadRequest);

    [Then("the countable line contains six onions")]
    public async Task AssertSixAsync()
    {
        using var list = await GroceryRecipeTestData.ReadListAsync(context);
        list.RootElement.GetProperty("items")[0].GetProperty("outstandingPurchaseQuantity")
            .GetDecimal().Should().Be(6);
    }

    [Then("two restored onions remain and can be reduced")]
    public async Task AssertTwoAndReduceAsync()
    {
        using var list = await GroceryRecipeTestData.ReadListAsync(context);
        list.RootElement.GetProperty("items")[0].GetProperty("outstandingPurchaseQuantity")
            .GetDecimal().Should().Be(2);
        using var response = await UpdateQuantityAsync(1);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var updated = await GroceryRecipeTestData.ReadListAsync(context);
        updated.RootElement.GetProperty("items")[0].GetProperty("outstandingPurchaseQuantity")
            .GetDecimal().Should().Be(1);
    }

    private async Task<HttpResponseMessage> UpdateQuantityAsync(decimal quantity)
    {
        using var list = await GroceryRecipeTestData.ReadListAsync(context);
        var item = list.RootElement.GetProperty("items")[0];
        return await context.Client.PutAsJsonAsync(
            $"/api/grocery-list/items/{item.GetProperty("id").GetGuid()}",
            new GroceryItemUpdateRequest(Guid.CreateVersion7(),
                list.RootElement.GetProperty("version").GetUInt32(), quantity,
                item.GetProperty("purchaseUnit").GetString(), null, null, null, null));
    }

    private async Task AddRecipeAsync(bool confirmRepeat)
    {
        using var list = await GroceryRecipeTestData.ReadListAsync(context);
        await GroceryRecipeTestData.AddRecipeAsync(context, _recipeId,
            list.RootElement.GetProperty("version").GetUInt32(), _recipeVersion, confirmRepeat);
        context.Response!.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
