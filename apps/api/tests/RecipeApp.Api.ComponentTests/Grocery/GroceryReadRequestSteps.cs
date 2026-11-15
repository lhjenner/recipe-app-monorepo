using Reqnroll;
using RecipeApp.Api.ComponentTests.Auth;

namespace RecipeApp.Api.ComponentTests.Grocery;

[Binding]
public sealed class GroceryReadRequestSteps(ApiAuthContext context)
{
    [When("I request the default grocery list")]
    public async Task RequestGroceryListAsync() =>
        context.Response = await context.Client.GetAsync("/api/grocery-list");

    [When("I request grocery purchase history")]
    public async Task RequestGroceryHistoryAsync() =>
        context.Response = await context.Client.GetAsync("/api/grocery-list/purchase-history?allDates=true");

    [When("I request grocery purchase history from {string} to {string}")]
    public async Task RequestGroceryHistoryForDatesAsync(string from, string to) =>
        context.Response = await context.Client.GetAsync(
            $"/api/grocery-list/purchase-history?from={from}&to={to}");
}
