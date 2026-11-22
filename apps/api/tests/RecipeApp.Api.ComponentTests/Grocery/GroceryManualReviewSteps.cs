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
public sealed class GroceryManualReviewSteps(ApiAuthContext context)
{
    private GroceryManualEntryCommand? _resolvedCommand;

    [When("I replay a previously committed matching-row batch")]
    public async Task ReplayPreviouslyCommittedBatchAsync()
    {
        var seed = new GroceryManualEntryCommand(
            Guid.CreateVersion7(), 0, [new("Oats", null, 1, "item")], []);
        using var seedResponse = await context.Client.PostAsJsonAsync("/api/grocery-list/items", seed);
        seedResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        await using var scope = context.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var owner = await database.Users.SingleAsync(user => user.Email == context.Email.Trim().ToLowerInvariant());
        var list = await database.GroceryLists.SingleAsync(item => item.OwnerUserId == owner.Id);
        var item = await database.GroceryItems.SingleAsync(value => value.GroceryListId == list.Id);
        _resolvedCommand = new GroceryManualEntryCommand(
            Guid.CreateVersion7(), 0, [new("Oats", null, 1, "item"), new("Oats", null, 2, "item")], []);
        database.GroceryCommandReceipts.Add(new GroceryCommandReceipt
        {
            GroceryListId = list.Id,
            OwnerUserId = owner.Id,
            CommandId = _resolvedCommand.CommandId,
            CommandType = "manual-add",
            RequestFingerprint = GroceryCommandFingerprint.Compute("manual-add", _resolvedCommand),
            ResultJson = JsonSerializer.Serialize(new GroceryCommandReceiptResponse(_resolvedCommand.CommandId, list.Version, [item.Id])),
            ResultListVersion = list.Version,
            CommittedAtUtc = DateTime.UtcNow
        });
        await database.SaveChangesAsync();
        context.Response?.Dispose();
        context.Response = await context.Client.PostAsJsonAsync("/api/grocery-list/items", _resolvedCommand);
    }

    [Then("the grocery API returns its receipt without applying it again")]
    public async Task AssertPreviousReceiptAsync()
    {
        context.Response!.StatusCode.Should().Be(HttpStatusCode.Created);
        using var response = await context.Client.GetAsync("/api/grocery-list");
        using var list = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Quantity(list.RootElement.GetProperty("items").EnumerateArray().ToArray(), "Oats").Should().Be(1);
    }

    [When("I resolve a mixed grocery batch by {string}")]
    public async Task ResolveMixedBatchAsync(string selection)
    {
        var seed = new GroceryManualEntryCommand(
            Guid.CreateVersion7(), 0, [new("Kale", null, 1, "item"), new("Oats", null, 1, "item")], []);
        using var seedResponse = await context.Client.PostAsJsonAsync("/api/grocery-list/items", seed);
        seedResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var version = await ReadVersionAsync();
        var draft = new GroceryManualEntryCommand(
            Guid.CreateVersion7(), version,
            [new("Rice", null, 1, "item"), new("Kale", null, 2, "item"), new("Oats", null, 3, "item")], []);
        using var reviewResponse = await context.Client.PostAsJsonAsync("/api/grocery-list/items", draft);
        reviewResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var problem = JsonDocument.Parse(await reviewResponse.Content.ReadAsStringAsync());
        var reviews = JsonSerializer.Deserialize<GroceryDuplicateReview[]>(
            problem.RootElement.GetProperty("duplicateReviews").GetRawText(),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        reviews.Should().HaveCount(2);

        if (selection == "submitting stale")
        {
            var concurrent = new GroceryManualEntryCommand(
                Guid.CreateVersion7(), version, [new("Salt", null, 1, "item")], []);
            using var concurrentResponse = await context.Client.PostAsJsonAsync("/api/grocery-list/items", concurrent);
            concurrentResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        _resolvedCommand = selection == "skipping both"
            ? new GroceryManualEntryCommand(Guid.CreateVersion7(), version, [draft.Items[0]], [])
            : new GroceryManualEntryCommand(
                Guid.CreateVersion7(), version, [draft.Items[0], draft.Items[2]],
                [reviews.Single(review => review.EntryIndex == 2) with { EntryIndex = 1 }]);
        context.Response?.Dispose();
        context.Response = await context.Client.PostAsJsonAsync("/api/grocery-list/items", _resolvedCommand);
    }

    [Then("the selective grocery batch has the expected {string} outcome")]
    public async Task AssertSelectionAsync(string selection)
    {
        context.Response!.StatusCode.Should().Be(selection == "submitting stale"
            ? HttpStatusCode.Conflict
            : HttpStatusCode.Created);
        if (selection != "submitting stale")
        {
            using var replay = await context.Client.PostAsJsonAsync("/api/grocery-list/items", _resolvedCommand);
            replay.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        using var response = await context.Client.GetAsync("/api/grocery-list");
        using var list = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var items = list.RootElement.GetProperty("items").EnumerateArray().ToArray();
        Quantity(items, "Kale").Should().Be(1);
        Quantity(items, "Oats").Should().Be(selection == "confirming one" ? 4 : 1);
        items.Any(item => item.GetProperty("name").GetString() == "Rice")
            .Should().Be(selection != "submitting stale");
    }

    private async Task<uint> ReadVersionAsync()
    {
        using var response = await context.Client.GetAsync("/api/grocery-list");
        using var list = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return list.RootElement.GetProperty("version").GetUInt32();
    }

    private static decimal Quantity(JsonElement[] items, string name) =>
        items.Single(item => item.GetProperty("name").GetString() == name)
            .GetProperty("outstandingPurchaseQuantity").GetDecimal();
}
