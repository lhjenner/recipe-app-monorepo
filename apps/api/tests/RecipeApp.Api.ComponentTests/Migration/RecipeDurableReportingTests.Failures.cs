using FirestoreMigration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data;
using System.Data.Common;

namespace RecipeApp.Api.ComponentTests.Migration;

public sealed partial class RecipeDurableReportingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AC18_CommitFailure_NeverInventsACommittedGuidOrClaimsRollback(bool lostResponse)
    {
        _fixture.RecipeInterceptor = new CommitInterceptor(lostResponse: lostResponse, failCommit: true);
        var path = _fixture.NewRecipeReportPath();
        var attempt = () => _fixture.RunRecipeAsync(RecipeTestData.RecipeJson(_fixture.IngredientId), true, path);
        await attempt.Should().ThrowAsync<IOException>();
        var report = await RecipeTestData.ReadReportAsync(path);
        report.Status.Should().Be("commit-uncertain");
        report.CommittedInserts.Should().Be(0);
        report.Recipes.Should().ContainSingle().Which.ExistingRecipeId.Should().BeNull();
        report.Recipes.Single().Outcome.Should().Be("commit-uncertain");
        report.Diagnostics.Should().Contain(message => message.Contains("uncertain", StringComparison.Ordinal));
        await using var db = _fixture.Database();
        (await db.Recipes.CountAsync()).Should().Be(lostResponse ? 1 : 0);
        if (lostResponse)
            (await db.RecipeImportProvenance.SingleAsync()).LastAppliedRunId.Should().Be(report.RunId);
    }

    [Fact]
    public async Task AC18_PostCommitReportFailure_PreservesCheckpointAndCommittedData()
    {
        var path = _fixture.NewRecipeReportPath();
        _fixture.ReportPublisher = new FailingPublisher("committed");
        var exitCode = await MigrationCommand.ExecuteAsync(async () =>
        {
            var result = await _fixture.RunRecipeAsync(RecipeTestData.RecipeJson(_fixture.IngredientId), true, path);
            return result.Errors == 0 ? 0 : 1;
        }, _fixture.Output);
        exitCode.Should().Be(1);
        var checkpoint = await RecipeTestData.ReadReportAsync(path);
        checkpoint.Status.Should().Be("commit-uncertain");
        checkpoint.CommittedInserts.Should().Be(0);
        checkpoint.Recipes.Single().ExistingRecipeId.Should().BeNull();
        await using var db = _fixture.Database();
        (await db.Recipes.CountAsync()).Should().Be(1);
        (await db.RecipeImportProvenance.SingleAsync()).LastAppliedRunId.Should().Be(checkpoint.RunId);
        _fixture.Output.ToString().Should().Contain("COMMITTED").And.Contain(checkpoint.RunId.ToString())
            .And.Contain("report persistence failed").And.NotContain("status=rolled-back");
        Directory.GetFiles(Path.GetDirectoryName(path)!, "*.tmp").Should().BeEmpty();
    }

    [Fact]
    public async Task AC17_LockedReportDestination_StopsBeforeDatabaseWrites()
    {
        var path = _fixture.NewRecipeReportPath();
        using var reservation = new FileStream(path + ".lock", FileMode.CreateNew,
            FileAccess.ReadWrite, FileShare.None);
        var attempt = () => _fixture.RunRecipeAsync(RecipeTestData.RecipeJson(_fixture.IngredientId), true, path);
        await attempt.Should().ThrowAsync<IOException>();
        File.Exists(path).Should().BeFalse();
        await using var db = _fixture.Database();
        (await db.Recipes.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AC17_PrecommitCheckpointFailure_RollsBackTheWriteTransaction()
    {
        var path = _fixture.NewRecipeReportPath();
        _fixture.ReportPublisher = new FailingPublisher("commit-uncertain");
        var attempt = () => _fixture.RunRecipeAsync(RecipeTestData.RecipeJson(_fixture.IngredientId), true, path);
        await attempt.Should().ThrowAsync<IOException>();
        var report = await RecipeTestData.ReadReportAsync(path);
        report.Status.Should().Be("rolled-back");
        report.CommittedInserts.Should().Be(0);
        report.Recipes.Single().ExistingRecipeId.Should().BeNull();
        await using var db = _fixture.Database();
        (await db.Recipes.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AC17_ReportCannotOverwriteTheImageJournalLock()
    {
        var journal = _fixture.NewRecipeReportPath();
        _fixture.ImageJournalPath = journal;
        var attempt = () => _fixture.RunRecipeAsync(RecipeTestData.RecipeJson(_fixture.IngredientId), true, journal + ".lock");
        await attempt.Should().ThrowAsync<FormatException>();
        File.Exists(journal + ".lock").Should().BeFalse();
        await using var db = _fixture.Database();
        (await db.Recipes.CountAsync()).Should().Be(0);
    }

    private sealed class FailingPublisher(string failingStatus, bool failOnce = false) : IRecipeReportPublisher
    {
        private bool _failed;
        public Task PublishAsync(string path, RecipeMigrationReport report, bool replace, CancellationToken cancellationToken)
        {
            if (report.Status == failingStatus && (!failOnce || !_failed))
            {
                _failed = true;
                throw new IOException("Synthetic report publication failure.");
            }
            return RecipeMigrationReportWriter.WriteAsync(path, report, cancellationToken, replace);
        }
    }

    private sealed class CommitInterceptor(bool lostResponse = false, bool failCommit = false) : DbTransactionInterceptor
    {
        private bool _write;
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
            TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            _write = transaction.IsolationLevel == IsolationLevel.Serializable;
            if (_write && failCommit && !lostResponse)
                throw new IOException("Synthetic commit request failure.");
            return ValueTask.FromResult(result);
        }

        public override Task TransactionCommittedAsync(DbTransaction transaction,
            TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            if (_write)
            {
                if (lostResponse) throw new IOException("Synthetic lost commit response.");
            }
            return Task.CompletedTask;
        }
    }
}
