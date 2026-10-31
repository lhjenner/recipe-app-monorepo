using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Reqnroll;

namespace RecipeApp.Api.ComponentTests.Migration;

[Binding]
public sealed class RecipeUpdateApprovalSteps(MigrationFixture fixture)
{
    [Given("a synthetic Recipe was imported")]
    public async Task ImportRecipeForUpdateAsync()
    {
        await fixture.SeedRecipeIngredientAsync();
        fixture.FirstRecipeResult = await fixture.RunRecipeAsync(
            RecipeTestData.RecipeJson(fixture.IngredientId), true, fixture.NewRecipeReportPath());
        fixture.FirstRecipeIdentity = await fixture.ReadRecipeIdentityAsync();
    }

    [When("I review a complete Recipe update and respond (.*)")]
    public async Task ReviewRecipeUpdateAsync(string response)
    {
        fixture.RecipeResult = await fixture.RunRecipeAsync(
            RecipeTestData.RecipeJson(fixture.IngredientId, title: "Updated synthetic recipe",
                tag: "Lunch", instruction: "Bake gently."),
            true, fixture.NewRecipeReportPath(), updateApproval: response == "EOF" ? null : response);
    }

    [Then("the Recipe update is (.*) as a whole")]
    public async Task RecipeUpdateOutcomeAsync(string outcome)
    {
        var before = fixture.FirstRecipeIdentity
            ?? throw new InvalidOperationException("The original Recipe identity was not captured.");
        var after = await fixture.ReadRecipeIdentityAsync();
        after.RecipeId.Should().Be(before.RecipeId);
        after.LegacyId.Should().Be(before.LegacyId);
        after.OwnerUserId.Should().Be(before.OwnerUserId);

        var report = await RecipeTestData.ReadReportAsync(fixture.RecipeResult!.ReportPath);
        report.Status.Should().Be("committed");
        report.Recipes.Should().ContainSingle().Which.Outcome.Should()
            .Be(outcome == "applied" ? "committed-update" : "declined");
        report.CommittedUpdates.Should().Be(outcome == "applied" ? 1 : 0);

        await using var db = fixture.Database();
        var recipe = await db.Recipes.Include(row => row.Tags)
            .Include(row => row.Steps).ThenInclude(step => step.Items)
            .Include(row => row.ImportProvenance).SingleAsync();
        recipe.Name.Should().Be(outcome == "applied" ? "Updated synthetic recipe" : "Synthetic recipe");
        recipe.Steps.Single().Instructions.Should().Be(outcome == "applied" ? "Bake gently." : "Mix.");
        recipe.Tags.Single().Text.Should().Be(outcome == "applied" ? "Lunch" : "Dinner");
        recipe.Steps.Single().Items.Single().IngredientId.Should().Be(fixture.IngredientId);
        if (outcome == "declined")
        {
            after.ChildIds.Should().Equal(before.ChildIds);
            recipe.ImportProvenance!.LastAppliedRunId.Should().Be(before.LastAppliedRunId);
        }
        else
        {
            recipe.ImportProvenance!.LastAppliedRunId.Should().Be(report.RunId);
        }
    }
}
