using FirestoreMigration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using RecipeApp.Api.ComponentTests.Auth;

namespace RecipeApp.Api.ComponentTests.Migration;

public sealed partial class RecipeDurableReportingTests : IAsyncLifetime
{
    private readonly ApiAuthContext _api = new();
    private MigrationFixture _fixture = null!;

    public async Task InitializeAsync()
    {
        await _api.StartAsync();
        _fixture = new(_api);
        await _fixture.InitializeAsync();
        await _fixture.SeedRecipeIngredientAsync();
    }

    [Fact]
    public async Task AC17_UnselectedRecipes_HaveAnOutcomeWithoutInventedMappings()
    {
        var selected = RecipeTestData.RecipeJson(_fixture.IngredientId)
            .Replace("\"title\":", "\"userId\":\"selected\",\"title\":", StringComparison.Ordinal);
        var unselected = RecipeTestData.RecipeJson(_fixture.IngredientId)
            .Replace("synthetic-recipe", "other-recipe", StringComparison.Ordinal)
            .Replace("\"title\":", "\"userId\":\"other\",\"title\":", StringComparison.Ordinal);
        var json = "[" + selected.Trim()[1..^1] + "," + unselected.Trim()[1..^1] + "]";
        var result = await _fixture.RunRecipeAsync(json, true, _fixture.NewRecipeReportPath(),
            selectedLegacyOwnerId: "selected");
        var report = await RecipeTestData.ReadReportAsync(result.ReportPath);
        report.SourceRecipes.Should().Be(2);
        report.SelectedSourceRecipes.Should().Be(1);
        report.Recipes.Should().HaveCount(2);
        report.Recipes.Single(item => item.LegacyId == "other-recipe")
            .Outcome.Should().Be("not-selected");
        report.Recipes.Single(item => item.LegacyId == "other-recipe")
            .ExistingRecipeId.Should().BeNull();
        report.CommittedInserts.Should().Be(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AC17_CancelledOrFailedApproval_PersistsReportBeforeRethrowing(bool cancel)
    {
        await _fixture.RunRecipeAsync(RecipeTestData.RecipeJson(_fixture.IngredientId),
            true, _fixture.NewRecipeReportPath());
        var path = _fixture.NewRecipeReportPath();
        var attempt = () => _fixture.RunRecipeAsync(
            RecipeTestData.RecipeJson(_fixture.IngredientId, title: "Update"),
            true, path, onUpdateApproval: () => cancel
                ? throw new OperationCanceledException("Synthetic cancellation")
                : throw new IOException("Synthetic input failure"));
        (await Record.ExceptionAsync(attempt)).Should().NotBeNull();
        File.Exists(path).Should().BeTrue();
        var report = await RecipeTestData.ReadReportAsync(path);
        report.Status.Should().Be(cancel ? "cancelled" : "rolled-back");
        report.Recipes.Should().ContainSingle().Which.Outcome.Should()
            .Be(cancel ? "cancelled" : "rolled-back");
        report.CommittedInserts.Should().Be(0);
        report.CommittedUpdates.Should().Be(0);
        report.Diagnostics.Should().NotBeEmpty();
        await using var db = _fixture.Database();
        (await db.Recipes.SingleAsync()).Name.Should().Be("Synthetic recipe");
    }

    [Fact]
    public async Task AC17_SaveFailure_RollsBackAndPublishesNoProposedGuid()
    {
        _fixture.RecipeInterceptor = new FailSave();
        var path = _fixture.NewRecipeReportPath();
        var attempt = () => _fixture.RunRecipeAsync(RecipeTestData.RecipeJson(_fixture.IngredientId), true, path);
        await attempt.Should().ThrowAsync<InvalidOperationException>();
        var report = await RecipeTestData.ReadReportAsync(path);
        report.Status.Should().Be("rolled-back");
        report.Recipes.Should().ContainSingle().Which.ExistingRecipeId.Should().BeNull();
        report.Recipes.Single().Outcome.Should().Be("rolled-back");
        await using var db = _fixture.Database();
        (await db.Recipes.CountAsync()).Should().Be(0);
    }

    private sealed class FailSave : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Synthetic save failure.");
    }

    [Fact]
    public async Task AC17_CancelledToken_StillPublishesTheFailureReport()
    {
        await _fixture.RunRecipeAsync(RecipeTestData.RecipeJson(_fixture.IngredientId),
            true, _fixture.NewRecipeReportPath());
        using var cancellation = new CancellationTokenSource();
        var path = _fixture.NewRecipeReportPath();
        var attempt = () => _fixture.RunRecipeAsync(
            RecipeTestData.RecipeJson(_fixture.IngredientId, title: "Update"), true, path,
            onUpdateApproval: () => { cancellation.Cancel(); return Task.CompletedTask; },
            cancellationToken: cancellation.Token);
        await attempt.Should().ThrowAsync<OperationCanceledException>();
        var report = await RecipeTestData.ReadReportAsync(path);
        report.Status.Should().Be("cancelled");
        report.CommittedUpdates.Should().Be(0);
        report.Recipes.Should().ContainSingle().Which.PlannedOutcome.Should().Be("proposed-update");
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task AC17_AllSourceOutcomes_ReconcileWithCounts(bool write, bool invalid)
    {
        var json = RecipeTestData.RecipesJson(_fixture.IngredientId,
            invalid ? Guid.NewGuid() : _fixture.IngredientId);
        var result = await _fixture.RunRecipeAsync(json, write, _fixture.NewRecipeReportPath());
        var report = await RecipeTestData.ReadReportAsync(result.ReportPath);
        report.Recipes.Should().HaveCount(2);
        report.OutcomeCounts!.Values.Sum().Should().Be(report.SourceRecipes);
        report.CommittedInserts.Should().Be(write && !invalid ? 2 : 0);
        if (invalid)
        {
            report.Recipes.Single(item => item.LegacyId == "valid-recipe").Outcome.Should().Be("not-applied");
            report.Recipes.Single(item => item.LegacyId == "invalid-recipe").Outcome.Should().Be("validation-blocked");
            report.Recipes.Single(item => item.LegacyId == "invalid-recipe").Diagnostics.Should().NotBeEmpty();
        }
        else
            report.Recipes.Should().OnlyContain(item => item.Outcome == (write ? "committed-insert" : "proposed-insert"));
        report.Recipes.Should().OnlyContain(item =>
            item.ExistingRecipeId.HasValue == (write && !invalid));
    }

    [Theory]
    [InlineData("yes")]
    [InlineData("no")]
    public async Task AC17_Conversions_DistinguishPlannedAndCommittedWork(string approval)
    {
        var heading = Guid.NewGuid();
        var note = Guid.NewGuid();
        _fixture.DecisionContents = $$$$"""
            {"{{{{heading}}}}":{"name":"Heading label","classification":"Heading"},
             "{{{{note}}}}":{"name":"Note label","classification":"Note"}}
            """;
        var json = $$$$"""
            [{"id":"synthetic-recipe","title":"Synthetic recipe","tags":[],"steps":[{
              "instructions":"Mix.","ingredients":[
                {"id":"{{{{heading}}}}","name":"First heading"},
                {"id":"{{{{heading}}}}","name":"Second heading"},
                {"id":"{{{{note}}}}","name":"Source note"}]
            }]}]
            """;
        var preview = await _fixture.RunRecipeAsync(json, false, _fixture.NewRecipeReportPath());
        var previewReport = await RecipeTestData.ReadReportAsync(preview.ReportPath);
        previewReport.Summary.PlannedHeadingsConverted.Should().Be(2);
        previewReport.Summary.PlannedNotesConverted.Should().Be(1);
        previewReport.Summary.CommittedHeadingsConverted.Should().Be(0);
        previewReport.Summary.CommittedNotesConverted.Should().Be(0);
        var insert = await _fixture.RunRecipeAsync(json, true, _fixture.NewRecipeReportPath());
        var insertReport = await RecipeTestData.ReadReportAsync(insert.ReportPath);
        insertReport.Summary.CommittedHeadingsConverted.Should().Be(2);
        insertReport.Summary.CommittedNotesConverted.Should().Be(1);
        var repeat = await _fixture.RunRecipeAsync(json, true, _fixture.NewRecipeReportPath());
        (await RecipeTestData.ReadReportAsync(repeat.ReportPath)).Summary.CommittedNotesConverted.Should().Be(0);
        var update = await _fixture.RunRecipeAsync(json.Replace("Synthetic recipe", "Update", StringComparison.Ordinal),
            true, _fixture.NewRecipeReportPath(), updateApproval: approval);
        var updateReport = await RecipeTestData.ReadReportAsync(update.ReportPath);
        updateReport.Summary.PlannedNotesConverted.Should().Be(1);
        updateReport.Summary.CommittedHeadingsConverted.Should().Be(approval == "yes" ? 2 : 0);
        updateReport.Summary.CommittedNotesConverted.Should().Be(approval == "yes" ? 1 : 0);
        _fixture.RecipeInterceptor = new FailSave();
        var path = _fixture.NewRecipeReportPath();
        var attempt = () => _fixture.RunRecipeAsync(json.Replace("Synthetic recipe", "Failure", StringComparison.Ordinal),
            true, path);
        await attempt.Should().ThrowAsync<InvalidOperationException>();
        (await RecipeTestData.ReadReportAsync(path)).Summary.CommittedHeadingsConverted.Should().Be(0);
    }

    [Fact]
    public async Task AC17_InterruptedApprovals_PreserveEarlierDecisions()
    {
        var json = RecipeTestData.RecipesJson(_fixture.IngredientId, _fixture.IngredientId);
        await _fixture.RunRecipeAsync(json, true, _fixture.NewRecipeReportPath());
        var reads = 0;
        var path = _fixture.NewRecipeReportPath();
        var attempt = () => _fixture.RunRecipeAsync(json.Replace("Mix.", "Changed instructions.", StringComparison.Ordinal),
            true, path, updateApproval: "no", onUpdateApproval: () =>
                ++reads == 2 ? throw new IOException("Synthetic interrupted input.") : Task.CompletedTask);
        await attempt.Should().ThrowAsync<IOException>();
        var report = await RecipeTestData.ReadReportAsync(path);
        report.Status.Should().Be("rolled-back");
        report.ApprovalDecisions.Should().ContainSingle().Which.Should()
            .Be(new KeyValuePair<string, bool>("valid-recipe", false));
        report.Recipes.Single(item => item.LegacyId == "valid-recipe").Outcome.Should().Be("declined");
        report.Recipes.Single(item => item.LegacyId == "invalid-recipe").Outcome.Should().Be("rolled-back");
        report.CommittedUpdates.Should().Be(0);
    }

    [Fact]
    public async Task AC17_AmbiguousSourceOwner_IsInvalidRatherThanIntentionallyExcluded()
    {
        var first = RecipeTestData.RecipeJson(_fixture.IngredientId)
            .Replace("\"title\":", "\"userId\":\"first\",\"title\":", StringComparison.Ordinal);
        var second = RecipeTestData.RecipeJson(_fixture.IngredientId)
            .Replace("synthetic-recipe", "second-recipe", StringComparison.Ordinal)
            .Replace("\"title\":", "\"userId\":\"second\",\"title\":", StringComparison.Ordinal);
        var missing = RecipeTestData.RecipeJson(_fixture.IngredientId)
            .Replace("synthetic-recipe", "ambiguous-recipe", StringComparison.Ordinal);
        var json = "[" + first.Trim()[1..^1] + "," + second.Trim()[1..^1] + "," + missing.Trim()[1..^1] + "]";
        var result = await _fixture.RunRecipeAsync(json, true, _fixture.NewRecipeReportPath(), selectedLegacyOwnerId: "first");
        var report = await RecipeTestData.ReadReportAsync(result.ReportPath);
        report.Status.Should().Be("validation-blocked");
        report.Recipes.Single(item => item.LegacyId == "second-recipe").Outcome.Should().Be("not-selected");
        var ambiguous = report.Recipes.Single(item => item.LegacyId == "ambiguous-recipe");
        ambiguous.Outcome.Should().Be("validation-blocked");
        ambiguous.Diagnostics.Should().Contain(message => message.Contains("userId", StringComparison.Ordinal));
        report.CommittedInserts.Should().Be(0);
        report.OutcomeCounts!.Values.Sum().Should().Be(3);
    }

    public async Task DisposeAsync()
    {
        _fixture.Dispose();
        await _api.DisposeAsync();
    }
}
