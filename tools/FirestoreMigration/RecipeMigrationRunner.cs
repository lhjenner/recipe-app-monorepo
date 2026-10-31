using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RecipeApp.Api.Images;
using RecipeApp.Api.Infrastructure.Persistence;

namespace FirestoreMigration;

public sealed class RecipeMigrationRunner(AppDbContext db, TextWriter output, TextReader confirmationInput,
    IPrivateObjectMigrationStorage? storage = null)
{
    public async Task<RecipeMigrationRunResult> RunAsync(MigrationOptions options,
        CancellationToken cancellationToken = default)
    {
        if (options.Mode != MigrationMode.Recipes || options.RecipeFile is null ||
            options.ReportFile is null)
            throw new ArgumentException("Recipe mode requires recipe and report paths.", nameof(options));

        var recipePath = Path.GetFullPath(options.RecipeFile);
        var decisionPath = Path.GetFullPath(options.Decisions);
        var reportPath = Path.GetFullPath(options.ReportFile);
        var imageManifestPath = options.ImageManifestFile is null ? null : Path.GetFullPath(options.ImageManifestFile);
        var inputPaths = new List<string> { recipePath, decisionPath, reportPath };
        if (imageManifestPath is not null) inputPaths.Add(imageManifestPath);
        if (options.ImageJournalFile is not null)
        {
            inputPaths.Add(Path.GetFullPath(options.ImageJournalFile));
            RecipeMigrationReportWriter.EnsureOutsideRepository(Path.GetFullPath(options.ImageJournalFile));
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
        var imageManifestBytes = imageManifestPath is null
            ? null
            : await File.ReadAllBytesAsync(imageManifestPath, cancellationToken);
        var sourceFiles = new RecipeSourceFiles(recipePath, recipeBytes,
            decisionPath, decisionBytes, imageManifestPath, imageManifestBytes);
        var utf8 = new UTF8Encoding(false, true);
        var recipeJson = utf8.GetString(recipeBytes).TrimStart('\uFEFF');
        var decisionJson = utf8.GetString(decisionBytes).TrimStart('\uFEFF');
        var imageManifestJson = imageManifestBytes is null
            ? null
            : utf8.GetString(imageManifestBytes).TrimStart('\uFEFF');
        var source = RecipeSourceParser.Parse(recipeJson, decisionJson);
        var imageManifest = imageManifestJson is null
            ? null
            : RecipeImageManifestParser.Parse(imageManifestJson);
        await RecipeMigrationInputConfirmation.ConfirmAsync(
            source, options, recipePath, decisionPath, imageManifestPath,
            confirmationInput, output,
            cancellationToken);
        var diagnostics = new List<string>(source.BlockingDiagnostics);
        var imageWarnings = new List<string>();
        var imageResults = new List<RecipeImageValidationResult>();

        if ((await db.Database.GetPendingMigrationsAsync(cancellationToken)).Any() ||
            db.Database.HasPendingModelChanges())
            throw new InvalidOperationException("Database schema is not current. Apply the API's EF migrations first.");
        if (!await db.Users.AnyAsync(user => user.Id == options.Owner, cancellationToken))
            throw new InvalidOperationException("Destination owner does not exist. Register the account first.");

        RecipePreviewPlan plan;
        IReadOnlyList<RecipeSourceRecord> selectedSourceRecipes = [];
        IReadOnlyList<RecipeTargetRecord> targetRecords = [];
        IReadOnlyList<Guid> sourceIds = [];
        string targetFingerprint;
        var sourceValidation = new RecipeSourceValidationResult([], []);
        await using (var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.RepeatableRead, cancellationToken))
        {
            await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY", cancellationToken);
            var selection = RecipeSourceValidator.Select(source, options.SelectedLegacyOwnerIds);
            var selected = selection.Recipes;
            var ids = selected.SelectMany(recipe => recipe.Steps)
                .SelectMany(step => step.Items)
                .Select(item => item.SourceId)
                .Distinct()
                .ToArray();
            sourceIds = ids;
            var ingredients = ids.Length == 0
                ? []
                : await db.Ingredients.AsNoTracking()
                    .Where(ingredient => ids.Contains(ingredient.Id))
                    .Select(ingredient => new RecipeIngredientTarget(
                        ingredient.Id, ingredient.OwnerUserId, ingredient.ArchivedAtUtc != null,
                        ingredient.ExcludeFromGroceryList))
                    .ToListAsync(cancellationToken);
            var validation = RecipeSourceValidator.Validate(source, options.Owner, ingredients,
                options.SelectedLegacyOwnerIds);
            sourceValidation = validation;
            selectedSourceRecipes = validation.Recipes;
            diagnostics.AddRange(validation.BlockingDiagnostics);
            var imageValidation = RecipeImageManifestValidator.Validate(
                selected, imageManifest, options.ImageRoot);
            diagnostics.AddRange(imageValidation.BlockingDiagnostics);
            imageWarnings.AddRange(imageValidation.Warnings);
            imageResults.AddRange(imageValidation.Images);
            foreach (var image in imageResults.Where(image => image.Status == "validated"))
                sourceFiles.IncludeImage(RecipeImageFiles.ResolveSelectedFile(
                    options.ImageRoot!, image.RelativePath), image.Sha256!);

            var legacyIds = selected.Select(recipe => recipe.LegacyId).Distinct(StringComparer.Ordinal).ToArray();
            var existing = legacyIds.Length == 0
                ? []
                : await db.Recipes.AsNoTracking()
                    .Where(recipe => recipe.OwnerUserId == options.Owner &&
                        recipe.LegacyId != null && legacyIds.Contains(recipe.LegacyId))
                    .Include(recipe => recipe.Tags)
                    .Include(recipe => recipe.Steps).ThenInclude(step => step.Items)
                    .Include(recipe => recipe.ImportProvenance)
                    .Include(recipe => recipe.Image)
                    .AsSplitQuery()
                    .ToListAsync(cancellationToken);
            var targets = existing.Select(RecipeTargetMapper.ToTarget).ToArray();
            targetRecords = targets;
            plan = RecipeImagePlanning.IncludeImages(
                RecipePreviewPlanner.Create(validation, options.Owner, targets), imageResults, targets);
            diagnostics.AddRange(plan.BlockingDiagnostics);
            targetFingerprint = await RecipeTargetFingerprint.ComputeAsync(
                db, options.Owner, legacyIds, ids, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        var status = diagnostics.Count == 0 ? "preview-valid" : "validation-blocked";
        var runId = Guid.CreateVersion7();
        var committedInserts = 0;
        var committedUpdates = 0;
        var reportRecipes = plan.Recipes;
        IReadOnlyList<JournalImage> journalImages = [];
        if (options.Write)
        {
            if (imageResults.Count > 0 && (storage is null || options.ImageJournalFile is null))
                diagnostics.Add("Image writes require configured private storage and --image-journal; no Recipes were written.");

            if (diagnostics.Count == 0)
            {
                var inputFingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                    Convert.ToHexString(SHA256.HashData(recipeBytes)) +
                    Convert.ToHexString(SHA256.HashData(decisionBytes)) +
                    (imageManifestBytes is null ? "" : Convert.ToHexString(SHA256.HashData(imageManifestBytes))) +
                    JsonSerializer.Serialize(selectedSourceRecipes.Select(recipe => recipe.LegacyId).Order(StringComparer.Ordinal)))));
                var commit = await RecipeMigrationApply.ApplyAsync(db, options, plan, selectedSourceRecipes,
                    targetRecords, imageResults, sourceFiles, sourceIds, targetFingerprint,
                    Convert.ToHexString(SHA256.HashData(recipeBytes)), source.DecisionFingerprint,
                    inputFingerprint, runId, storage, confirmationInput, output, cancellationToken);
                runId = commit.RunId;
                committedInserts = commit.Inserted;
                committedUpdates = commit.Updated;
                reportRecipes = commit.Recipes;
                journalImages = commit.JournalImages;
                status = "committed";
            }
            else
            {
                status = "validation-blocked";
            }
        }

        var report = new RecipeMigrationReport(
            3,
            runId,
            status,
            started,
            DateTime.UtcNow,
            options.Owner,
            Convert.ToHexString(SHA256.HashData(recipeBytes)),
            Convert.ToHexString(SHA256.HashData(decisionBytes)),
            source.Recipes.Count,
            selectedSourceRecipes.Count,
            source.OwnerFieldCounts.Present,
            source.OwnerFieldCounts.Missing,
            source.MissingArchiveCount,
            source.MissingDateCount,
            source.BlankPlaceholders.Count,
            source.UnknownFieldPaths.Count,
            plan.ProposedInserts,
            plan.ProposedUpdates,
            plan.Unchanged,
            committedInserts,
            committedUpdates,
            imageManifestBytes is null ? null : Convert.ToHexString(SHA256.HashData(imageManifestBytes)),
            imageResults.Count(image => image.Status == "validated"),
            RecipeMigrationReportBuilder.BuildSummary(selectedSourceRecipes, sourceValidation),
            imageResults,
            RecipeMigrationReportBuilder.BuildSourceDispositions(
                source, selectedSourceRecipes.Select(recipe => recipe.LegacyId).ToHashSet(StringComparer.Ordinal)),
            source.Warnings.Concat(imageWarnings).Distinct(StringComparer.Ordinal).ToArray(),
            diagnostics.Distinct(StringComparer.Ordinal).ToArray(),
            reportRecipes, options.ImageJournalFile, journalImages);
        try
        {
            await RecipeMigrationReportWriter.WriteAsync(reportPath, report, cancellationToken);
        }
        catch (Exception exception) when (status == "committed" &&
            exception is IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            output.WriteLine($"COMMITTED: Recipe run {runId}; report persistence failed. Reconcile database/provenance and image journal; do not assume rollback.");
            throw;
        }
        output.WriteLine($"Recipe {(options.Write ? "write" : "preview")}: owner={options.Owner}, status={status}, " +
            $"proposed-inserts={report.ProposedInserts}, proposed-updates={report.ProposedUpdates}, " +
            $"unchanged={report.Unchanged}, committed-inserts={report.CommittedInserts}, committed-updates={report.CommittedUpdates}, " +
            $"owner-fields-present={report.LegacyOwnerFieldsPresent}, owner-fields-missing={report.LegacyOwnerFieldsMissing}, " +
            $"blank-placeholders={report.BlankPlaceholdersOmitted}, unknown-fields={report.UnknownFieldOccurrences}, " +
            $"selected-source-recipes={report.SelectedSourceRecipes}, validated-images={report.ValidatedImages}, " +
            $"planned-headings={report.Summary.PlannedHeadingsConverted}, " +
            $"planned-notes={report.Summary.PlannedNotesConverted}, " +
            $"missing-ingredient-references={report.Summary.MissingIngredientReferences}, " +
            $"warnings={report.Warnings.Count}, errors={report.Diagnostics.Count}.");
        output.WriteLine($"Report: {reportPath}");
        return new(report.Diagnostics.Count, status, reportPath);
    }

}
