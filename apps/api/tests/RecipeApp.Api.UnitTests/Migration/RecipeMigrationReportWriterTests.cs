using System.Text.Json;
using FirestoreMigration;
using FluentAssertions;

namespace RecipeApp.Api.UnitTests.Migration;

public sealed class RecipeMigrationReportWriterTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"recipe-report-test-{Guid.NewGuid():N}.json");

    [Fact]
    public async Task ExistingReport_IsNeverOverwrittenByANewRun()
    {
        var original = Report();
        await RecipeMigrationReportWriter.WriteAsync(_path, original, CancellationToken.None);
        var bytes = await File.ReadAllBytesAsync(_path);
        var attempt = () => RecipeMigrationReportWriter.WriteAsync(_path, Report(), CancellationToken.None);
        await attempt.Should().ThrowAsync<IOException>();
        (await File.ReadAllBytesAsync(_path)).Should().Equal(bytes);
        AssertNoTemporaryFiles();
    }

    [Fact]
    public async Task CancelledPublication_PreservesThePreviousCompleteCheckpoint()
    {
        var original = Report();
        await RecipeMigrationReportWriter.WriteAsync(_path, original, CancellationToken.None);
        var bytes = await File.ReadAllBytesAsync(_path);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var attempt = () => RecipeMigrationReportWriter.WriteAsync(_path,
            original with { Status = "cancelled" }, cancellation.Token, replace: true);
        await attempt.Should().ThrowAsync<OperationCanceledException>();
        (await File.ReadAllBytesAsync(_path)).Should().Equal(bytes);
        AssertNoTemporaryFiles();
    }

    [Fact]
    public async Task CheckpointReplacement_PublishesTheWholeReport()
    {
        var original = Report();
        await RecipeMigrationReportWriter.WriteAsync(_path, original, CancellationToken.None);
        var final = original with { Status = "committed", CompletedAtUtc = DateTime.UtcNow };
        await RecipeMigrationReportWriter.WriteAsync(_path, final, CancellationToken.None, replace: true);
        var persisted = JsonSerializer.Deserialize<RecipeMigrationReport>(await File.ReadAllBytesAsync(_path));
        persisted.Should().BeEquivalentTo(final);
        AssertNoTemporaryFiles();
    }

    [Fact]
    public async Task ConcurrentNewReports_ExactlyOnePublishesACompleteDocument()
    {
        var first = Report();
        var second = Report();
        var errors = await Task.WhenAll(
            Record.ExceptionAsync(() => RecipeMigrationReportWriter.WriteAsync(_path, first, CancellationToken.None)),
            Record.ExceptionAsync(() => RecipeMigrationReportWriter.WriteAsync(_path, second, CancellationToken.None)));
        errors.Should().ContainSingle(error => error == null);
        errors.Should().ContainSingle(error => error is IOException);
        var persisted = JsonSerializer.Deserialize<RecipeMigrationReport>(await File.ReadAllBytesAsync(_path));
        new[] { first.RunId, second.RunId }.Should().Contain(persisted!.RunId);
        persisted.Status.Should().Be("preview-valid");
        AssertNoTemporaryFiles();
    }

    [Theory]
    [InlineData("cancelled", 2)]
    [InlineData("filesystem", 1)]
    [InlineData("database", 1)]
    public async Task CommandFailure_ReturnsTheExistingNonzeroExitCode(string failure, int expected)
    {
        using var output = new StringWriter();
        var exit = await MigrationCommand.ExecuteAsync(() => failure switch
        {
            "cancelled" => throw new OperationCanceledException(),
            "filesystem" => throw new IOException("Synthetic report failure."),
            _ => throw new Microsoft.EntityFrameworkCore.DbUpdateException("Synthetic database failure.")
        }, output);
        exit.Should().Be(expected);
        output.ToString().Should().NotBeEmpty();
    }

    private static RecipeMigrationReport Report() => new(
        SchemaVersion: 4, RunId: Guid.CreateVersion7(), Status: "preview-valid",
        StartedAtUtc: DateTime.UtcNow, CompletedAtUtc: DateTime.UtcNow, DestinationOwnerId: Guid.NewGuid(),
        SourceFileSha256: new('A', 64), DecisionFileSha256: new('B', 64),
        SourceRecipes: 0, SelectedSourceRecipes: 0, LegacyOwnerFieldsPresent: 0, LegacyOwnerFieldsMissing: 0,
        MissingArchiveFields: 0, MissingDateFields: 0, BlankPlaceholdersOmitted: 0, UnknownFieldOccurrences: 0,
        ProposedInserts: 0, ProposedUpdates: 0, Unchanged: 0, CommittedInserts: 0, CommittedUpdates: 0,
        ImageManifestSha256: null, ValidatedImages: 0, Summary: new(0, 0, 0, 0),
        Images: [], SourceDispositions: [], Warnings: [], Diagnostics: [], Recipes: []);

    private void AssertNoTemporaryFiles() =>
        Directory.GetFiles(Path.GetDirectoryName(_path)!, Path.GetFileName(_path) + ".*.tmp").Should().BeEmpty();

    public void Dispose() => File.Delete(_path);
}
