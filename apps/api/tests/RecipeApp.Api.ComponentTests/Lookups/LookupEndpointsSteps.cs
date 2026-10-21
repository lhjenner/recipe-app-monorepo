using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Reqnroll;
using RecipeApp.Api.ComponentTests.Auth;
using RecipeApp.Api.Auth;

namespace RecipeApp.Api.ComponentTests.Lookups;

[Binding]
public sealed class LookupEndpointsSteps(ApiAuthContext context)
{
    private readonly List<HttpResponseMessage> _lookupResponses = [];
    private string? _expectedLookupId;
    private string? _actualLookupId;

    [When("I request every lookup endpoint without signing in")]
    public async Task WhenIRequestEveryLookupEndpointWithoutSigningIn()
    {
        _lookupResponses.AddRange(
        [
            await context.Client.GetAsync("/api/categories"),
            await context.Client.PostAsJsonAsync("/api/categories", new { name = "Test" }),
            await context.Client.GetAsync("/api/departments"),
            await context.Client.PostAsJsonAsync("/api/departments", new { name = "Test" }),
            await context.Client.GetAsync("/api/supermarkets")
        ]);
    }

    [Then("every lookup response is unauthorized")]
    public void ThenEveryLookupResponseIsUnauthorized()
    {
        _lookupResponses.Should().OnlyContain(response =>
            response.StatusCode == HttpStatusCode.Unauthorized);
    }

    [When("I request the supermarkets")]
    public Task WhenIRequestTheSupermarkets() =>
        SendAsync(() => context.Client.GetAsync("/api/supermarkets"));

    [Then("the response contains all seeded supermarket chains and stores")]
    public async Task ThenTheResponseContainsAllSeededSupermarketChainsAndStores()
    {
        using var document = await ReadJsonAsync();
        var chains = document.RootElement.EnumerateArray().ToArray();

        chains.Select(chain => (
                Code: chain.GetProperty("code").GetString(),
                Name: chain.GetProperty("name").GetString()))
            .Should().BeEquivalentTo(
                new[]
                {
                    (Code: "woolworths", Name: "Woolworths"),
                    (Code: "new-world", Name: "New World"),
                    (Code: "pak-n-save", Name: "Pak'n Save")
                });

        AssertStore(chains, "new-world", "new-world-greenmeadows", "New World Greenmeadows", 1);
        AssertStore(chains, "pak-n-save", "pak-n-save-tamatea", "Pak'n Save Tamatea", 2);
        AssertStore(chains, "woolworths", "woolworths-carlyle", "Woolworths Carlyle", 3);
    }

    [When("I create a category named {string}")]
    public Task WhenICreateACategoryNamed(string name) =>
        CreateLookupAsync("categories", name);

    [When("I create a department named {string}")]
    public Task WhenICreateADepartmentNamed(string name) =>
        CreateLookupAsync("departments", name);

    [When("I create a category with a name longer than 100 characters")]
    public Task WhenICreateACategoryWithANameLongerThan100Characters() =>
        CreateLookupAsync("categories", new string('a', 101));

    [When("I create a department with a name longer than 100 characters")]
    public Task WhenICreateADepartmentWithANameLongerThan100Characters() =>
        CreateLookupAsync("departments", new string('a', 101));

    [When("I request the categories")]
    public Task WhenIRequestTheCategories() =>
        SendAsync(() => context.Client.GetAsync("/api/categories"));

    [When("I request the departments")]
    public Task WhenIRequestTheDepartments() =>
        SendAsync(() => context.Client.GetAsync("/api/departments"));

    [Then("the category names are sorted as {string}")]
    public async Task ThenTheCategoryNamesAreSortedAs(string expectedNames)
    {
        using var document = await ReadJsonAsync();
        var names = document.RootElement.EnumerateArray()
            .Select(category => category.GetProperty("name").GetString());

        names.Should().Equal(expectedNames.Split(','));
    }

    [Then("the department names are sorted as {string}")]
    public async Task ThenTheDepartmentNamesAreSortedAs(string expectedNames)
    {
        using var document = await ReadJsonAsync();
        var names = document.RootElement.EnumerateArray()
            .Select(department => department.GetProperty("name").GetString());

        names.Should().Equal(expectedNames.Split(','));
    }

    [When("another user signs in")]
    public async Task WhenAnotherUserSignsIn()
    {
        context.Email = "another-lookup-user@example.com";
        context.Password = "password123";
        if (await context.FindUserAsync(context.Email) is null)
        {
            await context.SeedExistingAccountAsync(context.Email, context.Password);
        }

        context.Response = await context.Client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(context.Email, context.Password));
        context.Response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Then("the response contains no categories")]
    public async Task ThenTheResponseContainsNoCategories()
    {
        using var document = await ReadJsonAsync();
        document.RootElement.GetArrayLength().Should().Be(0);
    }

    [Then("the response contains no departments")]
    public async Task ThenTheResponseContainsNoDepartments()
    {
        using var document = await ReadJsonAsync();
        document.RootElement.GetArrayLength().Should().Be(0);
    }

    [Then("the response is the same category as before")]
    public Task ThenTheResponseIsTheSameCategoryAsBefore() => AssertSameLookupIdAsync();

    [Then("the response is the same department as before")]
    public Task ThenTheResponseIsTheSameDepartmentAsBefore() => AssertSameLookupIdAsync();

    private async Task CreateLookupAsync(string lookup, string name)
    {
        await SendAsync(() => context.Client.PostAsJsonAsync(
            $"/api/{lookup}",
            new { name }));

        if (Response.IsSuccessStatusCode)
        {
            using var document = await ReadJsonAsync();
            _actualLookupId = document.RootElement.GetProperty("id").GetString();
            _expectedLookupId ??= _actualLookupId;
        }
    }

    private Task AssertSameLookupIdAsync()
    {
        _actualLookupId.Should().Be(_expectedLookupId);
        return Task.CompletedTask;
    }

    private async Task<JsonDocument> ReadJsonAsync() =>
        await JsonDocument.ParseAsync(await Response.Content.ReadAsStreamAsync());

    private static void AssertStore(
        JsonElement[] chains,
        string chainCode,
        string storeCode,
        string storeName,
        int displayOrder)
    {
        var stores = chains.Single(chain =>
                chain.GetProperty("code").GetString() == chainCode)
            .GetProperty("stores").EnumerateArray()
            .ToArray();
        stores.Should().ContainSingle();
        stores[0].GetProperty("code").GetString().Should().Be(storeCode);
        stores[0].GetProperty("name").GetString().Should().Be(storeName);
        stores[0].GetProperty("displayOrder").GetInt32().Should().Be(displayOrder);
    }

    private async Task SendAsync(Func<Task<HttpResponseMessage>> send) =>
        context.Response = await send();

    private HttpResponseMessage Response => context.Response
        ?? throw new InvalidOperationException("The lookup request has not been sent.");
}
