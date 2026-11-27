using System.Globalization;
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
public sealed class GroceryPurchaseReviewSteps(ApiAuthContext context)
{
    private readonly Guid _clearCommandId = Guid.CreateVersion7();
    private Guid _ingredientId;
    private Guid _purchaseEventId;
    private Guid _storeId;
    private readonly List<GroceryCommandMetadata> _successfulUndos = [];

    [When("I clear the empty list and later add an item")]
    public async Task ClearEmptyAndAddAsync()
    {
        using var clear = CreateClearRequest(_clearCommandId, 0);
        using var response = await context.Client.SendAsync(clear);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var list = await GroceryRecipeTestData.ReadListAsync(context);
        using var added = await context.Client.PostAsJsonAsync(
            "/api/grocery-list/items",
            new GroceryManualEntryCommand(Guid.CreateVersion7(),
                list.RootElement.GetProperty("version").GetUInt32(),
                [new GroceryManualEntryRequest("Synthetic apples", null, 1, "item")], []));
        added.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Then("reusing the clear command ID with a changed version is rejected")]
    public async Task ChangedClearIsRejectedAsync()
    {
        using var list = await GroceryRecipeTestData.ReadListAsync(context);
        using var replayRequest = CreateClearRequest(_clearCommandId, 0);
        using var replay = await context.Client.SendAsync(replayRequest);
        replay.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var request = CreateClearRequest(_clearCommandId, list.RootElement.GetProperty("version").GetUInt32());
        using var response = await context.Client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var after = await GroceryRecipeTestData.ReadListAsync(context);
        after.RootElement.GetProperty("items").GetArrayLength().Should().Be(1);
    }

    [When("I purchase a linked item after renaming its ingredient")]
    public async Task PurchaseRenamedIngredientAsync()
    {
        await using (var scope = context.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var owner = await GroceryRecipeTestData.CurrentOwnerAsync(context, db);
            var ingredient = GroceryRecipeTestData.CreateIngredient(owner.Id, "Synthetic original", DateTime.UtcNow);
            db.Ingredients.Add(ingredient);
            _ingredientId = ingredient.Id;
            _storeId = (await db.Stores.OrderBy(store => store.DisplayOrder).FirstAsync()).Id;
            await db.SaveChangesAsync();
        }

        using var added = await context.Client.PostAsJsonAsync(
            "/api/grocery-list/items",
            new GroceryManualEntryCommand(Guid.CreateVersion7(), 0,
                [new GroceryManualEntryRequest("Synthetic original", null, 1, "item", _ingredientId)], []));
        added.StatusCode.Should().Be(HttpStatusCode.Created);
        await RenameIngredientAsync("Synthetic at purchase");
        using var list = await GroceryRecipeTestData.ReadListAsync(context);
        var itemId = list.RootElement.GetProperty("items")[0].GetProperty("id").GetGuid();
        using var purchase = await context.Client.PostAsJsonAsync(
            $"/api/grocery-list/items/{itemId}/purchases",
            new GroceryPurchaseRequest(Guid.CreateVersion7(),
                list.RootElement.GetProperty("version").GetUInt32(), 1, "item", _storeId));
        purchase.StatusCode.Should().Be(HttpStatusCode.Created);
        using var receipt = JsonDocument.Parse(await purchase.Content.ReadAsStringAsync());
        _purchaseEventId = receipt.RootElement.GetProperty("resultIds")[0].GetGuid();
    }

    [When("I rename its ingredient and store after purchase")]
    public async Task RenameAfterPurchaseAsync()
    {
        await RenameIngredientAsync("Synthetic later");
        await using var scope = context.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var store = await db.Stores.SingleAsync(value => value.Id == _storeId);
        store.Name = "Synthetic renamed store";
        await db.SaveChangesAsync();
    }

    [When("I undo that purchase")]
    public async Task UndoAsync()
    {
        using var list = await GroceryRecipeTestData.ReadListAsync(context);
        using var response = await context.Client.PostAsJsonAsync(
            $"/api/grocery-list/purchase-history/{_purchaseEventId}/reverse",
            new GroceryCommandMetadata(Guid.CreateVersion7(), list.RootElement.GetProperty("version").GetUInt32()));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Then("both history events retain the names from the time of purchase")]
    public async Task AssertSnapshotsAsync()
    {
        using var response = await context.Client.GetAsync("/api/grocery-list/purchase-history?allDates=true");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var history = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var events = history.RootElement.GetProperty("items").EnumerateArray().ToArray();
        events.Should().HaveCount(2);
        events.Should().OnlyContain(value => value.GetProperty("itemNameSnapshot").GetString() == "Synthetic at purchase");
        events.Should().OnlyContain(value => value.GetProperty("storeName").GetString() == "New World Greenmeadows");
    }

