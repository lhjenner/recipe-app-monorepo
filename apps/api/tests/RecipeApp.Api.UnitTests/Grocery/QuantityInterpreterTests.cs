using FluentAssertions;
using RecipeApp.Api.Grocery;

namespace RecipeApp.Api.UnitTests.Grocery;

public sealed class QuantityInterpreterTests
{
    [Theory]
    [InlineData("1", "tbsp", 15)]
    [InlineData("2", "tsp", 10)]
    [InlineData("1 1/2", "tsp", 7.5)]
    [InlineData("1 / 2", "tsp", 2.5)]
    [InlineData("1 1 / 2", "tsp", 7.5)]
    [InlineData("0.5", "kg", 500)]
    [InlineData("250", "g", 250)]
    [InlineData("1", "cup", 250)]
    [InlineData("50", "ml", 50)]
    [InlineData("1.0000000000000000000000000000000000000000", "g", 1)]
    [InlineData("1", "tin", 1)]
    public void Interprets_supported_quantities_without_rounding(
        string amount,
        string unit,
        decimal expected)
    {
        var result = QuantityInterpreter.Interpret(amount, unit);

        result.Status.Should().Be(QuantityInterpretationStatus.Known);
        result.Amount.Should().Be(expected);
    }

    [Theory]
    [InlineData("1-2", "tsp")]
    [InlineData("2 heaped", "tbsp")]
    [InlineData("", "g")]
    [InlineData("0", "tsp")]
    [InlineData("-1", "g")]
    [InlineData("1/0", "tsp")]
    [InlineData("1/3", "tsp")]
    [InlineData("1.00000000000000000000000000001", "g")]
    [InlineData("79228162514264337593543950334/79228162514264337593543950335", "tsp")]
    [InlineData("10000000000000000000", "g")]
    [InlineData("2", "unknown-unit")]
    [InlineData("1", "US cup")]
    public void Keeps_unsupported_source_text_unresolved(string amount, string unit)
    {
        var result = QuantityInterpreter.Interpret(amount, unit);

        result.Status.Should().Be(QuantityInterpretationStatus.Unresolved);
        result.Amount.Should().BeNull();
        result.OriginalAmountText.Should().Be(amount);
        result.OriginalUnitText.Should().Be(unit);
        result.UnresolvedReason.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("tins", "tin")]
    [InlineData("packets", "packet")]
    [InlineData("pieces", "piece")]
    public void Canonicalizes_only_equivalent_count_labels(string input, string expected)
    {
        var result = QuantityInterpreter.Interpret("1", input);

        result.Status.Should().Be(QuantityInterpretationStatus.Known);
        result.Dimension.Should().Be(QuantityDimension.Count);
        result.CountLabel.Should().Be(expected);
    }

    [Fact]
    public void Count_labels_and_requirement_dimensions_produce_distinct_compatibility_keys()
    {
        var ingredientId = Guid.CreateVersion7();

        var tinKey = GroceryConsolidationKey.ForIngredient(
            ingredientId,
            QuantityDimension.Count,
            "tin");
        var pieceKey = GroceryConsolidationKey.ForIngredient(
            ingredientId,
            QuantityDimension.Count,
            "piece");
        var massKey = GroceryConsolidationKey.ForIngredient(
            ingredientId,
            QuantityDimension.Mass,
            null);

        tinKey.Should().NotBe(pieceKey);
        tinKey.Should().NotBe(massKey);
    }

    [Fact]
    public void Unresolved_consolidation_keys_are_bounded_and_normalize_long_source_units()
    {
        var ingredientId = Guid.CreateVersion7();
        var longUnit = new string('u', 500);

        var key = GroceryConsolidationKey.ForUnresolvedIngredient(ingredientId, longUnit);

        key.Should().HaveLength(64);
        GroceryConsolidationKey.ForUnresolvedIngredient(
                ingredientId,
                $"  {longUnit}  ")
            .Should().Be(key);
        GroceryConsolidationKey.ForUnresolvedIngredient(
                Guid.CreateVersion7(),
                longUnit)
            .Should().NotBe(key);
    }

