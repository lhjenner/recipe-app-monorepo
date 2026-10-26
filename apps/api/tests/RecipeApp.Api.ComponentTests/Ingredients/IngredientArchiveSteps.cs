using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using RecipeApp.Api.Auth;
using RecipeApp.Api.ComponentTests.Auth;
using RecipeApp.Api.Infrastructure.Persistence;

namespace RecipeApp.Api.ComponentTests.Ingredients;

[Binding]
public sealed class IngredientArchiveSteps(ApiAuthContext context)
{
    private readonly List<HttpStatusCode> _archiveResponses = [];
    private readonly List<HttpStatusCode> _restoreResponses = [];
    private string? _archivedAtUtcAfterFirstArchive;

    [When("I archive that ingredient twice")]
    public async Task WhenIArchiveThatIngredientTwice()
    {
        using var firstResponse = await context.Client.PostAsync(
            $"/api/ingredients/{context.ScenarioIngredientId}/archive",
            null);
        _archiveResponses.Add(firstResponse.StatusCode);
        using var afterFirstArchive = await GetIngredientAsync();
        _archivedAtUtcAfterFirstArchive = afterFirstArchive.RootElement
            .GetProperty("archivedAtUtc").GetString();

        using var secondResponse = await context.Client.PostAsync(
            $"/api/ingredients/{context.ScenarioIngredientId}/archive",
            null);
        _archiveResponses.Add(secondResponse.StatusCode);
    }

    [When("I archive that ingredient")]
    public async Task WhenIArchiveThatIngredient()
    {
        using var response = await context.Client.PostAsync(
            $"/api/ingredients/{context.ScenarioIngredientId}/archive",
            null);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [When("I restore that ingredient")]
    public Task WhenIRestoreThatIngredient() =>
        SendArchiveAsync("restore");

    [When("I restore that ingredient twice")]
    public async Task WhenIRestoreThatIngredientTwice()
    {
        using var firstResponse = await context.Client.PostAsync(
            $"/api/ingredients/{context.ScenarioIngredientId}/restore",
            null);
        _restoreResponses.Add(firstResponse.StatusCode);
        using var secondResponse = await context.Client.PostAsync(
            $"/api/ingredients/{context.ScenarioIngredientId}/restore",
            null);
        _restoreResponses.Add(secondResponse.StatusCode);
    }

    [When("I save a stale edit after archiving the ingredient")]
    public async Task WhenISaveAStaleEditAfterArchivingTheIngredient()
    {
        using var beforeArchive = await GetIngredientAsync();
        var originalVersion = beforeArchive.RootElement.GetProperty("version").GetUInt32();
        using var archiveResponse = await context.Client.PostAsync(
            $"/api/ingredients/{context.ScenarioIngredientId}/archive",
            null);
        archiveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        context.Response = await context.Client.PutAsJsonAsync(
            $"/api/ingredients/{context.ScenarioIngredientId}",
            new
            {
                name = "Stale Ingredient",
                version = originalVersion
            });
    }

    [When("I delete that ingredient")]
    public async Task WhenIDeleteThatIngredient() =>
        context.Response = await context.Client.DeleteAsync(
            $"/api/ingredients/{context.ScenarioIngredientId}");

    [Then("both archive responses are no content")]
    public void ThenBothArchiveResponsesAreNoContent() =>
        _archiveResponses.Should().OnlyContain(
            status => status == HttpStatusCode.NoContent);

    [Then("both restore responses are no content")]
    public void ThenBothRestoreResponsesAreNoContent() =>
        _restoreResponses.Should().OnlyContain(
            status => status == HttpStatusCode.NoContent);

    [Then("the ingredient is archived exactly once")]
    public async Task ThenTheIngredientIsArchivedExactlyOnce()
    {
        using var ingredient = await GetIngredientAsync();
        var archivedAtUtc = ingredient.RootElement.GetProperty("archivedAtUtc").GetString();
        archivedAtUtc.Should().NotBeNullOrWhiteSpace();
        archivedAtUtc.Should().Be(_archivedAtUtcAfterFirstArchive);
    }

    [Then("the default ingredient list does not contain {string}")]
    public async Task ThenTheDefaultIngredientListDoesNotContain(string name)
    {
        using var list = await GetIngredientListAsync();
        list.RootElement.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("name").GetString())
            .Should().NotContain(name);
    }

    [Then("the response is no content")]
    public void ThenTheResponseIsNoContent() =>
        context.Response!.StatusCode.Should().Be(HttpStatusCode.NoContent);

    [Then("the ingredient is active in the default list")]
    public async Task ThenTheIngredientIsActiveInTheDefaultList()
    {
        using var ingredient = await GetIngredientAsync();
        ingredient.RootElement.GetProperty("archivedAtUtc").ValueKind.Should().Be(JsonValueKind.Null);
        using var list = await GetIngredientListAsync();
        list.RootElement.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid())
            .Should().Contain(context.ScenarioIngredientId);
    }

    [Then("the stale edit is rejected and the ingredient stays archived")]
    public async Task ThenTheStaleEditIsRejectedAndTheIngredientStaysArchived()
    {
        context.Response!.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var ingredient = await GetIngredientAsync();
        ingredient.RootElement.GetProperty("archivedAtUtc").ValueKind.Should().NotBe(JsonValueKind.Null);
    }

    [Then("archiving and restoring another user's ingredient return not found")]
    public async Task ThenArchivingAndRestoringAnotherUsersIngredientReturnNotFound()
    {
        using var archiveResponse = await context.Client.PostAsync(
            $"/api/ingredients/{context.ScenarioIngredientId}/archive",
            null);
        using var restoreResponse = await context.Client.PostAsync(
            $"/api/ingredients/{context.ScenarioIngredientId}/restore",
            null);
        archiveResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        restoreResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Then("the ingredient remains archived")]
    public async Task ThenTheIngredientRemainsArchived()
    {
        await using var scope = context.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ingredient = await database.Ingredients.AsNoTracking()
            .SingleAsync(item => item.Id == context.ScenarioIngredientId);
        ingredient.ArchivedAtUtc.Should().NotBeNull();
    }

    [Then("the response method is not allowed")]
    public void ThenTheResponseMethodIsNotAllowed() =>
        context.Response!.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);

    private async Task SendArchiveAsync(string action)
    {
        context.Response = await context.Client.PostAsync(
            $"/api/ingredients/{context.ScenarioIngredientId}/{action}",
            null);
    }

    private async Task<JsonDocument> GetIngredientAsync()
    {
        var response = await context.Client.GetAsync(
            $"/api/ingredients/{context.ScenarioIngredientId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private async Task<JsonDocument> GetIngredientListAsync()
    {
        var response = await context.Client.GetAsync("/api/ingredients");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }
}
