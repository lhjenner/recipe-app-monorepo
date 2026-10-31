using FirestoreMigration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Reqnroll;

namespace RecipeApp.Api.ComponentTests.Migration;

[Binding]
public sealed class RecipeImportSteps(MigrationFixture fixture)
{
    [When("I preview a synthetic Recipe that references the existing Ingredient")]
    public async Task PreviewRecipeWithIngredientAsync()
    {
        await fixture.SeedRecipeIngredientAsync();
        fixture.Result = null;
        await fixture.RunRecipePreviewAsync(RecipeJson(fixture.IngredientId));
    }

    [When("I preview a synthetic Recipe that references a missing Ingredient")]
    public async Task PreviewRecipeWithMissingIngredientAsync() =>
        await fixture.RunRecipePreviewAsync(RecipeJson(Guid.NewGuid()));

    [When("I preview a synthetic Recipe with a valid image manifest")]
    public async Task PreviewRecipeWithValidImageAsync()
    {
        await fixture.SeedRecipeIngredientAsync();
        await fixture.RunRecipePreviewAsync(RecipeJson(fixture.IngredientId, withImage: true),
            includeImageManifest: true);
    }

    [When("I preview a synthetic Recipe with a corrupt selected image")]
    public async Task PreviewRecipeWithCorruptImageAsync() =>
        await fixture.RunRecipePreviewAsync(RecipeJson(fixture.IngredientId, withImage: true),
            includeImageManifest: true, corruptImage: true);

    [When("I write and repeat a synthetic Recipe import")]
    public async Task WriteAndRepeatRecipeImportAsync()
    {
        await fixture.SeedRecipeIngredientAsync();
        var recipeJson = RecipeJson(fixture.IngredientId);
        fixture.FirstRecipeResult = await fixture.RunRecipeAsync(
            recipeJson, true, fixture.NewRecipeReportPath());
        fixture.FirstRecipeIdentity = await fixture.ReadRecipeIdentityAsync();
        fixture.RecipeResult = await fixture.RunRecipeAsync(
            recipeJson, true, fixture.NewRecipeReportPath());
    }

    [When("I attempt a Recipe write with a missing Ingredient")]
    public async Task WriteRecipeWithMissingIngredientAsync()
    {
        await fixture.SeedRecipeIngredientAsync();
        fixture.RecipeAttemptReportPath = fixture.NewRecipeReportPath();
        await fixture.RunRecipeAsync(
            RecipesJson(fixture.IngredientId, Guid.NewGuid()),
            true, fixture.RecipeAttemptReportPath);
    }

    [When("I confirm a Recipe preview with a different destination owner")]
    public async Task ConfirmWrongDestinationOwnerAsync()
    {
        fixture.RecipeAttemptReportPath = fixture.NewRecipeReportPath();
        fixture.Failure = await Record.ExceptionAsync(() => fixture.RunRecipeAsync(
            RecipeJson(Guid.NewGuid()), false, fixture.RecipeAttemptReportPath,
            ownerConfirmation: Guid.NewGuid().ToString()));
    }

    [Then("the preview is rejected before creating a report")]
    public async Task WrongOwnerConfirmationIsRejectedAsync()
    {
        fixture.Failure.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Contain("confirmation did not match");
        fixture.Output.ToString().Should().Contain($"Destination owner: {fixture.Owner}")
            .And.Contain("Source-owner selection:");
        File.Exists(fixture.RecipeAttemptReportPath).Should().BeFalse();
        await using var db = fixture.Database();
        (await db.Recipes.CountAsync()).Should().Be(0);
    }

    [Then("the Recipe write is blocked without committing any Recipes")]
    public async Task InvalidRecipeWriteIsBlockedAsync()
    {
        var report = await ReadRecipeReportAsync(fixture.RecipeAttemptReportPath);
        report.Status.Should().Be("validation-blocked");
        report.CommittedInserts.Should().Be(0);
        report.CommittedUpdates.Should().Be(0);
        report.Diagnostics.Should().Contain(diagnostic => diagnostic.Contains("does not exist"));
        await using var db = fixture.Database();
        (await db.Recipes.CountAsync()).Should().Be(0);
        (await db.Ingredients.CountAsync()).Should().Be(1);
    }

    [Then("the Recipe mapping and child IDs remain stable with committed outcomes")]
    public async Task RecipeMappingAndChildrenRemainStableAsync()
    {
        var firstIdentity = fixture.FirstRecipeIdentity
            ?? throw new InvalidOperationException("The first Recipe import identity was not captured.");
        (await fixture.ReadRecipeIdentityAsync()).Should().BeEquivalentTo(firstIdentity);
        firstIdentity.RecipeId.ToString("D")[14].Should().Be('7');
        firstIdentity.LegacyId.Should().Be("synthetic-recipe");
        firstIdentity.OwnerUserId.Should().Be(fixture.Owner);
        firstIdentity.IngredientIds.Should().Equal(fixture.IngredientId);
        firstIdentity.SourceDocumentJson.Should().Contain("Synthetic recipe");
        firstIdentity.DocumentSha256.Should().NotBeNullOrWhiteSpace();
        firstIdentity.SourceFileSha256.Should().NotBeNullOrWhiteSpace();

        var firstReport = await ReadRecipeReportAsync(fixture.FirstRecipeResult!.ReportPath);
        firstReport.Status.Should().Be("committed");
        firstReport.CommittedInserts.Should().Be(1);
        firstIdentity.LastAppliedRunId.Should().Be(firstReport.RunId);
        firstReport.Recipes.Should().ContainSingle()
            .Which.ExistingRecipeId.Should().Be(firstIdentity.RecipeId);

        var repeatedReport = await ReadRecipeReportAsync(fixture.RecipeResult!.ReportPath);
        repeatedReport.Status.Should().Be("committed");
        repeatedReport.CommittedInserts.Should().Be(0);
        repeatedReport.CommittedUpdates.Should().Be(0);
        repeatedReport.Unchanged.Should().Be(1);
        repeatedReport.Recipes.Should().ContainSingle()
            .Which.ExistingRecipeId.Should().Be(firstIdentity.RecipeId);

        await using var db = fixture.Database();
        (await db.Recipes.CountAsync()).Should().Be(1);
        (await db.RecipeSteps.CountAsync()).Should().Be(1);
        (await db.RecipeStepItems.CountAsync()).Should().Be(1);
        (await db.RecipeTags.CountAsync()).Should().Be(1);
    }

