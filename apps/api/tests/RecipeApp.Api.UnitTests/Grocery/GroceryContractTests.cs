using System.Text.Json;
using FluentAssertions;
using RecipeApp.Api.Grocery;

namespace RecipeApp.Api.UnitTests.Grocery;

public sealed class GroceryContractTests
{
    [Theory]
    [InlineData("\"name\":\"Changed\"")]
    [InlineData("\"categoryId\":null")]
    [InlineData("\"storeMappings\":[]")]
    [InlineData("\"departmentUpdate\":{\"storeMappings\":[]}")]
    public void Shopping_edits_reject_unknown_catalogue_fields(string forbiddenProperty)
    {
        var json = "{\"commandId\":\"00000000-0000-7000-8000-000000000001\","
            + "\"expectedListVersion\":1,\"outstandingPurchaseQuantity\":2,\"purchaseUnit\":\"item\","
            + forbiddenProperty + "}";

        var deserialize = () => JsonSerializer.Deserialize<GroceryItemUpdateRequest>(
            json, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        deserialize.Should().Throw<JsonException>();
    }

    [Theory]
    [InlineData(9_999, "g", true)]
    [InlineData(0.125, "ml", true)]
    [InlineData(1, "tin", true)]
    [InlineData(0, "g", false)]
    [InlineData(-1, "g", false)]
    [InlineData(10_000, "g", false)]
    [InlineData(1.5, "tin", false)]
    [InlineData(1.0000000001, "g", false)]
    [InlineData(1, "kg", false)]
    [InlineData(1, "unknown", false)]
    public void Purchase_input_validation_enforces_limits_and_supported_units(
        decimal quantity,
        string unit,
        bool expectedValid)
    {
        var error = GroceryPurchaseQuantityContract.Validate(quantity, unit);

        (error is null).Should().Be(expectedValid);
    }

    [Fact]
    public void Manual_entry_contract_enforces_batch_name_and_note_limits()
    {
        var errors = GroceryManualEntryContract.ValidateBatch(
        [
            new("flour", new string('n', GroceryDecimalContract.MaximumNoteLength + 1)),
            new(new string('x', GroceryDecimalContract.MaximumNameLength + 1), null)
        ]);

        errors.Keys.Should().BeEquivalentTo("items[0].note", "items[1].name");
    }

    [Fact]
    public void Command_fingerprint_is_stable_and_changes_with_the_request()
    {
        var first = GroceryCommandFingerprint.Compute(
            "purchase",
            new GroceryPurchaseRequest(Guid.Parse("00000000-0000-7000-8000-000000000001"), 3, 2, "tin"));
        var retry = GroceryCommandFingerprint.Compute(
            "purchase",
            new GroceryPurchaseRequest(Guid.Parse("00000000-0000-7000-8000-000000000001"), 3, 2, "tin"));
        var changed = GroceryCommandFingerprint.Compute(
            "purchase",
            new GroceryPurchaseRequest(Guid.Parse("00000000-0000-7000-8000-000000000001"), 3, 1, "tin"));

        first.Should().Be(retry);
        first.Should().NotBe(changed);
        first.Should().HaveLength(64);
    }
}
