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
public sealed class GroceryManualEntrySteps(ApiAuthContext context)
{
    private GroceryManualEntryCommand? _command;
    private GroceryDuplicateReview[] _reviews = [];
    private Guid _editedItemId;

    [When("I edit manual grocery item {string} to quantity {string} {string} and note {string}")]
    public async Task EditManualItemAsync(string name, string quantity, string unit, string note)
    {
        using var listResponse = await context.Client.GetAsync("/api/grocery-list");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using var list = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        var item = FindItem(list, name);
        _editedItemId = item.GetProperty("id").GetGuid();
        context.Response?.Dispose();
        context.Response = await context.Client.PutAsJsonAsync(
            $"/api/grocery-list/items/{_editedItemId}",
            new
            {
                commandId = Guid.CreateVersion7(),
                expectedListVersion = list.RootElement.GetProperty("version").GetUInt32(),
                outstandingPurchaseQuantity = decimal.Parse(quantity, CultureInfo.InvariantCulture),
                purchaseUnit = unit,
                note,
                departmentUpdate = (object?)null,
                adHocStoreId = (Guid?)null,
                adHocDepartmentId = (Guid?)null
            });
    }

    [Then("the grocery API updates the same manual line to quantity {string} and note {string}")]
    public async Task AssertManualEditAsync(string quantity, string note)
    {
        context.Response!.StatusCode.Should().Be(HttpStatusCode.OK);
        using var list = await ReadListAsync();
        var item = FindItem(list, "Cocoa");
        item.GetProperty("id").GetGuid().Should().Be(_editedItemId);
        item.GetProperty("outstandingPurchaseQuantity").GetDecimal()
            .Should().Be(decimal.Parse(quantity, CultureInfo.InvariantCulture));
        item.GetProperty("note").GetString().Should().Be(note);
    }

    [When("I submit one manual item {string} with quantity {string} {string}")]
    public Task SubmitSingleAsync(string name, string quantity, string unit) =>
        SubmitSingleAsync(name, quantity, unit, false);

    [When("I submit one manual item {string} with quantity {string} {string} and save it as an Ingredient")]
    public Task SubmitSingleIngredientAsync(string name, string quantity, string unit) =>
        SubmitSingleAsync(name, quantity, unit, true);

    [When("I submit a second manual increment for {string} with quantity {string} {string}")]
    public Task SubmitSecondIncrementAsync(string name, string quantity, string unit) =>
        SubmitSingleAsync(name, quantity, unit, false);

    [When("I submit duplicate manual rows for {string}")]
    public Task SubmitDuplicateBatchAsync(string name) =>
        SubmitAsync(
        [
            new(name, null, 1, "tin"),
            new(name, null, 2, "tin")
        ]);

    [When("I replay the same grocery command")]
    public Task ReplayCommandAsync() => SendAsync(_command!);

    [When("I confirm the duplicate review")]
    public Task ConfirmDuplicateReviewAsync()
    {
        _command = _command! with { ReviewedDuplicates = _reviews };
        return SendAsync(_command);
    }

    [Then("the grocery API creates one manual line named {string} with quantity {string}")]
    public async Task AssertManualLineAsync(string name, string quantity)
    {
        context.Response!.StatusCode.Should().Be(HttpStatusCode.Created);
        context.Response.Headers.Location.Should().NotBeNull();
        using var list = await ReadListAsync();
        var item = FindItem(list, name);
        item.GetProperty("outstandingPurchaseQuantity").GetDecimal()
            .Should().Be(decimal.Parse(quantity, CultureInfo.InvariantCulture));
        var contributions = item.GetProperty("contributions").EnumerateArray().ToArray();
        contributions.Should().HaveCount(decimal.Parse(quantity, CultureInfo.InvariantCulture) == 3 ? 2 : 1);
        contributions.Should().OnlyContain(contribution =>
            contribution.GetProperty("origin").GetString() == "manual");
    }

    [Then("the grocery API returns the same command without a second increment")]
    public async Task AssertIdempotentReplayAsync()
    {
        context.Response!.StatusCode.Should().Be(HttpStatusCode.Created);
        using var list = await ReadListAsync();
        var item = FindItem(list, _command!.Items[0].Name!);
        item.GetProperty("outstandingPurchaseQuantity").GetDecimal().Should().Be(2);
        item.GetProperty("contributions").GetArrayLength().Should().Be(1);
    }

    [Then("the grocery API requests duplicate review without changing the line")]
    public async Task AssertDuplicateReviewAsync()
    {
        context.Response!.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var problem = JsonDocument.Parse(await context.Response.Content.ReadAsStringAsync());
        _reviews = JsonSerializer.Deserialize<GroceryDuplicateReview[]>(
            problem.RootElement.GetProperty("duplicateReviews").GetRawText(),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        _reviews.Should().ContainSingle(review =>
            review.ExistingQuantity == 1
            && review.Increment == 2
            && review.ResultingQuantity == 3);

        using var list = await ReadListAsync();
        FindItem(list, "Kale").GetProperty("outstandingPurchaseQuantity").GetDecimal().Should().Be(1);
    }

    [Then("the grocery API requests a review of the pending batch rows")]
    public async Task AssertBatchReviewAsync()
    {
        context.Response!.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var problem = JsonDocument.Parse(await context.Response.Content.ReadAsStringAsync());
        _reviews = JsonSerializer.Deserialize<GroceryDuplicateReview[]>(
            problem.RootElement.GetProperty("duplicateReviews").GetRawText(),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        _reviews.Should().ContainSingle(review =>
            review.EntryIndex == 1
            && review.TargetEntryIndex == 0
            && review.ExistingQuantity == 1
            && review.ResultingQuantity == 3);

        using var list = await ReadListAsync();
        list.RootElement.GetProperty("items").GetArrayLength().Should().Be(0);
    }

    [Then("the grocery API creates one linked Ingredient line named {string}")]
    public async Task AssertLinkedIngredientLineAsync(string name)
    {
        context.Response!.StatusCode.Should().Be(HttpStatusCode.Created);
        using var list = await ReadListAsync();
        var item = FindItem(list, name);
        item.GetProperty("ingredientId").ValueKind.Should().Be(JsonValueKind.String);
        item.GetProperty("contributions").GetArrayLength().Should().Be(1);
    }

    private async Task SubmitSingleAsync(string name, string quantity, string unit, bool saveAsIngredient) =>
        await SubmitAsync(
        [
            new(
                name,
                null,
                decimal.Parse(quantity, CultureInfo.InvariantCulture),
                unit,
                SaveAsIngredient: saveAsIngredient)
        ]);

    private async Task SubmitAsync(IReadOnlyList<GroceryManualEntryRequest> entries)
    {
        using var response = await context.Client.GetAsync("/api/grocery-list");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var list = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var version = list.RootElement.GetProperty("version").GetUInt32();
        _command = new GroceryManualEntryCommand(
            Guid.CreateVersion7(),
            version,
            entries,
            []);

        await SendAsync(_command);
    }

    private async Task SendAsync(GroceryManualEntryCommand command)
    {
        context.Response?.Dispose();
        context.Response = await context.Client.PostAsJsonAsync("/api/grocery-list/items", command);
    }

    private async Task<JsonDocument> ReadListAsync()
    {
        using var response = await context.Client.GetAsync("/api/grocery-list");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static JsonElement FindItem(JsonDocument list, string name) =>
        list.RootElement.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("name").GetString() == name);
}