    [Theory]
    [InlineData("1", "tbsp", "2", "tsp", 25, "ml")]
    [InlineData("2", "tbsp", "1", "tsp", 35, "ml")]
    [InlineData("1", "cup", "50", "ml", 300, "ml")]
    [InlineData("0.5", "kg", "250", "g", 750, "g")]
    [InlineData("1/2", "tsp", "1 1/2", "tsp", 10, "ml")]
    [InlineData("2", "tin", "1", "tins", 3, "tin")]
    [InlineData("2", "onions", "1", "onion", 3, "onion")]
    [InlineData("1", "packet", "1", "packets", 2, "packet")]
    [InlineData("1", "tsp", "1", "cup", 255, "ml")]
    public void Consolidates_the_prd_examples_exactly(
        string firstAmount,
        string firstUnit,
        string secondAmount,
        string secondUnit,
        decimal expectedAmount,
        string expectedUnit)
    {
        var first = QuantityInterpreter.Interpret(firstAmount, firstUnit);
        var result = QuantityConsolidation.Consolidate(
        [
            first,
            QuantityInterpreter.Interpret(secondAmount, secondUnit)
        ]);

        result.KnownTotals.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new KnownQuantityTotal(
                expectedAmount,
                first.Dimension!.Value,
                expectedUnit,
                first.CountLabel));
    }

    [Fact]
    public void Unresolved_contributions_are_preserved_but_excluded_from_known_totals()
    {
        var unresolved = QuantityInterpreter.Interpret("1-2", "tsp");

        var result = QuantityConsolidation.Consolidate(
        [
            QuantityInterpreter.Interpret("1", "tbsp"),
            unresolved
        ]);

        result.KnownTotals.Should().ContainSingle()
            .Which.Amount.Should().Be(15);
        result.UnresolvedContributions.Should().ContainSingle()
            .Which.OriginalAmountText.Should().Be("1-2");
    }

    [Fact]
    public void Consolidated_demand_is_not_limited_by_the_purchase_input_cap()
    {
        var result = QuantityConsolidation.Consolidate(
        [
            QuantityInterpreter.Interpret("9999", "tin"),
            QuantityInterpreter.Interpret("9999", "tin")
        ]);

        result.KnownTotals.Should().ContainSingle()
            .Which.Amount.Should().Be(19_998);
    }

    [Fact]
    public void Count_and_mass_requirements_for_one_ingredient_remain_separate()
    {
        var result = QuantityConsolidation.Consolidate(
        [
            QuantityInterpreter.Interpret("5", "mushrooms"),
            QuantityInterpreter.Interpret("250", "g")
        ]);

        result.KnownTotals.Should().BeEquivalentTo(
        [
            new KnownQuantityTotal(5, QuantityDimension.Count, "mushroom", "mushroom"),
            new KnownQuantityTotal(250, QuantityDimension.Mass, "g", null)
        ]);
    }

    [Fact]
    public void Recipe_and_manual_count_contributions_share_one_exact_total()
    {
        var result = QuantityConsolidation.Consolidate(
        [
            QuantityInterpreter.Interpret("2", "onions"),
            QuantityInterpreter.Interpret("1", "onion"),
            QuantityInterpreter.Interpret("1", "onion")
        ]);

        result.KnownTotals.Should().ContainSingle()
            .Which.Amount.Should().Be(4);
    }

    [Fact]
    public void Consolidation_rejects_values_that_exceed_storage_precision_instead_of_rounding()
    {
        var maximum = QuantityInterpreter.Interpret(
            "9999999999999999999.999999999",
            "g");
        var additionalGram = QuantityInterpreter.Interpret("1", "g");

        var act = () => QuantityConsolidation.Consolidate([maximum, additionalGram]);

        act.Should().Throw<OverflowException>();
    }

    [Fact]
    public void Unlinked_identity_uses_normalized_name_without_becoming_a_catalogue_link()
    {
        var ingredientId = Guid.CreateVersion7();

        var linkedKey = GroceryConsolidationKey.ForIngredient(
            ingredientId,
            QuantityDimension.Volume,
            null);
        var unlinkedKey = GroceryConsolidationKey.ForName(
            "  Fresh   Basil ",
            QuantityDimension.Volume,
            null);

        linkedKey.Should().NotBe(unlinkedKey);
        GroceryConsolidationKey.ForName(
                "fresh basil",
                QuantityDimension.Volume,
                null)
            .Should().Be(unlinkedKey);
    }
}
