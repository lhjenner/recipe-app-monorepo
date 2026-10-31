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
    public void Reports_source_field_dispositions_and_ordered_step_item_outcomes()
    {
        var source = RecipeSourceParser.Parse(
            Fixture("recipe-source.json"),
            Fixture("recipe-decisions.json"));

        var report = RecipeMigrationReportBuilder.BuildSourceDispositions(
            source, new HashSet<string>(["legacy-recipe-001"], StringComparer.Ordinal));

        report.Should().HaveCount(2);
        var firstRecipe = report[0];
        firstRecipe.LegacyId.Should().Be("legacy-recipe-001");
        firstRecipe.Selected.Should().BeTrue();
        firstRecipe.Fields.Should().Contain(field =>
                field.JsonPath == "recipes[0].title" && field.Disposition == "Recipe.Name")
            .And.Contain(field =>
                field.JsonPath == "recipes[0].futureMetadata" &&
                field.Disposition == "retained-in-provenance")
            .And.Contain(field =>
                field.JsonPath == "recipes[0].userId" &&
                field.Disposition == "retained-in-provenance; legacy-owner-selection-only")
            .And.Contain(field =>
                field.JsonPath == "recipes[0].dateAdded._seconds" &&
                field.Disposition == "Recipe.DateAddedAtUtc")
            .And.Contain(field =>
                field.JsonPath == "recipes[0].steps[0].ingredients[0].department" &&
                field.Disposition == "retained-in-provenance");
        firstRecipe.Steps.Select(step => step.Position).Should().Equal(0, 1);
        firstRecipe.Steps[0].SourceItemCount.Should().Be(4);
        firstRecipe.Steps[0].Items.Select(item => item.Position).Should().Equal(0, 1, 2, 3);
        firstRecipe.Steps[0].Items.Select(item => item.Disposition).Should()
            .Equal("ingredient", "heading", "note", "blank-placeholder-omitted");
        firstRecipe.Steps[0].Items[0].IngredientId.Should()
            .Be(Guid.Parse("117c8374-5db4-4021-b106-5b466e2446ed"));
        firstRecipe.Steps[0].Items[1].IngredientId.Should().BeNull();
        firstRecipe.Steps[1].SourceItemCount.Should().Be(0);
        report[1].Selected.Should().BeFalse();
    }

    [Fact]
    public void Reports_planned_structural_conversion_and_missing_ingredient_occurrences()
    {
        const string recipe = """
            [{
              "id":"legacy-recipe",
              "title":"Synthetic recipe",
              "tags":[],
              "steps":[{
                "instructions":"Mix",
                "ingredients":[
                  {"id":"117c8374-5db4-4021-b106-5b466e2446ed","name":"Flour"},
                  {"id":"117c8374-5db4-4021-b106-5b466e2446ed","name":"Flour"},
                  {"id":"217c8374-5db4-4021-b106-5b466e2446ed","name":"Optional:"},
                  {"id":"317c8374-5db4-4021-b106-5b466e2446ed","name":"To serve"}
                ]
              }]
            }]
            """;
        var source = RecipeSourceParser.Parse(recipe, Fixture("recipe-decisions.json"));
        var owner = Guid.Parse("417c8374-5db4-4021-b106-5b466e2446ed");
        var validation = RecipeSourceValidator.Validate(source, owner, []);

        validation.MissingIngredientReferences.Should().Be(2);
        validation.CrossOwnerIngredientReferences.Should().Be(0);
        var summary = RecipeMigrationReportBuilder.BuildSummary(
            validation.Recipes, validation);

        summary.PlannedHeadingsConverted.Should().Be(1);
        summary.PlannedNotesConverted.Should().Be(1);
        summary.MissingIngredientReferences.Should().Be(2);
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

    [Fact]
    public void Validates_same_owner_ingredient_references_and_keeps_archived_or_excluded_ingredients_valid()
    {
        var source = RecipeSourceParser.Parse(
            Fixture("recipe-source.json"),
            Fixture("recipe-decisions.json"));
        var owner = Guid.Parse("417c8374-5db4-4021-b106-5b466e2446ed");
        var ingredients = new[]
        {
            new RecipeIngredientTarget(Guid.Parse("117c8374-5db4-4021-b106-5b466e2446ed"), owner, IsArchived: true),
        };

        var result = RecipeSourceValidator.Validate(source, owner, ingredients);

        result.BlockingDiagnostics.Should().BeEmpty();
        result.Recipes.Should().HaveCount(2);
    }

    [Fact]
    public void Blocks_missing_and_different_owner_ingredient_references_with_source_paths()
    {
        const string recipe = """
            [{
              "id":"legacy-recipe",
              "title":"Synthetic recipe",
              "tags":[],
              "steps":[{
                "instructions":"Mix",
                "ingredients":[
                  {"id":"117c8374-5db4-4021-b106-5b466e2446ed","name":"Flour"},
                  {"id":"217c8374-5db4-4021-b106-5b466e2446ed","name":"Sugar"}
                ]
              }]
            }]
            """;
        var source = RecipeSourceParser.Parse(recipe, "{}");
        var owner = Guid.Parse("417c8374-5db4-4021-b106-5b466e2446ed");
        var otherOwner = Guid.Parse("517c8374-5db4-4021-b106-5b466e2446ed");

        var result = RecipeSourceValidator.Validate(source, owner,
        [
            new(Guid.Parse("117c8374-5db4-4021-b106-5b466e2446ed"), owner),
            new(Guid.Parse("217c8374-5db4-4021-b106-5b466e2446ed"), otherOwner)
        ]);

        result.BlockingDiagnostics.Should().Contain(diagnostic =>
            diagnostic.Contains("recipes[0].steps[0].ingredients[1]") &&
            diagnostic.Contains("different owner"));
        result.BlockingDiagnostics.Should().NotContain(diagnostic =>
            diagnostic.Contains("recipes[0].steps[0].ingredients[0]"));

        var missing = RecipeSourceValidator.Validate(source, owner, []);
        missing.BlockingDiagnostics.Should().Contain(diagnostic =>
            diagnostic.Contains("recipes[0].steps[0].ingredients[0]") &&
            diagnostic.Contains("does not exist"));
    }

    [Theory]
    [InlineData("217c8374-5db4-4021-b106-5b466e2446ed", "Heading")]
    [InlineData("317c8374-5db4-4021-b106-5b466e2446ed", "Note")]
    public void Blocks_structural_decisions_that_conflict_with_an_existing_ingredient(string id, string kind)
    {
        var source = RecipeSourceParser.Parse(
            Fixture("recipe-source.json"),
            Fixture("recipe-decisions.json"));
        var owner = Guid.Parse("417c8374-5db4-4021-b106-5b466e2446ed");

        var result = RecipeSourceValidator.Validate(source, owner,
        [
            new(Guid.Parse(id), owner)
        ]);

        result.BlockingDiagnostics.Should().Contain(diagnostic =>
            diagnostic.Contains($"classified as {kind}") &&
            diagnostic.Contains(id));
    }

    [Fact]
    public void Blocks_structural_decisions_that_match_an_ingredient_owned_by_another_user()
    {
        var source = RecipeSourceParser.Parse(
            Fixture("recipe-source.json"),
            Fixture("recipe-decisions.json"));
        var otherOwner = Guid.Parse("517c8374-5db4-4021-b106-5b466e2446ed");

        var result = RecipeSourceValidator.Validate(source, Guid.NewGuid(),
        [
            new(Guid.Parse("217c8374-5db4-4021-b106-5b466e2446ed"), otherOwner)
        ]);

        result.BlockingDiagnostics.Should().Contain(diagnostic =>
            diagnostic.Contains("classified as Heading") &&
            diagnostic.Contains("217c8374-5db4-4021-b106-5b466e2446ed"));
    }

    [Fact]
    public void Requires_an_explicit_allowed_source_set_for_multi_owner_exports()
    {
        const string recipes = """
            [
              {"id":"recipe-a","title":"A","userId":"legacy-owner-a","tags":[],"steps":[]},
              {"id":"recipe-b","title":"B","userId":"legacy-owner-b","tags":[],"steps":[]}
            ]
            """;
        var source = RecipeSourceParser.Parse(recipes, "{}");
        var owner = Guid.Parse("417c8374-5db4-4021-b106-5b466e2446ed");

        var ambiguous = RecipeSourceValidator.Validate(source, owner, []);
        ambiguous.BlockingDiagnostics.Should().Contain(diagnostic => diagnostic.Contains("explicit source-owner selection"));
        ambiguous.Recipes.Should().BeEmpty();

        var selected = RecipeSourceValidator.Validate(source, owner, [],
            new HashSet<string>(["legacy-owner-b"], StringComparer.Ordinal));
        selected.BlockingDiagnostics.Should().BeEmpty();
        selected.Recipes.Should().ContainSingle().Which.LegacyId.Should().Be("recipe-b");

        var unknownSelection = RecipeSourceValidator.Validate(source, owner, [],
            new HashSet<string>(["not-in-export"], StringComparer.Ordinal));
        unknownSelection.BlockingDiagnostics.Should().Contain(diagnostic =>
            diagnostic.Contains("not present in the export"));
    }

    [Fact]
    public void Blocks_unowned_recipes_when_selecting_from_a_multi_owner_export()
    {
        const string recipes = """
            [
              {"id":"recipe-a","title":"A","userId":"legacy-owner-a","tags":[],"steps":[]},
              {"id":"recipe-b","title":"B","userId":"legacy-owner-b","tags":[],"steps":[]},
              {"id":"recipe-c","title":"C","tags":[],"steps":[]}
            ]
            """;
        var source = RecipeSourceParser.Parse(recipes, "{}");

        var result = RecipeSourceValidator.Validate(source, Guid.NewGuid(), [],
            new HashSet<string>(["legacy-owner-a"], StringComparer.Ordinal));

        result.BlockingDiagnostics.Should().Contain(diagnostic =>
            diagnostic.Contains("missing legacy owner IDs") &&
            diagnostic.Contains("multi-owner export"));
        result.Recipes.Should().ContainSingle().Which.LegacyId.Should().Be("recipe-a");
    }

    [Fact]
    public void Preview_matches_only_exact_owner_scoped_legacy_ids_and_does_not_assign_new_ids()
    {
        var source = RecipeSourceParser.Parse(
            Fixture("recipe-source.json"),
            Fixture("recipe-decisions.json"));
        var owner = Guid.Parse("417c8374-5db4-4021-b106-5b466e2446ed");
        var ingredientValidation = RecipeSourceValidator.Validate(source, owner,
        [
            new(Guid.Parse("117c8374-5db4-4021-b106-5b466e2446ed"), owner)
        ]);
        var first = source.Recipes[0];
        var target = Target(first, Guid.Parse("617c8374-5db4-4021-b106-5b466e2446ed"), owner);

        var plan = RecipePreviewPlanner.Create(ingredientValidation, owner, [target]);

        plan.Recipes.Should().ContainSingle(recipe => recipe.LegacyId == first.LegacyId)
            .Which.Outcome.Should().Be("unchanged");
        plan.Recipes.Should().ContainSingle(recipe => recipe.LegacyId == "legacy-recipe-002")
            .Which.Outcome.Should().Be("proposed-insert");
        plan.ProposedInserts.Should().Be(1);
        plan.ProposedUpdates.Should().Be(0);
        plan.Unchanged.Should().Be(1);
        plan.Recipes.Single(recipe => recipe.LegacyId == "legacy-recipe-002")
            .ExistingRecipeId.Should().BeNull();
    }

    [Fact]
    public void Preview_includes_provenance_and_typed_field_differences_for_updates()
    {
        var source = RecipeSourceParser.Parse(
            Fixture("recipe-source.json"),
            Fixture("recipe-decisions.json"));
        var owner = Guid.Parse("417c8374-5db4-4021-b106-5b466e2446ed");
        var validation = RecipeSourceValidator.Validate(source, owner,
        [
            new(Guid.Parse("117c8374-5db4-4021-b106-5b466e2446ed"), owner)
        ]);
        var recipe = source.Recipes[0];
        var target = Target(recipe, Guid.Parse("617c8374-5db4-4021-b106-5b466e2446ed"), owner) with
        {
            Name = "Renamed in target",
            SourceDocumentJson = """{"id":"legacy-recipe-001","title":"Synthetic soup"}"""
        };

        var plan = RecipePreviewPlanner.Create(validation, owner, [target]);

        var update = plan.Recipes.Single(recipePlan => recipePlan.LegacyId == recipe.LegacyId);
        update.Outcome.Should().Be("proposed-update");
        update.ExistingRecipeId.Should().Be(target.Id);
        update.Differences.Should().Contain("Name").And.Contain("SourceProvenance");
    }

    [Fact]
    public void Preview_does_not_match_case_distinct_legacy_ids()
    {
        var source = RecipeSourceParser.Parse(
            Fixture("recipe-source.json"),
            Fixture("recipe-decisions.json"));
        var owner = Guid.Parse("417c8374-5db4-4021-b106-5b466e2446ed");
        var validation = RecipeSourceValidator.Validate(source, owner,
        [
            new(Guid.Parse("117c8374-5db4-4021-b106-5b466e2446ed"), owner)
        ]);
        var target = Target(source.Recipes[0] with { LegacyId = "LEGACY-RECIPE-001" },
            Guid.Parse("617c8374-5db4-4021-b106-5b466e2446ed"), owner);

        var plan = RecipePreviewPlanner.Create(validation, owner, [target]);

        plan.Recipes.Single(recipe => recipe.LegacyId == "legacy-recipe-001")
            .Outcome.Should().Be("proposed-insert");
        plan.Recipes.Single(recipe => recipe.LegacyId == "legacy-recipe-001")
            .ExistingRecipeId.Should().BeNull();
    }

    [Fact]
    public void Provenance_object_property_order_is_not_a_change_but_array_order_is()
    {
        const string recipeJson = """
            [{"id":"legacy-recipe","title":"Soup","tags":["A","B"],"steps":[],"extra":{"a":1,"b":2}}]
            """;
        var source = RecipeSourceParser.Parse(recipeJson, "{}");
        var owner = Guid.Parse("417c8374-5db4-4021-b106-5b466e2446ed");
        var validation = RecipeSourceValidator.Validate(source, owner, []);
        var target = Target(source.Recipes.Single(), Guid.NewGuid(), owner) with
        {
            SourceDocumentJson = """
                {"extra":{"b":2,"a":1},"steps":[],"tags":["A","B"],"title":"Soup","id":"legacy-recipe"}
                """
        };

        var unchanged = RecipePreviewPlanner.Create(validation, owner, [target]);
        unchanged.Recipes.Single().Outcome.Should().Be("unchanged");

        var reordered = RecipePreviewPlanner.Create(validation, owner,
        [
            target with
            {
                Tags = ["B", "A"],
                SourceDocumentJson = """{"id":"legacy-recipe","title":"Soup","tags":["B","A"],"steps":[],"extra":{"a":1,"b":2}}"""
            }
        ]);
        reordered.Recipes.Single().Differences.Should().Contain("Tags").And.Contain("SourceProvenance");
    }

    [Fact]
    public void Preview_refuses_to_match_a_target_recipe_from_another_owner()
    {
        const string recipeJson = """[{"id":"legacy-recipe","title":"Soup","tags":[],"steps":[]}]""";
        var source = RecipeSourceParser.Parse(recipeJson, "{}");
        var owner = Guid.Parse("417c8374-5db4-4021-b106-5b466e2446ed");
        var otherOwner = Guid.Parse("517c8374-5db4-4021-b106-5b466e2446ed");
        var validation = RecipeSourceValidator.Validate(source, owner, []);
        var target = Target(source.Recipes.Single(), Guid.NewGuid(), otherOwner);

        var plan = RecipePreviewPlanner.Create(validation, owner, [target]);

        plan.Recipes.Single().Outcome.Should().Be("proposed-insert");
        plan.BlockingDiagnostics.Should().Contain(diagnostic => diagnostic.Contains("different owner"));
    }

    private static RecipeTargetRecord Target(RecipeSourceRecord source, Guid id, Guid owner) => new(
        id,
        source.LegacyId,
        owner,
        source.Name,
        source.SourceText,
        source.SourceUrl,
        source.ServingsText,
        source.PrepTimeText,
        source.CookTimeText,
        source.DateAddedAtUtc,
        source.IsArchived,
        source.Tags,
        source.Steps.Select(step => new RecipeTargetStep(
            step.Position,
            step.Instructions,
            step.Items.Select(item => new RecipeTargetItem(
                item.Position,
                item.Kind.ToString(),
                item.IngredientId,
                item.Text,
                item.QuantityText,
                item.UnitText,
                item.NotesText)).ToArray())).ToArray(),
        source.SourceDocumentJson);
}