    [Given("an older purchase event has a store ID but no store name snapshot")]
    public async Task SeedLegacyPurchaseAsync()
    {
        await using var scope = context.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var owner = await GroceryRecipeTestData.CurrentOwnerAsync(context, db);
        _storeId = (await db.Stores.OrderBy(store => store.DisplayOrder).FirstAsync()).Id;
        var now = DateTime.UtcNow;
        var list = new GroceryList { OwnerUserId = owner.Id, CreatedAtUtc = now, UpdatedAtUtc = now };
        var item = new GroceryItem
        {
            GroceryListId = list.Id, OwnerUserId = owner.Id, NameSnapshot = "Synthetic old event",
            NormalizedName = "synthetic old event", ConsolidationKey = "legacy-test",
            PurchaseUnit = "item", State = GroceryItemState.Completed, CreatedAtUtc = now, UpdatedAtUtc = now
        };
        db.AddRange(list, item, new GroceryPurchaseEvent
        {
            GroceryListId = list.Id, GroceryItemId = item.Id, OwnerUserId = owner.Id,
            Kind = GroceryPurchaseEventKind.Purchase, Quantity = 1, PurchaseUnit = "item",
            OccurredAtUtc = now, ItemNameSnapshot = item.NameSnapshot, StoreIdSnapshot = _storeId,
            Origin = GroceryPurchaseOrigin.Manual
        });
        await db.SaveChangesAsync();
    }

    [When("the older event's store is renamed")]
    public async Task RenameLegacyStoreAsync()
    {
        await using var scope = context.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var store = await db.Stores.SingleAsync(value => value.Id == _storeId);
        store.Name = "Synthetic current store";
        await db.SaveChangesAsync();
    }

    [Then("its history shows the current store name")]
    public async Task AssertLegacyStoreAsync()
    {
        using var response = await context.Client.GetAsync("/api/grocery-list/purchase-history?allDates=true");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var history = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        history.RootElement.GetProperty("items")[0].GetProperty("storeName").GetString()
            .Should().Be("Synthetic current store");
    }

    [When("I concurrently undo the purchase with different commands")]
    public async Task ConcurrentUndosAsync()
    {
        using var historyResponse = await context.Client.GetAsync("/api/grocery-list/purchase-history?allDates=true");
        using var history = JsonDocument.Parse(await historyResponse.Content.ReadAsStringAsync());
        _purchaseEventId = history.RootElement.GetProperty("items")[0].GetProperty("id").GetGuid();
        using var list = await GroceryRecipeTestData.ReadListAsync(context);
        var version = list.RootElement.GetProperty("version").GetUInt32();
        var commands = new[]
        {
            new GroceryCommandMetadata(Guid.CreateVersion7(), version),
            new GroceryCommandMetadata(Guid.CreateVersion7(), version)
        };
        var responses = await Task.WhenAll(commands.Select(command => context.Client.PostAsJsonAsync(
            $"/api/grocery-list/purchase-history/{_purchaseEventId}/reverse", command)));
        for (var index = 0; index < responses.Length; index++)
        {
            using var response = responses[index];
            response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.OK);
            _successfulUndos.Add(commands[index]);
        }
    }

    [Then("every successful undo command rejects reuse with changed content")]
    public async Task AssertUndoReceiptsAsync()
    {
        _successfulUndos.Should().HaveCount(2);
        foreach (var command in _successfulUndos)
        {
            using var response = await context.Client.PostAsJsonAsync(
                $"/api/grocery-list/purchase-history/{_purchaseEventId}/reverse",
                command with { ExpectedListVersion = command.ExpectedListVersion + 1 });
            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        }
    }

    private async Task RenameIngredientAsync(string name)
    {
        await using var scope = context.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ingredient = await db.Ingredients.SingleAsync(value => value.Id == _ingredientId);
        ingredient.Name = name;
        ingredient.NormalizedName = name.ToLowerInvariant();
        await db.SaveChangesAsync();
    }

    private static HttpRequestMessage CreateClearRequest(Guid commandId, uint version)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/grocery-list/items");
        request.Headers.Add("X-Command-Id", commandId.ToString());
        request.Headers.Add("X-Expected-List-Version", version.ToString(CultureInfo.InvariantCulture));
        return request;
    }
}
