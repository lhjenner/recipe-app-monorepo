using System.Text.Json;
using FirestoreMigration;
using FluentAssertions;

namespace RecipeApp.Api.UnitTests.Migration;

public sealed class MigrationTests
{
    private const string Id = "117c8374-5db4-4021-b106-5b466e2446ed";
    private const string Meta = """
        [{"id":"categories","list":["Baking"]},{"id":"departments","list":["Aisle 7"]},
         {"id":"supermarkets","supermarkets":[{"id":"store","chain":"Chain","name":"Store","location":"Town"}]}]
        """;

    private static MigrationSource Parse(string ingredients, string decisions = "{}") =>
        SourceParser.Parse(ingredients, Meta, decisions);

    [Fact]
    public void Defaults_to_dry_run_and_requires_explicit_inputs()
    {
        var options = MigrationOptions.Parse(["--backup", "backup", "--owner", Id, "--decisions", "decisions.json"]);
        options.Write.Should().BeFalse();
        var invalid = () => MigrationOptions.Parse(["--accept-all"]);
        invalid.Should().Throw<FormatException>();
    }

    [Fact]
    public void Maps_three_store_states_and_source_nutrition_timestamp()
    {
        var source = Parse($$$$"""
            [{"id":"{{{{Id}}}}","name":"Syrup","category":"Baking",
              "departments":{"store":"N/A","empty":""},
              "nutrition":{"per100g":{"calories":311,"sodium":25},
                "lastUpdated":{"_seconds":1769393852,"_nanoseconds":474000000},"manuallyEdited":true},
              "preferredVariant":"Golden","productUrls":{"chain":[
                {"url":"https://example.test/syrup","variantLabel":"Golden","note":"large","supportsBothPricingMethods":true}]}}]
            """);
        source.Errors.Should().BeEmpty();
        var ingredient = source.Ingredients.Single();
        ingredient.Mappings.Should().ContainSingle().Which.IsNotStocked.Should().BeTrue();
        ingredient.Nutrition.EnergyKcal.Should().Be(311);
        ingredient.Nutrition.SodiumMg.Should().Be(25);
        ingredient.Nutrition.UpdatedAtUtc.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1769393852)
            .AddTicks(4740000).UtcDateTime);
        ingredient.Links.Single().IsPreferred.Should().BeTrue();
        source.Ignored.Should().ContainKey("ingredients.nutrition.manuallyEdited");
    }

    [Fact]
    public void Invalid_items_require_retained_guid_decisions()
    {
        var json = $$$$"""[{"id":"{{{{Id}}}}","name":"Water","invalid":true}]""";
        Parse(json).Unclassified.Should().ContainSingle();
        var classified = Parse(json, $$$$"""{"{{{{Id}}}}":{"name":"Old name","classification":"Ingredient"}}""");
        classified.Ingredients.Single().ExcludeFromGroceryList.Should().BeTrue();
        Parse(json, $$$$"""{"{{{{Id}}}}":{"name":"Water","classification":"Heading"}}""")
            .Headings.Should().Be(1);
        Parse(json, $$$$"""{"{{{{Id}}}}":{"name":"Water","classification":"Note"}}""")
            .Notes.Should().Be(1);
    }

    [Theory]
    [InlineData("""[{"name":"Missing ID"}]""")]
    [InlineData("""[{"id":"bad","name":"Bad ID"}]""")]
    [InlineData("""{}""")]
    [InlineData("""[{"id":"117c8374-5db4-4021-b106-5b466e2446ed","name":"A","name":"B"}]""")]
    public void Malformed_source_is_blocking(string json)
    {
        var act = () => Parse(json);
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Duplicate_ids_and_noninvalid_decisions_block()
    {
        var item = $$$$"""{"id":"{{{{Id}}}}","name":"Flour"}""";
        Parse($"[{item},{item}]").Errors.Should().Contain(error => error.Contains("Duplicate"));
        Parse($"[{item}]", $$$$"""{"{{{{Id}}}}":{"name":"Flour","classification":"Ingredient"}}""")
            .Errors.Should().Contain(error => error.Contains("non-invalid"));
    }

    [Theory]
    [InlineData("1.0001")]
    [InlineData("1000000")]
    [InlineData("\"not a number\"")]
    [InlineData("1e-100")]
    [InlineData("1.0000000000000000000000000000001")]
    public void Unrepresentable_nutrition_blocks_instead_of_rounding(string number)
    {
        var act = () => Parse($$$$"""[{"id":"{{{{Id}}}}","name":"Flour","nutrition":{"per100g":{"protein":{{{{number}}}}}}}]""");
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Representable_business_rule_violations_are_warnings_not_data_loss()
    {
        var source = Parse($$$$"""
            [{"id":"{{{{Id}}}}","name":"Flour","nutrition":{"per100g":{"fat":1,"saturatedFat":2,"sugar":-1}}}]
            """);
        source.Errors.Should().BeEmpty();
        source.Warnings.Should().HaveCount(2);
        source.Ingredients.Single().Nutrition.SaturatedFatG.Should().Be(2);
    }

    [Fact]
    public void Comparison_normalizes_only_lookup_references_and_includes_removals()
    {
        var value = Parse($$$$"""
            [{"id":"{{{{Id}}}}","name":"Flour","category":" Baking ",
              "departments":{"store":" AISLE   7 "},"productUrls":{"chain":[{"url":"https://example.test"}]}}]
            """).Ingredients.Single();
        var normalized = value with
        {
            Category = "baking",
            Mappings = [new MappingValue("store", "aisle 7", false)]
        };
        IngredientDiff.Compare(value, normalized).Should().BeEmpty();
        var cleared = normalized with { Category = null, Mappings = [], Links = [], Name = "flour" };
        IngredientDiff.Compare(value, cleared).Select(diff => diff.Field)
            .Should().BeEquivalentTo("Name", "Category", "Mappings[store]", "Links[chain:0]");
    }

    [Fact]
    public void Unsupported_and_duplicate_decisions_are_rejected_but_absent_decisions_are_retained()
    {
        var unsupported = () => Parse("[]", $$$$"""{"{{{{Id}}}}":{"name":"X","classification":"Auto"}}""");
        unsupported.Should().Throw<FormatException>();
        var duplicate = () => Parse("[]", $$$$"""
            {"{{{{Id}}}}":{"name":"X","classification":"Note"},"{{{{Id}}}}":{"name":"X","classification":"Heading"}}
            """);
        duplicate.Should().Throw<FormatException>();
        var source = Parse("[]", $$$$"""{"{{{{Id}}}}":{"name":"X","classification":"Note"}}""");
        source.Decisions.Should().ContainKey(Guid.Parse(Id));
        source.Warnings.Should().Contain(warning => warning.Contains("absent"));
    }

    [Fact]
    public void Ambiguous_or_missing_preferred_variants_warn_without_guessing()
    {
        var source = Parse($$$$"""
            [{"id":"{{{{Id}}}}","name":"Flour","preferredVariant":"White",
              "productUrls":{"chain":[{"url":"https://example.test/1","variantLabel":"White"},
                {"url":"https://example.test/2","variantLabel":"White"}]}}]
            """);
        source.Warnings.Should().Contain(warning => warning.Contains("preferredVariant"));
        source.Ingredients.Single().Links.Should().OnlyContain(link => !link.IsPreferred);
    }

    [Fact]
    public void Numeric_scale_is_not_a_semantic_change()
    {
        var value = Parse($$$$"""[{"id":"{{{{Id}}}}","name":"Flour","nutrition":{"per100g":{"protein":1}}}]""")
            .Ingredients.Single();
        IngredientDiff.Compare(value, value with { Nutrition = value.Nutrition with { ProteinG = 1.000m } })
            .Should().BeEmpty();
    }
}
