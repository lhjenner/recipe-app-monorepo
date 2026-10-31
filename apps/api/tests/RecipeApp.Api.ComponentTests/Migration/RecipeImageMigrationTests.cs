using FirestoreMigration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using RecipeApp.Api.ComponentTests.Auth;
using SkiaSharp;
using System.Data;
using System.Data.Common;

namespace RecipeApp.Api.ComponentTests.Migration;

public sealed partial class RecipeImageMigrationTests : IAsyncLifetime
{
    private readonly ApiAuthContext _api = new();
    private MigrationFixture _fixture = null!;
    private readonly MigrationImageStorage _storage = new();

    public async Task InitializeAsync()
    {
        await _api.StartAsync();
        _fixture = new MigrationFixture(_api);
        await _fixture.InitializeAsync();
        await _fixture.SeedRecipeIngredientAsync();
        _fixture.ImageStorage = _storage;
        _fixture.ImageJournalPath = _fixture.NewRecipeReportPath();
    }

    [Fact]
    public async Task AC34_ImportAndRepeat_PreserveImageAssociationAndChildIds()
    {
        var json = RecipeTestData.RecipeJson(_fixture.IngredientId, withImage: true);
        var first = await _fixture.RunRecipeAsync(json, true, _fixture.NewRecipeReportPath(),
            includeImageManifest: true);
        first.Status.Should().Be("committed");
        var identity = await _fixture.ReadRecipeIdentityAsync();
        await using var db = _fixture.Database();
        var image = await db.RecipeImages.SingleAsync();
        image.RecipeId.Should().Be(identity.RecipeId);
        image.ByteSize.Should().Be(TestFixtures.SyntheticImage.Png.Length);
        _storage.Objects[image.ObjectKey].Should().Equal(TestFixtures.SyntheticImage.Png);

        var repeat = await _fixture.RunRecipeAsync(json, true, _fixture.NewRecipeReportPath(),
            includeImageManifest: true);
        repeat.Status.Should().Be("committed");
        (await _fixture.ReadRecipeIdentityAsync()).Should().BeEquivalentTo(identity);
        (await db.RecipeImages.AsNoTracking().SingleAsync()).ObjectKey.Should().Be(image.ObjectKey);
        var report = await RecipeTestData.ReadReportAsync(repeat.ReportPath);
        report.CommittedUpdates.Should().Be(0);
        _storage.Creates.Should().Be(1);
        report.JournalImages.Should().ContainSingle().Which.State.Should().Be("referenced");
    }

    [Theory]
    [InlineData("yes", true)]
    [InlineData("no", false)]
    [InlineData(null, false)]
    public async Task AC39_SameUrlChangedChecksum_RequiresApprovalAndRetainsOldBytes(string? answer, bool applied)
    {
        await ImportAsync();
        var identity = await _fixture.ReadRecipeIdentityAsync();
        var original = await ImageKeyAsync();
        ChangeImage();
        var result = await ImportAsync(answer);
        var current = await ImageKeyAsync();
        current.Should().Be(applied ? _storage.Objects.Keys.Single(key => key != original) : original);
        _storage.Objects[original].Should().Equal(TestFixtures.SyntheticImage.Png);
        _storage.Creates.Should().Be(applied ? 2 : 1);
        (await _fixture.ReadRecipeIdentityAsync()).RecipeId.Should().Be(identity.RecipeId);
        var report = await RecipeTestData.ReadReportAsync(result.ReportPath);
        report.Recipes.Single().Differences.Should().Contain("Image");
        report.CommittedUpdates.Should().Be(applied ? 1 : 0);
        _fixture.Output.ToString().Should().Contain("Apply this entire Recipe update?");
    }

    [Theory]
    [InlineData("yes", true)]
    [InlineData("no", false)]
    public async Task AC39_ImageRemoval_IsAtomicAndNeverDeletesOldObject(string answer, bool applied)
    {
        await ImportAsync();
        var original = await ImageKeyAsync();
        await _fixture.RunRecipeAsync(RecipeTestData.RecipeJson(_fixture.IngredientId),
            true, _fixture.NewRecipeReportPath(), updateApproval: answer);
        await using var db = _fixture.Database();
        (await db.RecipeImages.CountAsync()).Should().Be(applied ? 0 : 1);
        _storage.Objects.Should().ContainKey(original);
        _storage.Deletes.Should().Be(0);
    }

    [Fact]
    public async Task AC36_UncertainUpload_ReconcilesAndRetriesWithoutDuplicateUpload()
    {
        _storage.FailAfterWrite = true;
        var attempt = () => ImportAsync();
        await attempt.Should().ThrowAsync<IOException>();
        await using var db = _fixture.Database();
        (await db.Recipes.CountAsync()).Should().Be(0);
        using (var journal = RecipeImageJournal.OpenExisting(_fixture.ImageJournalPath!))
        {
            journal.Data.Images.Should().ContainSingle().Which.State.Should().Be("planned");
            var recovery = await RecipeImageRecovery.RecoverAsync(db, _storage, journal, false);
            recovery.Unreferenced.Should().Be(1);
            recovery.Committed.Should().BeFalse();
        }
        _storage.FailAfterWrite = false;
        (await ImportAsync()).Status.Should().Be("committed");
        _storage.Creates.Should().Be(1);
        using var recovered = RecipeImageJournal.OpenExisting(_fixture.ImageJournalPath!);
        var committed = await RecipeImageRecovery.RecoverAsync(db, _storage, recovered, true);
        committed.Referenced.Should().Be(1);
        committed.Committed.Should().BeTrue();
        _storage.Deletes.Should().Be(0);
    }

