using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RecipeApp.Api.Images;
using RecipeApp.Api.Infrastructure.Persistence;

namespace FirestoreMigration;

public sealed class RecipeMigrationRunner(AppDbContext db, TextWriter output, TextReader confirmationInput,
    IPrivateObjectMigrationStorage? storage = null, IRecipeReportPublisher? reportPublisher = null)
{
    public async Task<RecipeMigrationRunResult> RunAsync(MigrationOptions options,
        CancellationToken cancellationToken = default)
    {
        if (options.Mode != MigrationMode.Recipes || options.RecipeFile is null || options.ReportFile is null)
            throw new ArgumentException("Recipe mode requires recipe and report paths.", nameof(options));
        var recipePath = Path.GetFullPath(options.RecipeFile);
        var decisionPath = Path.GetFullPath(options.Decisions);
        var reportPath = Path.GetFullPath(options.ReportFile);
        var manifestPath = options.ImageManifestFile is null ? null : Path.GetFullPath(options.ImageManifestFile);
        var inputPaths = new List<string> { recipePath, decisionPath, reportPath, reportPath + ".lock" };
        if (manifestPath is not null) inputPaths.Add(manifestPath);
        if (options.ImageJournalFile is not null)
        {
            var journalPath = Path.GetFullPath(options.ImageJournalFile);
            inputPaths.Add(journalPath);
            inputPaths.Add(journalPath + ".lock");
            RecipeMigrationReportWriter.EnsureOutsideRepository(journalPath);
        }
        RecipeMigrationReportWriter.EnsureDistinctInputs(inputPaths);
        RecipeMigrationReportWriter.EnsureOutsideRepository(reportPath);
        if (File.Exists(reportPath))
            throw new IOException("Report destination already exists; choose a new path to preserve prior reports.");
        if (!Directory.Exists(Path.GetDirectoryName(reportPath)))
            throw new DirectoryNotFoundException("Report destination directory does not exist.");

        var started = DateTime.UtcNow;
        var recipeBytes = await File.ReadAllBytesAsync(recipePath, cancellationToken);
        var decisionBytes = await File.ReadAllBytesAsync(decisionPath, cancellationToken);
        var manifestBytes = manifestPath is null ? null : await File.ReadAllBytesAsync(manifestPath, cancellationToken);
        var files = new RecipeSourceFiles(recipePath, recipeBytes, decisionPath, decisionBytes, manifestPath, manifestBytes);
        var utf8 = new UTF8Encoding(false, true);
        var source = RecipeSourceParser.Parse(utf8.GetString(recipeBytes).TrimStart('\uFEFF'),
            utf8.GetString(decisionBytes).TrimStart('\uFEFF'));
        await RecipeMigrationInputConfirmation.ConfirmAsync(source, options, recipePath, decisionPath, manifestPath,
            confirmationInput, output, cancellationToken);
        var sourceHash = Hash(recipeBytes);
        var decisionHash = Hash(decisionBytes);
        var manifestHash = manifestBytes is null ? null : Hash(manifestBytes);
        using var report = new RecipeReportSession(options with { ReportFile = reportPath },
            source, started, sourceHash, decisionHash, manifestHash,
            RecipeImageRecovery.DatabaseScope(db), reportPublisher);
        try
        {
            await report.SaveAsync(cancellationToken);
            var manifest = manifestBytes is null ? null :
                RecipeImageManifestParser.Parse(utf8.GetString(manifestBytes).TrimStart('\uFEFF'));
            var readSet = await RecipeMigrationPreview.ReadAsync(db, options, source, manifest, files, report, cancellationToken);
            if (options.Write && report.Images.Count > 0 && (storage is null || options.ImageJournalFile is null))
                report.Diagnostics.Add("Image writes require configured private storage and --image-journal; no Recipes were written.");
            report.Progress.Status = report.Diagnostics.Count == 0 ? "preview-valid" : "validation-blocked";
            await report.SaveAsync(cancellationToken);
            if (options.Write && report.Diagnostics.Count == 0)
            {
                var fingerprint = Hash(Encoding.UTF8.GetBytes(sourceHash + decisionHash + manifestHash +
                    JsonSerializer.Serialize(report.Selected.Select(recipe => recipe.LegacyId).Order(StringComparer.Ordinal))));
                await RecipeMigrationApply.ApplyAsync(db, options, report.Plan, report.Selected,
                    report.Targets, report.Images, files, readSet.SourceIds, readSet.Fingerprint,
                    sourceHash, source.DecisionFingerprint, fingerprint, report.Progress,
                    storage, confirmationInput, output, report.SaveAsync, cancellationToken);
            }
        }
        catch (Exception failure)
        {
            await report.SaveFailureAsync(failure, output);
            throw;
        }
        try
        {
            await report.SaveAsync(cancellationToken);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            report.PrintPersistenceFailure(output, failure);
            await report.SaveFailureAsync(failure, output);
            throw;
        }
        var snapshot = report.Snapshot();
        output.WriteLine($"Recipe {(options.Write ? "write" : "preview")}: owner={options.Owner}, status={snapshot.Status}, " +
            $"proposed-inserts={snapshot.ProposedInserts}, proposed-updates={snapshot.ProposedUpdates}, " +
            $"unchanged={snapshot.Unchanged}, committed-inserts={snapshot.CommittedInserts}, committed-updates={snapshot.CommittedUpdates}, " +
            $"selected-source-recipes={snapshot.SelectedSourceRecipes}, validated-images={snapshot.ValidatedImages}, " +
            $"planned-headings={snapshot.Summary.PlannedHeadingsConverted}, planned-notes={snapshot.Summary.PlannedNotesConverted}, " +
            $"committed-headings={snapshot.Summary.CommittedHeadingsConverted}, committed-notes={snapshot.Summary.CommittedNotesConverted}, " +
            $"missing-ingredient-references={snapshot.Summary.MissingIngredientReferences}, " +
            $"warnings={snapshot.Warnings.Count}, errors={snapshot.Diagnostics.Count}.");
        output.WriteLine($"Report: {reportPath}");
        return new(snapshot.Diagnostics.Count, snapshot.Status, reportPath);
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}
