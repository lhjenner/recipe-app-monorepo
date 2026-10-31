using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RecipeApp.Api.Infrastructure.Persistence;

namespace FirestoreMigration;

public sealed record RecipeMigrationRunResult(int Errors, string Status, string ReportPath);

public sealed record RecipeMigrationReport(
    int SchemaVersion,
    Guid RunId,
    string Status,
    DateTime StartedAtUtc,
    DateTime CompletedAtUtc,
    Guid DestinationOwnerId,
    string SourceFileSha256,
    string DecisionFileSha256,
    int SourceRecipes,
    int SelectedSourceRecipes,
    int LegacyOwnerFieldsPresent,
    int LegacyOwnerFieldsMissing,
    int MissingArchiveFields,
    int MissingDateFields,
    int BlankPlaceholdersOmitted,
    int UnknownFieldOccurrences,
    int ProposedInserts,
    int ProposedUpdates,
    int Unchanged,
    int CommittedInserts,
    int CommittedUpdates,
    string? ImageManifestSha256,
    int ValidatedImages,
    RecipeMigrationRunSummary Summary,
    IReadOnlyList<RecipeImageValidationResult> Images,
    IReadOnlyList<RecipeMigrationSourceDisposition> SourceDispositions,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Diagnostics,
    IReadOnlyList<RecipePreviewItem> Recipes);

public sealed class RecipeMigrationRunner(AppDbContext db, TextWriter output)
{
    public async Task<RecipeMigrationRunResult> RunAsync(MigrationOptions options,
        CancellationToken cancellationToken = default)
    {
        if (options.Mode != MigrationMode.Recipes || options.RecipeFile is null ||
            options.ReportFile is null)
            throw new ArgumentException("Recipe mode requires recipe and report paths.", nameof(options));
        if (options.Write)
            throw new InvalidOperationException("Recipe write mode is not implemented; no data was written.");

        var recipePath = Path.GetFullPath(options.RecipeFile);
        var decisionPath = Path.GetFullPath(options.Decisions);
        var reportPath = Path.GetFullPath(options.ReportFile);
        var imageManifestPath = options.ImageManifestFile is null ? null : Path.GetFullPath(options.ImageManifestFile);
        var inputPaths = new List<string> { recipePath, decisionPath, reportPath };
        if (imageManifestPath is not null) inputPaths.Add(imageManifestPath);
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
        var sourceValidation = new RecipeSourceValidationResult([], []);
        await using (var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.RepeatableRead, cancellationToken))
        {
            await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY", cancellationToken);
            var selection = RecipeSourceValidator.Validate(source, options.Owner, [],
                options.SelectedLegacyOwnerIds);
            var selected = selection.Recipes;
            var ids = selected.SelectMany(recipe => recipe.Steps)
                .SelectMany(step => step.Items)
                .Select(item => item.SourceId)
                .Distinct()
                .ToArray();
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

            var legacyIds = selected.Select(recipe => recipe.LegacyId).Distinct(StringComparer.Ordinal).ToArray();
            var existing = legacyIds.Length == 0
                ? []
                : await db.Recipes.AsNoTracking()
                    .Where(recipe => recipe.OwnerUserId == options.Owner &&
                        recipe.LegacyId != null && legacyIds.Contains(recipe.LegacyId))
                    .Include(recipe => recipe.Tags)
                    .Include(recipe => recipe.Steps).ThenInclude(step => step.Items)
                    .Include(recipe => recipe.ImportProvenance)
                    .AsSplitQuery()
                    .ToListAsync(cancellationToken);
            var targets = existing.Select(ToTarget).ToArray();
            plan = RecipePreviewPlanner.Create(validation, options.Owner, targets);
            diagnostics.AddRange(plan.BlockingDiagnostics);
            await transaction.CommitAsync(cancellationToken);
        }

        var status = diagnostics.Count == 0 ? "preview-valid" : "validation-blocked";
        output.WriteLine($"Destination owner: {options.Owner}");
        output.WriteLine(options.SelectedLegacyOwnerIds is null
            ? $"Source owners: {string.Join(", ", source.LegacyOwnerIds.Order(StringComparer.Ordinal))} (all; explicit selection not required)"
            : $"Source-owner selection: {string.Join(", ", options.SelectedLegacyOwnerIds.Order(StringComparer.Ordinal))}");
        var report = new RecipeMigrationReport(
            3,
            Guid.CreateVersion7(),
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
            0,
            0,
            imageManifestBytes is null ? null : Convert.ToHexString(SHA256.HashData(imageManifestBytes)),
            imageResults.Count(image => image.Status == "validated"),
            RecipeMigrationReportBuilder.BuildSummary(selectedSourceRecipes, sourceValidation),
            imageResults,
            RecipeMigrationReportBuilder.BuildSourceDispositions(
                source, selectedSourceRecipes.Select(recipe => recipe.LegacyId).ToHashSet(StringComparer.Ordinal)),
            source.Warnings.Concat(imageWarnings).Distinct(StringComparer.Ordinal).ToArray(),
            diagnostics.Distinct(StringComparer.Ordinal).ToArray(),
            plan.Recipes);
        await RecipeMigrationReportWriter.WriteAsync(reportPath, report, cancellationToken);
        output.WriteLine($"Recipe preview: owner={options.Owner}, status={status}, " +
            $"proposed-inserts={report.ProposedInserts}, proposed-updates={report.ProposedUpdates}, " +
            $"unchanged={report.Unchanged}, committed-inserts=0, committed-updates=0, " +
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

    private static RecipeTargetRecord ToTarget(Recipe recipe) => new(
        recipe.Id,
        recipe.LegacyId!,
        recipe.OwnerUserId,
        recipe.Name,
        recipe.SourceText,
        recipe.SourceUrl,
        recipe.ServingsText,
        recipe.PrepTimeText,
        recipe.CookTimeText,
        recipe.DateAddedAtUtc,
        recipe.IsArchived,
        recipe.Tags.OrderBy(tag => tag.Position).Select(tag => tag.Text).ToArray(),
        recipe.Steps.OrderBy(step => step.Position).Select(step => new RecipeTargetStep(
            step.Position,
            step.Instructions,
            step.Items.OrderBy(item => item.Position).Select(item => new RecipeTargetItem(
                item.Position,
                item.Kind.ToString(),
                item.IngredientId,
                item.Text,
                item.QuantityText,
                item.UnitText,
                item.NotesText)).ToArray())).ToArray(),
        recipe.ImportProvenance?.SourceDocumentJson);

}