    [Theory]
    [InlineData("commit")]
    [InlineData("conflict")]
    [InlineData("cancel")]
    public async Task AC36_PrecommitFailure_KeepsCurrentImageAndJournalsLeftover(string failure)
    {
        await ImportAsync();
        var original = await ImageKeyAsync();
        ChangeImage();
        if (failure == "commit") _fixture.RecipeInterceptor = new FailWriteCommit();
        if (failure == "cancel")
            _storage.AfterCreate = () => throw new OperationCanceledException("Synthetic staging cancellation.");
        if (failure == "conflict")
            _storage.AfterCreate = async () =>
            {
                await using var concurrent = _fixture.Database();
                (await concurrent.RecipeTags.SingleAsync()).Text = "Concurrent tag";
                await concurrent.SaveChangesAsync();
            };
        var exception = await Record.ExceptionAsync(() => ImportAsync());
        exception.Should().NotBeNull();
        (await ImageKeyAsync()).Should().Be(original);
        _storage.Objects.Should().HaveCount(2);
        _storage.Objects[original].Should().Equal(TestFixtures.SyntheticImage.Png);
        _fixture.RecipeInterceptor = null;
        using var journal = RecipeImageJournal.OpenExisting(_fixture.ImageJournalPath!);
        await using var db = _fixture.Database();
        var recovery = await RecipeImageRecovery.RecoverAsync(db, _storage, journal, false);
        recovery.Unreferenced.Should().Be(1);
        recovery.Committed.Should().BeFalse();
        var cleanup = await RecipeImageRecovery.RecoverAsync(db, _storage, journal, true);
        cleanup.Deleted.Should().Be(1);
        _storage.Objects.Keys.Should().Equal(original);
    }

    [Fact]
    public async Task AC35_CorruptReadback_BlocksEveryRecipeAndRefusesUnsafeCleanup()
    {
        _storage.CorruptWrites = true;
        var attempt = () => ImportAsync();
        await attempt.Should().ThrowAsync<InvalidDataException>();
        await using var db = _fixture.Database();
        (await db.Recipes.CountAsync()).Should().Be(0);
        using var journal = RecipeImageJournal.OpenExisting(_fixture.ImageJournalPath!);
        var cleanup = () => RecipeImageRecovery.RecoverAsync(db, _storage, journal, true);
        await cleanup.Should().ThrowAsync<InvalidDataException>();
        _storage.Deletes.Should().Be(0);
    }

    [Fact]
    public async Task AC13_PreviewValidatesWithoutCreatingJournalOrObjects()
    {
        var result = await _fixture.RunRecipeAsync(RecipeTestData.RecipeJson(_fixture.IngredientId, withImage: true),
            false, _fixture.NewRecipeReportPath(), includeImageManifest: true);
        result.Status.Should().Be("preview-valid");
        _storage.Creates.Should().Be(0);
        File.Exists(_fixture.ImageJournalPath).Should().BeFalse();
    }

    [Fact]
    public async Task AC36_UncertainCommitResponse_IsReconciledWithoutDeletingCommittedObject()
    {
        _fixture.RecipeInterceptor = new FailAfterCommit();
        var attempt = () => ImportAsync();
        await attempt.Should().ThrowAsync<IOException>();
        _fixture.RecipeInterceptor = null;
        using var journal = RecipeImageJournal.OpenExisting(_fixture.ImageJournalPath!);
        journal.Data.CommitAttempted.Should().BeTrue();
        journal.Data.Committed.Should().BeFalse();
        await using var db = _fixture.Database();
        var recovery = await RecipeImageRecovery.RecoverAsync(db, _storage, journal, true);
        recovery.Committed.Should().BeTrue();
        recovery.Referenced.Should().Be(1);
        _storage.Deletes.Should().Be(0);
    }

    private Task<RecipeMigrationRunResult> ImportAsync(string? answer = "yes") =>
        _fixture.RunRecipeAsync(RecipeTestData.RecipeJson(_fixture.IngredientId, withImage: true),
            true, _fixture.NewRecipeReportPath(), includeImageManifest: true, updateApproval: answer);

    private async Task<string> ImageKeyAsync()
    {
        await using var db = _fixture.Database();
        return (await db.RecipeImages.SingleAsync()).ObjectKey;
    }

    private void ChangeImage()
    {
        using var bitmap = new SKBitmap(1, 1);
        bitmap.Erase(SKColors.Red);
        using var image = SKImage.FromBitmap(bitmap);
        using var bytes = image.Encode(SKEncodedImageFormat.Png, 100);
        _fixture.ImageBytes = bytes.ToArray();
        _fixture.ImageJournalPath = _fixture.NewRecipeReportPath();
    }

    private sealed class FailWriteCommit : DbTransactionInterceptor
    {
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
            TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            if (transaction.IsolationLevel == IsolationLevel.Serializable)
                throw new InvalidOperationException("Synthetic failure after writes, before commit.");
            return ValueTask.FromResult(result);
        }

    }

    private sealed class FailAfterCommit : DbTransactionInterceptor
    {
        private bool _write;
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
            TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            _write = transaction.IsolationLevel == IsolationLevel.Serializable;
            return ValueTask.FromResult(result);
        }

        public override Task TransactionCommittedAsync(DbTransaction transaction,
            TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            if (_write)
                throw new IOException("Synthetic lost response after successful commit.");
            return Task.CompletedTask;
        }
    }

    public async Task DisposeAsync()
    {
        _fixture.Dispose();
        await _api.DisposeAsync();
    }
}
