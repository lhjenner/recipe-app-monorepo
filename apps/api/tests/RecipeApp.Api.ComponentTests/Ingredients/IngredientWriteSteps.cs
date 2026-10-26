using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Reqnroll;
using RecipeApp.Api.ComponentTests.Auth;

namespace RecipeApp.Api.ComponentTests.Ingredients;

[Binding]
public sealed class IngredientWriteSteps(ApiAuthContext context)
{
    private uint _originalVersion;

    [When("I create an ingredient named {string}")]
    public Task WhenICreateAnIngredientNamed(string name) =>
        SendAsync(new { name });

    [When("I create an ingredient with negative energy")]
    public Task WhenICreateAnIngredientWithNegativeEnergy() =>
        SendAsync(new
        {
            name = "Invalid Nutrition",
            nutrition = new
            {
                energyKcal = -1,
                proteinG = (decimal?)null,
                carbohydrateG = (decimal?)null,
                sugarG = (decimal?)null,
                fatG = (decimal?)null,
                saturatedFatG = (decimal?)null,
                fibreG = (decimal?)null,
                sodiumMg = (decimal?)null
            }
        });

    [Then("the ingredient is created with a version 7 ID and a location")]
    public async Task ThenIngredientIsCreatedWithVersionSevenIdAndLocation()
    {
        context.Response!.StatusCode.Should().Be(HttpStatusCode.Created);
        context.Response.Headers.Location.Should().NotBeNull();
        using var document = JsonDocument.Parse(await context.Response.Content.ReadAsStringAsync());
        var id = document.RootElement.GetProperty("id").GetString();
        Guid.TryParse(id, out _).Should().BeTrue();
        id![14].Should().Be('7');
    }

    [Then("the response reports an ingredient name conflict")]
    public async Task ThenResponseReportsAnIngredientNameConflict()
    {
        context.Response!.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var document = JsonDocument.Parse(await context.Response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("title").GetString()
            .Should().Be("An ingredient with this name already exists");
    }

    [When("I update only a store mapping")]
    public async Task WhenIUpdateOnlyAStoreMapping()
    {
        using var current = await GetIngredientAsync();
        var detail = current.RootElement;
        _originalVersion = detail.GetProperty("version").GetUInt32();
        var product = detail.GetProperty("productLinks")[0];

        context.Response = await context.Client.PutAsJsonAsync(
            $"/api/ingredients/{context.ScenarioIngredientId}",
            new
            {
                name = detail.GetProperty("name").GetString(),
                categoryId = detail.GetProperty("categoryId").GetGuid(),
                excludeFromGroceryList = detail.GetProperty("excludeFromGroceryList").GetBoolean(),
                nutrition = new
                {
                    energyKcal = (decimal?)52,
                    proteinG = (decimal?)null,
                    carbohydrateG = (decimal?)14,
                    sugarG = (decimal?)10,
                    fatG = (decimal?)null,
                    saturatedFatG = (decimal?)null,
                    fibreG = (decimal?)null,
                    sodiumMg = (decimal?)null
                },
                nutritionSource = detail.GetProperty("nutritionSource").GetString(),
                storeMappings = new[]
                {
                    new
                    {
                        storeId = Guid.Parse("67af7315-c4e5-4f9e-89b3-7c3a71ba11da"),
                        departmentId = (Guid?)null,
                        isNotStocked = true
                    },
                    new
                    {
                        storeId = Guid.Parse("b4df52fd-667a-41d8-9a9b-9d6e3bc8c87c"),
                        departmentId = (Guid?)null,
                        isNotStocked = true
                    },
                    new
                    {
                        storeId = Guid.Parse("3e50666e-4aac-458f-9ae8-2eacba31ec6b"),
                        departmentId = (Guid?)null,
                        isNotStocked = false
                    }
                },
                productLinks = new[]
                {
                    new
                    {
                        chainId = Guid.Parse("19f8486e-7b2c-4e10-949f-ecf0d2039b36"),
                        url = product.GetProperty("url").GetString(),
                        variantLabel = product.GetProperty("variantLabel").GetString(),
                        note = product.GetProperty("note").GetString(),
                        supportsBothPricingMethods = product.GetProperty("supportsBothPricingMethods").GetBoolean(),
                        isPreferred = product.GetProperty("isPreferred").GetBoolean()
                    }
                },
                version = _originalVersion
            });
    }

    [Then("the ingredient version advances and the new mapping is saved")]
    public async Task ThenIngredientVersionAdvancesAndMappingIsSaved()
    {
        context.Response!.StatusCode.Should().Be(HttpStatusCode.OK);
        using var updated = JsonDocument.Parse(await context.Response.Content.ReadAsStringAsync());
        updated.RootElement.GetProperty("version").GetUInt32().Should().BeGreaterThan(_originalVersion);
        updated.RootElement.GetProperty("storeMappings").EnumerateArray()
            .Single(mapping => mapping.GetProperty("storeCode").GetString() == "new-world-greenmeadows")
            .GetProperty("state").GetString().Should().Be("notStocked");
        updated.RootElement.GetProperty("productLinks").GetArrayLength().Should().Be(1);
    }

    private async Task<JsonDocument> GetIngredientAsync()
    {
        var response = await context.Client.GetAsync($"/api/ingredients/{context.ScenarioIngredientId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private async Task SendAsync<T>(T request)
    {
        context.Response = await context.Client.PostAsJsonAsync("/api/ingredients", request);
    }
}