    [Then("the Recipe preview reports a proposed insert without committing it")]
    public async Task RecipePreviewIsReadOnlyAsync()
    {
        var report = await ReadRecipeReportAsync();
        report.Status.Should().Be("preview-valid");
        report.SchemaVersion.Should().Be(3);
        report.ProposedInserts.Should().Be(1);
        report.CommittedInserts.Should().Be(0);
        report.Recipes.Should().ContainSingle().Which.ExistingRecipeId.Should().BeNull();
        report.SelectedSourceRecipes.Should().Be(1);
        var source = report.SourceDispositions.Should().ContainSingle().Which;
        source.Selected.Should().BeTrue();
        source.Fields.Should().Contain(field =>
            field.JsonPath == "recipes[0].title" && field.Disposition == "Recipe.Name");
        source.Steps.Should().ContainSingle().Which.Items.Should().ContainSingle()
            .Which.Disposition.Should().Be("ingredient");
        await using var db = fixture.Database();
        (await db.Recipes.CountAsync()).Should().Be(0);
        (await db.Ingredients.CountAsync()).Should().Be(1);
    }

    [Then("the Recipe preview is blocked and still writes its report")]
    public async Task RecipePreviewBlockedAsync()
    {
        var report = await ReadRecipeReportAsync();
        report.Status.Should().Be("validation-blocked");
        report.Diagnostics.Should().Contain(diagnostic => diagnostic.Contains("does not exist"));
        report.Summary.MissingIngredientReferences.Should().Be(1);
        report.Summary.PlannedHeadingsConverted.Should().Be(0);
        report.Summary.PlannedNotesConverted.Should().Be(0);
        report.CommittedInserts.Should().Be(0);
        await using var db = fixture.Database();
        (await db.Recipes.CountAsync()).Should().Be(0);
    }

    [Then("the Recipe preview reports validated image metadata without committing it")]
    public async Task RecipeImageValidatedAsync()
    {
        var report = await ReadRecipeReportAsync();
        report.Status.Should().Be("preview-valid");
        report.ValidatedImages.Should().Be(1);
        report.Images.Should().ContainSingle().Which.Should().Match<RecipeImageValidationResult>(image =>
            image.Status == "validated" && image.ContentType == "image/png" &&
            image.Width == 1 && image.Height == 1);
        await using var db = fixture.Database();
        (await db.RecipeImages.CountAsync()).Should().Be(0);
    }

    [Then("the Recipe preview reports a blocking image diagnostic")]
    public async Task RecipeImageBlockedAsync()
    {
        var report = await ReadRecipeReportAsync();
        report.Status.Should().Be("validation-blocked");
        report.Images.Should().ContainSingle().Which.Status.Should().Be("invalid");
        report.Diagnostics.Should().Contain(diagnostic => diagnostic.Contains("decoder"));
        await using var db = fixture.Database();
        (await db.Recipes.CountAsync()).Should().Be(0);
    }

    private static string RecipeJson(Guid ingredientId, bool withImage = false) => $$$$"""
        [{
          "id":"synthetic-recipe",
          "title":"Synthetic recipe",
          {{{{(withImage ? "\"imageUrl\":\"https://example.test/synthetic\"," : "")}}}}
          "tags":["Dinner"],
          "steps":[{
            "instructions":"Mix.",
            "ingredients":[{"id":"{{{{ingredientId}}}}","name":"Synthetic flour","quantity":"1 cup"}]
          }]
        }]
        """;

    private static string RecipesJson(Guid availableIngredientId, Guid missingIngredientId) => $$$$"""
        [
          {"id":"valid-recipe","title":"Valid recipe","tags":[],"steps":[{
            "instructions":"Mix.","ingredients":[{
              "id":"{{{{availableIngredientId}}}}","name":"Synthetic flour"
            }]
          }]},
          {"id":"invalid-recipe","title":"Invalid recipe","tags":[],"steps":[{
            "instructions":"Mix.","ingredients":[{
              "id":"{{{{missingIngredientId}}}}","name":"Missing Ingredient"
            }]
          }]}
        ]
        """;

    private async Task<RecipeMigrationReport> ReadRecipeReportAsync(string? reportPath = null)
    {
        await using var stream = File.OpenRead(reportPath ?? fixture.RecipeReportFile);
        return await System.Text.Json.JsonSerializer.DeserializeAsync<RecipeMigrationReport>(stream)
            ?? throw new InvalidOperationException("Recipe preview report could not be read.");
    }
}
