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
public sealed class GroceryCancellationSteps(ApiAuthContext context)
{
    private Guid _cancelledItemId;

    [When("I cancel the manual grocery item named {string}")]
    public async Task CancelManualItemAsync(string name)
    {
        using var listResponse = await context.Client.GetAsync("/api/grocery-list");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using var list = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        var item = list.RootElement.GetProperty("items").EnumerateArray()
            .Single(value => value.GetProperty("name").GetString() == name);
        _cancelledItemId = item.GetProperty("id").GetGuid();

        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/grocery-list/items/{_cancelledItemId}");
        request.Headers.Add("X-Command-Id", Guid.CreateVersion7().ToString());
        request.Headers.Add(
            "X-Expected-List-Version",
            list.RootElement.GetProperty("version").GetUInt32().ToString());
        context.Response?.Dispose();
        context.Response = await context.Client.SendAsync(request);
    }

    [Then("the cancelled grocery line is retained but absent from the default list")]
    public async Task AssertCancellationAsync()
    {
        context.Response!.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var response = await context.Client.GetAsync("/api/grocery-list");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var list = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        list.RootElement.GetProperty("items").EnumerateArray()
            .Should().NotContain(item => item.GetProperty("id").GetGuid() == _cancelledItemId);

        await using var scope = context.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var item = await db.GroceryItems
            .Include(value => value.Contributions)
            .SingleAsync(value => value.Id == _cancelledItemId);
        item.State.Should().Be(GroceryItemState.Cancelled);
        item.Contributions.Should().ContainSingle()
            .Which.State.Should().Be(GroceryContributionState.Cancelled);
    }

    [Then("the re-added grocery line has a new identity")]
    public async Task AssertReAddedLineAsync()
    {
        context.Response!.StatusCode.Should().Be(HttpStatusCode.Created);
        using var response = await context.Client.GetAsync("/api/grocery-list");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var list = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var activeItemId = list.RootElement.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("name").GetString() == "Paprika")
            .GetProperty("id").GetGuid();
        activeItemId.Should().NotBe(_cancelledItemId);
    }
}
