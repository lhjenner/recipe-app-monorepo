using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Reqnroll;
using RecipeApp.Api.ComponentTests.Auth;
using RecipeApp.Api.Grocery;

namespace RecipeApp.Api.ComponentTests.Grocery;

[Binding]
public sealed class GroceryPurchaseWorkflowSteps(ApiAuthContext context)
{
    private Guid _itemId;
    private Guid _purchaseItemId;
    private Guid _purchaseEventId;
    private string _itemName = string.Empty;
    private string _purchaseUnit = string.Empty;

    [When("I add and purchase three synthetic tins")]
    public async Task AddAndPurchaseThreeTinsAsync()
    {
        await AddItemAsync("Synthetic purchase tins", 3, "tin");
        await PurchaseAsync(3);
    }

    [Then("the whole line is completed with one immutable purchase event")]
    public async Task AssertPurchaseRecordedAsync()
    {
        context.Response!.StatusCode.Should().Be(HttpStatusCode.Created);
        using var list = await ReadListAsync(includeCompleted: true);
        var item = FindItem(list, _itemId);
        item.GetProperty("state").GetString().Should().Be("completed");
        item.GetProperty("outstandingPurchaseQuantity").GetDecimal().Should().Be(0);
        using var history = await ReadHistoryAsync();
        var purchase = history.RootElement.GetProperty("items").EnumerateArray()
            .Should().ContainSingle()
            .Which;
        purchase.GetProperty("kind").GetString().Should().Be("purchase");
        purchase.GetProperty("quantity").GetDecimal().Should().Be(3);
        purchase.GetProperty("itemNameSnapshot").GetString().Should().Be(_itemName);
        _purchaseEventId = purchase.GetProperty("id").GetGuid();
    }

    [When("I undo the synthetic purchase")]
    public async Task UndoPurchaseAsync()
    {
        using var list = await ReadListAsync();
        var version = list.RootElement.GetProperty("version").GetUInt32();
        context.Response?.Dispose();
        context.Response = await context.Client.PostAsJsonAsync(
            $"/api/grocery-list/purchase-history/{_purchaseEventId}/reverse",
            new GroceryCommandMetadata(Guid.CreateVersion7(), version));
    }

    [Then("the purchase remains with one reversal and exactly three tins outstanding")]
    public async Task AssertPurchaseReversedAsync()
    {
        context.Response!.StatusCode.Should().Be(HttpStatusCode.Created);
        using var list = await ReadListAsync();
        var item = FindItem(list, _itemId);
        item.GetProperty("state").GetString().Should().Be("outstanding");
        item.GetProperty("outstandingPurchaseQuantity").GetDecimal().Should().Be(3);
        using var history = await ReadHistoryAsync();
        var events = history.RootElement.GetProperty("items").EnumerateArray().ToArray();
        events.Should().HaveCount(2);
        events.Single(value => value.GetProperty("kind").GetString() == "purchase")
            .GetProperty("isReversed").GetBoolean().Should().BeTrue();
        events.Single(value => value.GetProperty("kind").GetString() == "reversal")
            .GetProperty("reversesEventId").GetGuid().Should().Be(_purchaseEventId);
    }

    [When("I undo the synthetic purchase again")]
    public Task UndoPurchaseAgainAsync() => UndoPurchaseAsync();

    [When("I restart the API against the existing PostgreSQL database")]
    public Task RestartApiAsync() => context.RestartApiAsync();

    [Then("exactly three tins remain outstanding")]
    public async Task AssertThreeTinsRemainOutstandingAfterRestartAsync()
    {
        using var list = await ReadListAsync();
        var item = FindItem(list, _itemId);
        item.GetProperty("state").GetString().Should().Be("outstanding");
        item.GetProperty("outstandingPurchaseQuantity").GetDecimal().Should().Be(3);
    }

    [Then("the purchase and one reversal remain in history")]
    public async Task AssertPurchaseAndReversalRemainAfterRestartAsync()
    {
        using var history = await ReadHistoryAsync();
        var events = history.RootElement.GetProperty("items").EnumerateArray().ToArray();
        events.Should().HaveCount(2);
        events.Should().ContainSingle(value => value.GetProperty("kind").GetString() == "purchase");
        events.Should().ContainSingle(value => value.GetProperty("kind").GetString() == "reversal");
    }

    [When("I add two more synthetic tins")]
    public Task AddLaterDemandAsync() =>
        AddItemAsync(_itemName, 2, _purchaseUnit);

    [Then("undo adds only the purchased quantity to the later outstanding line")]
    public async Task AssertLaterDemandPreservedAsync()
    {
        context.Response!.StatusCode.Should().Be(HttpStatusCode.Created);
        using var list = await ReadListAsync();
        var item = FindItem(list, _itemId);
        item.GetProperty("state").GetString().Should().Be("outstanding");
        item.GetProperty("outstandingPurchaseQuantity").GetDecimal().Should().Be(5);
        using var history = await ReadHistoryAsync();
        var events = history.RootElement.GetProperty("items").EnumerateArray().ToArray();
        events.Should().HaveCount(2);
        events.Should().OnlyContain(value =>
            value.GetProperty("groceryItemId").GetGuid() == _purchaseItemId);
    }

