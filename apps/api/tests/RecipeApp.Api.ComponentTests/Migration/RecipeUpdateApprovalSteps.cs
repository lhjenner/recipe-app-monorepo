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

    [When("the (.*) changes during Recipe update approval")]
    public async Task ChangeDuringRecipeApprovalAsync(string resource)
    {
        fixture.StaleResource = resource;
        fixture.Failure = await Record.ExceptionAsync(() => fixture.RunRecipeAsync(
            RecipeTestData.RecipeJson(fixture.IngredientId, title: "Approved update",
                tag: "Lunch", instruction: "Bake gently."),
            true, fixture.NewRecipeReportPath(),
            onUpdateApproval: async () =>
            {
                if (resource == "source")
                {
                    File.WriteAllText(fixture.RecipeFile,
                        RecipeTestData.RecipeJson(fixture.IngredientId, title: "Changed source"));
                    return;
                }

                await using var db = fixture.Database();
                switch (resource)
                {
                    case "Recipe row":
                        var recipe = await db.Recipes.SingleAsync();
                        recipe.Name = "Concurrent target edit";
                        recipe.UpdatedAtUtc = DateTime.UtcNow;
                        break;
                    case "Recipe child":
                        var step = await db.RecipeSteps.SingleAsync();
                        step.Instructions = "Concurrent target step";
                        break;
                    case "Ingredient":
                        var ingredient = await db.Ingredients.SingleAsync();
                        ingredient.Name = "Concurrent ingredient edit";
                        ingredient.NormalizedName = "concurrent ingredient edit";
                        ingredient.UpdatedAtUtc = DateTime.UtcNow;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(resource), resource,
                            "Unknown stale Recipe test resource.");
                }
                await db.SaveChangesAsync();
            }));
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

    [Then("the stale Recipe update is rejected without overwriting the change")]
    public async Task StaleRecipeUpdateIsRejectedAsync()
    {
        fixture.Failure.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Contain("changed after preview");
        await using var db = fixture.Database();
        var recipe = await db.Recipes.Include(row => row.Tags)
            .Include(row => row.Steps).SingleAsync();
        var expectedName = fixture.StaleResource == "Recipe row"
            ? "Concurrent target edit"
            : "Synthetic recipe";
        recipe.Name.Should().Be(expectedName);
        recipe.Tags.Single().Text.Should().Be("Dinner");
        recipe.Steps.Single().Instructions.Should().Be(
            fixture.StaleResource == "Recipe child" ? "Concurrent target step" : "Mix.");
        if (fixture.StaleResource == "Ingredient")
        {
            var ingredient = await db.Ingredients.SingleAsync();
            ingredient.Name.Should().Be("Concurrent ingredient edit");
        }
    }
}
