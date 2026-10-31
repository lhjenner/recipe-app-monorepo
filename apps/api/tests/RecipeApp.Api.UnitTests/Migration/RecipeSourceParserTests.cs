using System.Reflection;
using FirestoreMigration;
using FluentAssertions;

namespace RecipeApp.Api.UnitTests.Migration;

public sealed class RecipeSourceParserTests
{
    private static string Fixture(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resource = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith(fileName, StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [Fact]
    public void Maps_recipe_fields_and_preserves_ordered_content_and_decisions()
    {
        var source = RecipeSourceParser.Parse(
            Fixture("recipe-source.json"),
            Fixture("recipe-decisions.json"));

        source.Recipes.Should().HaveCount(2);
        var recipe = source.Recipes[0];
        recipe.LegacyId.Should().Be("legacy-recipe-001");
        recipe.Name.Should().Be("Synthetic soup");
        recipe.SourceText.Should().Be("Synthetic cookbook");
        recipe.SourceUrl.Should().Be("https://example.test/soup");
        recipe.ServingsText.Should().Be("4-6");
        recipe.PrepTimeText.Should().Be("about 20 minutes");
        recipe.CookTimeText.Should().Be("1-2 hours");
        recipe.Tags.Should().Equal("Family", "Dinner", "Family");
        recipe.Steps.Select(step => step.Position).Should().Equal(0, 1);
        recipe.Steps[0].Instructions.Should().Be("Mix gently.\nDo not overwork.");
        recipe.Steps[0].Items.Select(item => item.Kind)
            .Should().Equal(RecipeSourceItemKind.Ingredient, RecipeSourceItemKind.Heading, RecipeSourceItemKind.Note);
        recipe.Steps[0].Items.Select(item => item.Position).Should().Equal(0, 1, 2);
        recipe.Steps[0].Items[0].IngredientId.Should().Be(Guid.Parse("117c8374-5db4-4021-b106-5b466e2446ed"));
        recipe.Steps[0].Items[0].QuantityText.Should().Be("1/2");
        recipe.Steps[0].Items[0].UnitText.Should().Be("cup");
        recipe.Steps[0].Items[0].NotesText.Should().Be("sifted");
        recipe.Steps[0].Items[1].Text.Should().Be("Optional:");
        recipe.Steps[0].Items[1].QuantityText.Should().Be("to taste");
        recipe.Steps[0].Items[1].NotesText.Should().Be("if desired");
        recipe.Steps[0].Items[2].Kind.Should().Be(RecipeSourceItemKind.Note);
        recipe.Steps[1].Items.Should().BeEmpty();
        source.BlankPlaceholders.Should().ContainSingle()
            .Which.JsonPath.Should().Be("recipes[0].steps[0].ingredients[3]");
    }

    [Fact]
    public void Parses_supported_timestamp_shapes_and_reports_archive_and_owner_defaults()
    {
        var source = RecipeSourceParser.Parse(
            Fixture("recipe-source.json"),
            Fixture("recipe-decisions.json"));

        source.Recipes[0].DateAddedAtUtc.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1769393852)
            .AddTicks(4740000).UtcDateTime);
        source.Recipes[1].DateAddedAtUtc.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1769393853)
            .AddTicks(1234560).UtcDateTime);
        source.Recipes.Select(recipe => recipe.IsArchived).Should().Equal(true, false);
        source.MissingArchiveCount.Should().Be(1);
        source.OwnerFieldCounts.Present.Should().Be(1);
        source.OwnerFieldCounts.Missing.Should().Be(1);
        source.Warnings.Should().Contain(warning => warning.Contains("archived"));
    }

    [Fact]
    public void Reports_unknown_fields_and_retains_original_source_documents()
    {
        var source = RecipeSourceParser.Parse(
            Fixture("recipe-source.json"),
            Fixture("recipe-decisions.json"));

        source.Recipes[0].SourceDocumentJson.Should().Contain("\"futureMetadata\"");
        source.UnknownFieldPaths.Should().Contain("recipes[0].futureMetadata");
        source.UnknownFieldPaths.Should().Contain("recipes[0].steps[0].ingredients[0].department");
    }

    [Fact]
    public void Missing_dates_are_preserved_as_null_and_structural_text_is_a_blocker()
    {
        const string recipe = """
            [{
              "id":"legacy-recipe",
              "title":"Synthetic recipe",
              "tags":[],
              "steps":[{
                "instructions":"",
                "ingredients":[{"id":"217c8374-5db4-4021-b106-5b466e2446ed","name":""}]
              }]
            }]
            """;

        var source = RecipeSourceParser.Parse(recipe, Fixture("recipe-decisions.json"));

        source.Recipes.Single().DateAddedAtUtc.Should().BeNull();
        source.MissingDateCount.Should().Be(1);
        source.BlockingDiagnostics.Should().ContainSingle()
            .Which.Should().Contain("steps[0].ingredients[0].name");
    }

    [Theory]
    [InlineData("""[{"id":"legacy-recipe","title":"Soup","dateAdded":{"_seconds":1,"_nanoseconds":1},"tags":[],"steps":[]}]""")]
    [InlineData("""[{"id":"legacy-recipe","title":"Soup","dateAdded":{"type":"firestore/timestamp/1.0","seconds":1,"nanoseconds":1000000000},"tags":[],"steps":[]}]""")]
    [InlineData("""[{"id":"legacy-recipe","title":"Soup","dateAdded":{"type":"unsupported","seconds":1,"nanoseconds":0},"tags":[],"steps":[]}]""")]
    public void Rejects_lossy_or_invalid_timestamp_encodings(string recipes)
    {
        var act = () => RecipeSourceParser.Parse(recipes, "{}");

        act.Should().Throw<FormatException>();
    }

    [Theory]
    [InlineData("""[{"id":"","title":"Soup","tags":[],"steps":[]}]""")]
    [InlineData("""[{"id":"legacy-recipe","title":"Soup","tags":[],"steps":[{"instructions":"Mix","ingredients":[{"id":"","name":"Flour"}]}]}]""")]
    [InlineData("""[{"id":"legacy-recipe","title":"Soup","tags":[],"steps":[{"instructions":"Mix","ingredients":[{"id":"bad","name":"Flour"}]}]}]""")]
    public void Rejects_missing_recipe_or_meaningful_line_ids(string recipes)
    {
        var act = () => RecipeSourceParser.Parse(recipes, "{}");

        act.Should().Throw<FormatException>();
    }
}
