using System.Net;
using System.Text.Json;
using FluentAssertions;
using Reqnroll;
using RecipeApp.Api.ComponentTests.Auth;

namespace RecipeApp.Api.ComponentTests.Ingredients;

[Binding]
public sealed class IngredientReadSteps(ApiAuthContext context)
{
    private HttpResponseMessage? _detailResponse;

    [When("I request the ingredient list and a detail without signing in")]
    public async Task WhenIRequestTheIngredientListAndADetailWithoutSigningIn()
    {
        context.Response = await context.Client.GetAsync("/api/ingredients");
        _detailResponse = await context.Client.GetAsync(
            $"/api/ingredients/{Guid.NewGuid()}");
    }

    [Then("both ingredient responses are unauthorized")]
    public void ThenBothIngredientResponsesAreUnauthorized()
    {
        context.Response!.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _detailResponse!.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [When("I request ingredient page {int} with page size {int}")]
    public Task WhenIRequestIngredientPageWithPageSize(int page, int pageSize) =>
        RequestListAsync($"page={page}&pageSize={pageSize}");

    [When("I request the default ingredient list")]
    public Task WhenIRequestTheDefaultIngredientList() =>
        SendAsync(() => context.Client.GetAsync("/api/ingredients"));

    [When("I request archived ingredient page {int} with page size {int}")]
    public Task WhenIRequestArchivedIngredientPageWithPageSize(int page, int pageSize) =>
        RequestListAsync($"page={page}&pageSize={pageSize}&archived=true");

    [When("I search ingredients for {string}")]
    public Task WhenISearchIngredientsFor(string search) =>
        RequestListAsync($"search={Uri.EscapeDataString(search)}&page=1&pageSize=25");

    [Then("the ingredient list contains {int} items, {int} total, on page {int}")]
    public async Task ThenTheIngredientListContainsItemsTotalOnPage(
        int itemCount,
        int totalCount,
        int page)
    {
        using var document = await ReadResponseAsync(context.Response!);
        var result = document.RootElement;
        result.GetProperty("items").GetArrayLength().Should().Be(itemCount);
        result.GetProperty("totalCount").GetInt32().Should().Be(totalCount);
        result.GetProperty("page").GetInt32().Should().Be(page);
    }

    [Then("the ingredient names are sorted by normalized name")]
    public async Task ThenTheIngredientNamesAreSortedByNormalizedName()
    {
        using var document = await ReadResponseAsync(context.Response!);
        var names = document.RootElement.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("name").GetString())
            .ToArray();
        names.Should().BeInAscendingOrder(name => name);
    }

    [Then("the ingredient list contains the names {string}")]
    public async Task ThenTheIngredientListContainsTheNames(string expectedNames)
    {
        using var document = await ReadResponseAsync(context.Response!);
        document.RootElement.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("name").GetString())
            .Should().Equal(expectedNames.Split(','));
    }

    [Then("the ingredient list total is {int}")]
    public async Task ThenTheIngredientListTotalIs(int totalCount)
    {
        using var document = await ReadResponseAsync(context.Response!);
        document.RootElement.GetProperty("totalCount").GetInt32().Should().Be(totalCount);
    }

    [When("I request the previous user's ingredient")]
    public Task WhenIRequestThePreviousUsersIngredient() =>
        SendDetailAsync(context.ScenarioIngredientId);

    [Then("the ingredient list does not contain {string}")]
    public async Task ThenTheIngredientListDoesNotContain(string name)
    {
        using var document = await ReadResponseAsync(context.Response!);
        document.RootElement.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("name").GetString())
            .Should().NotContain(name);
    }

    [Then("the ingredient list contains {string}")]
    public async Task ThenTheIngredientListContains(string name)
    {
        using var document = await ReadResponseAsync(context.Response!);
        document.RootElement.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("name").GetString())
            .Should().Contain(name);
    }

    [Then("the ingredient list contains only archived items")]
    public async Task ThenTheIngredientListContainsOnlyArchivedItems()
    {
        using var document = await ReadResponseAsync(context.Response!);
        var items = document.RootElement.GetProperty("items").EnumerateArray().ToArray();
        items.Should().ContainSingle();
        items[0].GetProperty("archived").GetBoolean().Should().BeTrue();
        items[0].GetProperty("name").GetString().Should().Be("Archived Ingredient");
    }

    [When("I request that ingredient's detail")]
    public Task WhenIRequestThatIngredientsDetail() =>
        SendDetailAsync(context.ScenarioIngredientId);

    [Then("the ingredient detail includes its category, nutrition, product link, and store states")]
    public async Task ThenTheIngredientDetailIncludesItsRelatedData()
    {
        using var document = await ReadResponseAsync(context.Response!);
        var result = document.RootElement;
        result.GetProperty("categoryName").GetString().Should().Be("Produce");
        result.GetProperty("nutrition").GetProperty("energyKcal").GetDecimal().Should().Be(52);
        var productLinks = result.GetProperty("productLinks").EnumerateArray().ToArray();
        productLinks.Should().ContainSingle();
        productLinks[0].GetProperty("chainCode").GetString().Should().Be("new-world");
        productLinks[0].GetProperty("url").GetString().Should().Be("https://example.com/apples");
        var storeMappings = result.GetProperty("storeMappings").EnumerateArray().ToArray();
        storeMappings.Should().HaveCount(3);
        storeMappings.Single(mapping =>
                mapping.GetProperty("storeCode").GetString() == "new-world-greenmeadows")
            .GetProperty("state").GetString().Should().Be("mapped");
        storeMappings.Single(mapping =>
                mapping.GetProperty("storeCode").GetString() == "new-world-greenmeadows")
            .GetProperty("departmentName").GetString().Should().Be("Fruit and Vegetables");
        storeMappings.Single(mapping =>
                mapping.GetProperty("storeCode").GetString() == "pak-n-save-tamatea")
            .GetProperty("state").GetString().Should().Be("notStocked");
        storeMappings.Single(mapping =>
                mapping.GetProperty("storeCode").GetString() == "woolworths-carlyle")
            .GetProperty("state").GetString().Should().Be("unmapped");
        result.GetProperty("version").GetUInt32().Should().BeGreaterThan(0);
    }

    [Then("the response is a 400 problem details response")]
    public async Task ThenTheResponseIsA400ProblemDetailsResponse()
    {
        context.Response!.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        context.Response.Content.Headers.ContentType?.MediaType
            .Should().Be("application/problem+json");
    }

    private Task RequestListAsync(string query) =>
        SendAsync(() => context.Client.GetAsync($"/api/ingredients?{query}"));

    private Task SendDetailAsync(Guid id) =>
        SendAsync(() => context.Client.GetAsync($"/api/ingredients/{id}"));

    private async Task SendAsync(Func<Task<HttpResponseMessage>> request) =>
        context.Response = await request();

    private static async Task<JsonDocument> ReadResponseAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync());
}
