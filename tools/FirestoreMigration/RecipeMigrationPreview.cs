using System.Data;
using Microsoft.EntityFrameworkCore;
using RecipeApp.Api.Infrastructure.Persistence;

namespace FirestoreMigration;

internal sealed record RecipePreviewReadSet(IReadOnlyList<Guid> SourceIds, string Fingerprint);

internal static class RecipeMigrationPreview
{
    public static async Task<RecipePreviewReadSet> ReadAsync(AppDbContext db, MigrationOptions options,
        RecipeMigrationSource source, IReadOnlyList<RecipeImageManifestEntry>? manifest,
        RecipeSourceFiles files, RecipeReportSession report, CancellationToken cancellationToken)
    {
        if ((await db.Database.GetPendingMigrationsAsync(cancellationToken)).Any() ||
            db.Database.HasPendingModelChanges())
            throw new InvalidOperationException("Database schema is not current. Apply the API's EF migrations first.");
        if (!await db.Users.AnyAsync(user => user.Id == options.Owner, cancellationToken))
            throw new InvalidOperationException("Destination owner does not exist. Register the account first.");
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.RepeatableRead, cancellationToken);
        await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY", cancellationToken);
        var ids = report.Selected.SelectMany(recipe => recipe.Steps).SelectMany(step => step.Items)
            .Select(item => item.SourceId).Distinct().ToArray();
        var ingredients = ids.Length == 0 ? [] : await db.Ingredients.AsNoTracking()
            .Where(ingredient => ids.Contains(ingredient.Id))
            .Select(ingredient => new RecipeIngredientTarget(ingredient.Id, ingredient.OwnerUserId,
                ingredient.ArchivedAtUtc != null, ingredient.ExcludeFromGroceryList))
            .ToListAsync(cancellationToken);
        report.Validation = RecipeSourceValidator.Validate(source, options.Owner, ingredients, options.SelectedLegacyOwnerIds);
        report.Diagnostics.AddRange(report.Validation.BlockingDiagnostics);
        var imageValidation = RecipeImageManifestValidator.Validate(report.Selected, manifest, options.ImageRoot);
        report.Diagnostics.AddRange(imageValidation.BlockingDiagnostics);
        report.Warnings.AddRange(imageValidation.Warnings);
        report.Images.AddRange(imageValidation.Images);
        foreach (var image in report.Images.Where(image => image.Status == "validated"))
            files.IncludeImage(RecipeImageFiles.ResolveSelectedFile(options.ImageRoot!, image.RelativePath), image.Sha256!);
        var legacyIds = report.Selected.Select(recipe => recipe.LegacyId).Distinct(StringComparer.Ordinal).ToArray();
        var existing = legacyIds.Length == 0 ? [] : await db.Recipes.AsNoTracking()
            .Where(recipe => recipe.OwnerUserId == options.Owner && recipe.LegacyId != null &&
                legacyIds.Contains(recipe.LegacyId))
            .Include(recipe => recipe.Tags).Include(recipe => recipe.Steps).ThenInclude(step => step.Items)
            .Include(recipe => recipe.ImportProvenance).Include(recipe => recipe.Image)
            .AsSplitQuery().ToListAsync(cancellationToken);
        report.Targets = existing.Select(RecipeTargetMapper.ToTarget).ToArray();
        report.Plan = RecipeImagePlanning.IncludeImages(
            RecipePreviewPlanner.Create(report.Validation, options.Owner, report.Targets), report.Images, report.Targets);
        report.Diagnostics.AddRange(report.Plan.BlockingDiagnostics);
        var fingerprint = await RecipeTargetFingerprint.ComputeAsync(db, options.Owner, legacyIds, ids, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(ids, fingerprint);
    }
}