    [Then("the purchase still has exactly one reversal")]
    public async Task AssertSingleReversalAsync()
    {
        context.Response!.StatusCode.Should().Be(HttpStatusCode.OK);
        using var history = await ReadHistoryAsync();
        history.RootElement.GetProperty("items").EnumerateArray()
            .Count(value => value.GetProperty("kind").GetString() == "reversal")
            .Should().Be(1);
    }

    [When("I try to purchase fewer than three synthetic tins")]
    public async Task TryPartialPurchaseAsync()
    {
        await AddItemAsync("Synthetic partial tins", 3, "tin");
        await PurchaseAsync(2);
    }

    [Then("the purchase is rejected without changing the line or history")]
    public async Task AssertPartialPurchaseRejectedAsync()
    {
        context.Response!.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var list = await ReadListAsync();
        var item = FindItem(list, _itemId);
        item.GetProperty("state").GetString().Should().Be("outstanding");
        item.GetProperty("outstandingPurchaseQuantity").GetDecimal().Should().Be(3);
        using var history = await ReadHistoryAsync();
        history.RootElement.GetProperty("items").GetArrayLength().Should().Be(0);
    }

    [When("I clear two synthetic outstanding grocery lines")]
    public async Task ClearTwoItemsAsync()
    {
        await AddItemsAsync(
        [
            new("Synthetic clear apples", null, 2, "item"),
            new("Synthetic clear pears", null, 1, "item")
        ]);
        using var list = await ReadListAsync();
        using var request = new HttpRequestMessage(HttpMethod.Delete, "/api/grocery-list/items");
        request.Headers.Add("X-Command-Id", Guid.CreateVersion7().ToString());
        request.Headers.Add(
            "X-Expected-List-Version",
            list.RootElement.GetProperty("version").GetUInt32().ToString(CultureInfo.InvariantCulture));
        context.Response?.Dispose();
        context.Response = await context.Client.SendAsync(request);
    }

    [Then("both lines are cancelled and no purchase event is created")]
    public async Task AssertClearCancelledOnlyAsync()
    {
        context.Response!.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var list = await ReadListAsync();
        list.RootElement.GetProperty("items").GetArrayLength().Should().Be(0);
        using var history = await ReadHistoryAsync();
        history.RootElement.GetProperty("items").GetArrayLength().Should().Be(0);
    }

    private async Task AddItemAsync(string name, decimal quantity, string unit) =>
        await AddItemsAsync([new(name, null, quantity, unit)]);

    private async Task AddItemsAsync(IReadOnlyList<GroceryManualEntryRequest> items)
    {
        using var list = await ReadListAsync();
        context.Response?.Dispose();
        context.Response = await context.Client.PostAsJsonAsync(
            "/api/grocery-list/items",
            new GroceryManualEntryCommand(
                Guid.CreateVersion7(),
                list.RootElement.GetProperty("version").GetUInt32(),
                items,
                []));
        context.Response!.StatusCode.Should().Be(HttpStatusCode.Created);
        if (items.Count == 1)
        {
            _itemName = items[0].Name!;
            _purchaseUnit = items[0].PurchaseUnit!;
            using var added = await ReadListAsync();
            _itemId = FindItem(added, _itemName).GetProperty("id").GetGuid();
        }
    }

    private async Task PurchaseAsync(decimal expectedQuantity)
    {
        using var list = await ReadListAsync();
        _purchaseItemId = _itemId;
        var version = list.RootElement.GetProperty("version").GetUInt32();
        context.Response?.Dispose();
        context.Response = await context.Client.PostAsJsonAsync(
            $"/api/grocery-list/items/{_itemId}/purchases",
            new GroceryPurchaseRequest(
                Guid.CreateVersion7(),
                version,
                expectedQuantity,
                _purchaseUnit));
    }

    private async Task<JsonDocument> ReadListAsync(bool includeCompleted = false)
    {
        using var response = await context.Client.GetAsync(
            $"/api/grocery-list?includeCompleted={includeCompleted.ToString().ToLowerInvariant()}&allDates=true");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private async Task<JsonDocument> ReadHistoryAsync()
    {
        using var response = await context.Client.GetAsync(
            "/api/grocery-list/purchase-history?allDates=true");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static JsonElement FindItem(JsonDocument list, Guid itemId) =>
        list.RootElement.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("id").GetGuid() == itemId);

    private static JsonElement FindItem(JsonDocument list, string name) =>
        list.RootElement.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("name").GetString() == name);
}
