using FirestoreMigration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace RecipeApp.Api.ComponentTests.Migration;

public sealed partial class RecipeDurableReportingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AC17_StagingFailure_ReportsJournalRunAndUncertainUpload(bool cancelled)
    {
        var storage = new MigrationImageStorage { FailAfterWrite = !cancelled };
        if (cancelled)
            storage.AfterCreate = () => throw new OperationCanceledException("Synthetic cancelled upload response.");
        _fixture.ImageStorage = storage;
        _fixture.ImageJournalPath = _fixture.NewRecipeReportPath();
        var path = _fixture.NewRecipeReportPath();
        var attempt = () => _fixture.RunRecipeAsync(RecipeTestData.RecipeJson(_fixture.IngredientId, withImage: true),
            true, path, includeImageManifest: true);
        (await Record.ExceptionAsync(attempt)).Should().NotBeNull();
        var report = await RecipeTestData.ReadReportAsync(path);
        using (var journal = RecipeImageJournal.OpenExisting(_fixture.ImageJournalPath))
        {
            report.RunId.Should().Be(journal.Data.RunId);
            report.DatabaseScopeSha256.Should().Be(journal.Data.DatabaseScope);
        }
        report.Status.Should().Be(cancelled ? "cancelled" : "rolled-back");
        report.ImageWork!.PlannedUploads.Should().Be(1);
        report.ImageWork.Uploaded.Should().Be(0);
        report.ImageWork.UploadUncertain.Should().Be(1);
        report.ImageWork.CommittedAssociations.Should().Be(0);
        report.ImageOutcomes.Should().ContainSingle().Which.Outcome.Should().Be("upload-uncertain");
        report.JournalImages.Should().ContainSingle().Which.ObjectKey.Should().Be(storage.Objects.Keys.Single());
        report.Recipes.Single().ExistingRecipeId.Should().BeNull();
        storage.FailAfterWrite = false;
        storage.AfterCreate = null;
        var retry = await _fixture.RunRecipeAsync(RecipeTestData.RecipeJson(_fixture.IngredientId, withImage: true),
            true, _fixture.NewRecipeReportPath(), includeImageManifest: true);
        var retryReport = await RecipeTestData.ReadReportAsync(retry.ReportPath);
        retryReport.ImageWork!.Uploaded.Should().Be(0);
        retryReport.ImageWork.ReusedStaged.Should().Be(1);
        retryReport.ImageWork.VerifiedStaged.Should().Be(1);
        retryReport.ImageWork.CommittedAssociations.Should().Be(1);
        retryReport.RunId.Should().Be(report.RunId);
        storage.Creates.Should().Be(1);
    }

    [Fact]
    public async Task AC17_VerifiedUpload_ThenRollback_ReportsOnlyPlannedDatabaseWork()
    {
        _fixture.ImageStorage = new MigrationImageStorage();
        _fixture.ImageJournalPath = _fixture.NewRecipeReportPath();
        _fixture.RecipeInterceptor = new FailSave();
        var path = _fixture.NewRecipeReportPath();
        var attempt = () => _fixture.RunRecipeAsync(RecipeTestData.RecipeJson(_fixture.IngredientId, withImage: true),
            true, path, includeImageManifest: true);
        await attempt.Should().ThrowAsync<InvalidOperationException>();
        var report = await RecipeTestData.ReadReportAsync(path);
        report.Status.Should().Be("rolled-back");
        report.ImageWork!.Uploaded.Should().Be(1);
        report.ImageWork.VerifiedStaged.Should().Be(1);
        report.ImageWork.UnreferencedStaged.Should().Be(1);
        report.ImageWork.CommittedAssociations.Should().Be(0);
        report.ImageOutcomes.Should().ContainSingle().Which.Outcome.Should().Be("unreferenced-staged");
        using var journal = RecipeImageJournal.OpenExisting(_fixture.ImageJournalPath);
        journal.Data.CommitAttempted.Should().BeFalse();
        await using var db = _fixture.Database();
        (await db.Recipes.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData("yes")]
    [InlineData("no")]
    public async Task AC17_ImageCounts_DistinguishUploadsRetainedAssociationsAndRemovals(string approval)
    {
        _fixture.ImageStorage = new MigrationImageStorage();
        _fixture.ImageJournalPath = _fixture.NewRecipeReportPath();
        var json = RecipeTestData.RecipeJson(_fixture.IngredientId, withImage: true);
        var insert = await _fixture.RunRecipeAsync(json, true, _fixture.NewRecipeReportPath(), includeImageManifest: true);
        var insertReport = await RecipeTestData.ReadReportAsync(insert.ReportPath);
        insertReport.ImageWork!.Uploaded.Should().Be(1);
        insertReport.ImageWork.VerifiedStaged.Should().Be(1);
        insertReport.ImageWork.CommittedAssociations.Should().Be(1);
        var repeat = await _fixture.RunRecipeAsync(json, true, _fixture.NewRecipeReportPath(), includeImageManifest: true);
        var repeatReport = await RecipeTestData.ReadReportAsync(repeat.ReportPath);
        repeatReport.ImageWork!.Uploaded.Should().Be(0);
        repeatReport.ImageWork.ReusedStaged.Should().Be(0);
        repeatReport.ImageWork.RetainedAssociations.Should().Be(1);
        repeatReport.CommittedUnchanged.Should().Be(1);
        var removal = await _fixture.RunRecipeAsync(RecipeTestData.RecipeJson(_fixture.IngredientId),
            true, _fixture.NewRecipeReportPath(), updateApproval: approval);
        var removalReport = await RecipeTestData.ReadReportAsync(removal.ReportPath);
        removalReport.ImageWork!.PlannedRemovals.Should().Be(1);
        removalReport.ImageWork.CommittedRemovals.Should().Be(approval == "yes" ? 1 : 0);
        removalReport.ImageOutcomes.Should().ContainSingle().Which.PreviousObjectKey.Should()
            .Be(insertReport.ImageOutcomes!.Single().ObjectKey);
    }

    [Fact]
    public async Task AC17_LostCommitResponse_ReportsImageUncertaintyNotRollback()
    {
        _fixture.ImageStorage = new MigrationImageStorage();
        _fixture.ImageJournalPath = _fixture.NewRecipeReportPath();
        _fixture.RecipeInterceptor = new CommitInterceptor(lostResponse: true);
        var path = _fixture.NewRecipeReportPath();
        var attempt = () => _fixture.RunRecipeAsync(RecipeTestData.RecipeJson(_fixture.IngredientId, withImage: true),
            true, path, includeImageManifest: true);
        await attempt.Should().ThrowAsync<IOException>();
        var report = await RecipeTestData.ReadReportAsync(path);
        report.Status.Should().Be("commit-uncertain");
        report.ImageWork!.Uploaded.Should().Be(1);
        report.ImageWork.VerifiedStaged.Should().Be(1);
        report.ImageWork.CommitUncertain.Should().Be(1);
        report.ImageWork.UnreferencedStaged.Should().Be(0);
        report.ImageWork.CommittedAssociations.Should().Be(0);
        await using var db = _fixture.Database();
        (await db.RecipeImages.SingleAsync()).ObjectKey.Should().Be(report.ImageOutcomes!.Single().ObjectKey);
    }

    [Fact]
    public async Task AC18_TransientReportFailure_PersistsKnownCommittedMappingsBeforeRethrowing()
    {
        var storage = new MigrationImageStorage();
        _fixture.ImageStorage = storage;
        _fixture.ImageJournalPath = _fixture.NewRecipeReportPath();
        _fixture.ReportPublisher = new FailingPublisher("committed", failOnce: true);
        var path = _fixture.NewRecipeReportPath();
        var attempt = () => _fixture.RunRecipeAsync(RecipeTestData.RecipeJson(_fixture.IngredientId, withImage: true),
            true, path, includeImageManifest: true);
        await attempt.Should().ThrowAsync<IOException>();
        var report = await RecipeTestData.ReadReportAsync(path);
        report.Status.Should().Be("committed");
        report.CommittedInserts.Should().Be(1);
        report.ImageWork!.CommittedAssociations.Should().Be(1);
        await using var db = _fixture.Database();
        report.Recipes.Single().ExistingRecipeId.Should().Be((await db.Recipes.SingleAsync()).Id);
        using var journal = RecipeImageJournal.OpenExisting(_fixture.ImageJournalPath);
        journal.Data.RunId.Should().Be(report.RunId);
        journal.Data.Committed.Should().BeFalse();
        (await RecipeImageRecovery.RecoverAsync(db, storage, journal, false)).Committed.Should().BeTrue();
        _fixture.Output.ToString().Should().Contain("COMMITTED").And.Contain(report.RunId.ToString());
    }

    [Fact]
    public async Task AC17_ReportsHashImageAccessReferences_EvenWhenValidationFails()
    {
        var reference = "https://example.test/synthetic?token=synthetic-private-value";
        var json = RecipeTestData.RecipeJson(_fixture.IngredientId, withImage: true)
            .Replace("https://example.test/synthetic", reference, StringComparison.Ordinal);
        var result = await _fixture.RunRecipeAsync(json, false, _fixture.NewRecipeReportPath(), includeImageManifest: true);
        var report = await RecipeTestData.ReadReportAsync(result.ReportPath);
        report.Status.Should().Be("validation-blocked");
        var expectedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(reference)));
        report.ImageOutcomes!.Single().ImageReferenceSha256.Should().Be(expectedHash);
        (await File.ReadAllTextAsync(result.ReportPath)).Should().NotContain("synthetic-private-value");
        _fixture.Output.ToString().Should().NotContain("synthetic-private-value");
    }
}
