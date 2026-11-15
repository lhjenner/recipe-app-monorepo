using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using RecipeApp.Api.Infrastructure.Persistence;
using RecipeApp.Api.ComponentTests.Auth;

namespace RecipeApp.Api.ComponentTests.Grocery;

[Binding]
public sealed class GroceryReadAssertionSteps(ApiAuthContext context)
{
    [Then("the grocery list is empty without a persistent list")]
    public async Task AssertEmptyGroceryListAsync()
    {
        context.Response!.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = await JsonDocument.ParseAsync(
            await context.Response.Content.ReadAsStreamAsync());
        document.RootElement.GetProperty("id").ValueKind.Should().Be(JsonValueKind.Null);
        document.RootElement.GetProperty("items").GetArrayLength().Should().Be(0);

        await using var scope = context.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var owner = await db.Users.AsNoTracking().SingleAsync(user =>
            user.Email == context.Email.Trim().ToLowerInvariant());
        (await db.GroceryLists.CountAsync(list => list.OwnerUserId == owner.Id)).Should().Be(0);
    }

    [Then("the grocery item shows current Ingredient metadata and the original source text")]
    public async Task AssertCurrentIngredientReadAsync()
    {
        context.Response!.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await context.Response.Content.ReadAsStringAsync());
        var item = document.RootElement.GetProperty("items")[0];
        item.GetProperty("name").GetString().Should().Be("current onion name");
        item.GetProperty("requirementAmount").GetDecimal().Should().Be(2);
        item.GetProperty("requirementUnit").GetString().Should().Be("onion");
        var mapping = item.GetProperty("storeMappings")[0];
        mapping.GetProperty("state").GetString().Should().Be("mapped");
        mapping.GetProperty("departmentName").GetString().Should().Be("produce");
        item.GetProperty("contributions")[0]
            .GetProperty("originalAmountText").GetString().Should().Be("2");
    }

    [Then("grocery purchase history contains {int} events")]
    public async Task AssertGroceryHistoryCountAsync(int expectedCount)
    {
        context.Response!.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = await JsonDocument.ParseAsync(
            await context.Response.Content.ReadAsStreamAsync());
        document.RootElement.GetProperty("totalCount").GetInt32().Should().Be(expectedCount);
        document.RootElement.GetProperty("items").GetArrayLength().Should().Be(expectedCount);
    }

    [Then("grocery purchase history contains {int} event named {string} marked reversed")]
    public async Task AssertFilteredReversedHistoryAsync(int expectedCount, string expectedName)
    {
        context.Response!.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = await JsonDocument.ParseAsync(
            await context.Response.Content.ReadAsStreamAsync());
        var items = document.RootElement.GetProperty("items");
        document.RootElement.GetProperty("totalCount").GetInt32().Should().Be(expectedCount);
        items.GetArrayLength().Should().Be(expectedCount);
        items[0].GetProperty("itemNameSnapshot").GetString().Should().Be(expectedName);
        items[0].GetProperty("isReversed").GetBoolean().Should().BeTrue();
    }

    [Then("the grocery response is unauthorized")]
    public void AssertUnauthorized() =>
        context.Response!.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
}
