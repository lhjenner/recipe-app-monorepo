using System.Data;
using Microsoft.EntityFrameworkCore;
using RecipeApp.Api.Infrastructure.Persistence;

namespace FirestoreMigration;

public sealed class RecipeImportWriter(AppDbContext db)
{
    public async Task<(int Inserted, int Updated, IReadOnlyList<RecipePreviewItem> Recipes)> ApplyAsync(
        RecipePreviewPlan plan, IReadOnlyList<RecipeSourceRecord> sourceRecipes,
        IReadOnlySet<string> approvedUpdates,
        MigrationOptions options, Guid runId, string sourceFileSha256,
        string decisionFingerprint, string targetFingerprint,
        RecipeSourceFiles sourceFiles, IReadOnlyList<Guid> sourceIds,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, RecipeTargetImage>? stagedImages = null)
    {
        var sourceByLegacyId = sourceRecipes.ToDictionary(recipe => recipe.LegacyId, StringComparer.Ordinal);
        var insertedRecipes = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var updatedRecipes = new Dictionary<string, Guid>(StringComparer.Ordinal);
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        await db.Database.ExecuteSqlRawAsync("""
            SET LOCAL lock_timeout = '10s';
            LOCK TABLE "Ingredients", "Recipes", "RecipeTags", "RecipeSteps",
                "RecipeStepItems", "RecipeImages", "RecipeImportProvenance"
                IN SHARE ROW EXCLUSIVE MODE;
            """, cancellationToken);
        using var sourceHandles = sourceFiles.VerifyAndHold();
        var currentFingerprint = await RecipeTargetFingerprint.ComputeAsync(
            db, options.Owner, plan.Recipes.Select(recipe => recipe.LegacyId).ToArray(),
            sourceIds, cancellationToken);
        if (currentFingerprint != targetFingerprint)
            throw new InvalidOperationException(
                "Recipe target state changed after preview; nothing was committed. Run and review again.");

        var legacyIds = plan.Recipes
            .Where(recipe => recipe.Outcome == "proposed-insert")
            .Select(recipe => recipe.LegacyId)
            .ToArray();
        var concurrentMatches = legacyIds.Length == 0
            ? []
            : await db.Recipes
                .Where(recipe => recipe.OwnerUserId == options.Owner &&
                    recipe.LegacyId != null && legacyIds.Contains(recipe.LegacyId))
                .Select(recipe => recipe.LegacyId!)
                .ToListAsync(cancellationToken);
        if (concurrentMatches.Count > 0)
            throw new InvalidOperationException(
                "Recipe mappings changed after preview; no Recipes were committed. Review a fresh preview.");

        var updateIds = plan.Recipes
            .Where(recipe => recipe.Outcome == "proposed-update" && approvedUpdates.Contains(recipe.LegacyId))
            .Select(recipe => recipe.LegacyId)
            .ToArray();
        var updateTargets = updateIds.Length == 0
            ? []
            : await db.Recipes
                .Where(recipe => recipe.OwnerUserId == options.Owner &&
                    recipe.LegacyId != null && updateIds.Contains(recipe.LegacyId))
                .Include(recipe => recipe.Tags)
                .Include(recipe => recipe.Steps).ThenInclude(step => step.Items)
                .Include(recipe => recipe.ImportProvenance)
                .Include(recipe => recipe.Image)
                .AsSplitQuery()
                .ToListAsync(cancellationToken);
        if (updateTargets.Count != updateIds.Length)
            throw new InvalidOperationException(
                "An approved Recipe mapping changed after preview. Nothing was committed; review a fresh preview.");

        var now = DateTime.UtcNow;
        foreach (var item in plan.Recipes.Where(recipe => recipe.Outcome == "proposed-insert"))
        {
            if (!sourceByLegacyId.TryGetValue(item.LegacyId, out var source))
                throw new InvalidOperationException(
                    $"Validated source Recipe '{item.LegacyId}' was not available for insertion.");

            var recipe = RecipeEntityMapper.Create(source, options.Owner, sourceFileSha256,
                decisionFingerprint, runId, now);
            RecipeImageStaging.Apply(recipe, AcceptedImage(source, stagedImages));
            db.Recipes.Add(recipe);
            insertedRecipes.Add(item.LegacyId, recipe.Id);
        }

        var targetsByLegacyId = updateTargets.ToDictionary(recipe => recipe.LegacyId!, StringComparer.Ordinal);
        foreach (var legacyId in updateIds)
        {
            if (!sourceByLegacyId.TryGetValue(legacyId, out var source))
                throw new InvalidOperationException(
                    $"Validated source Recipe '{legacyId}' was not available for update.");
            var target = targetsByLegacyId[legacyId];
            RecipeEntityMapper.Update(db, target, source, options.Owner, sourceFileSha256,
                decisionFingerprint, runId, now);
            RecipeImageStaging.Apply(target, AcceptedImage(source, stagedImages));
            updatedRecipes.Add(legacyId, target.Id);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var results = plan.Recipes.Select(item =>
        {
            if (insertedRecipes.TryGetValue(item.LegacyId, out var insertedId))
                return item with { ExistingRecipeId = insertedId, Outcome = "committed-insert" };
            if (updatedRecipes.TryGetValue(item.LegacyId, out var updatedId))
                return item with { ExistingRecipeId = updatedId, Outcome = "committed-update" };
            if (item.Outcome == "proposed-update" && !approvedUpdates.Contains(item.LegacyId))
                return item with { Outcome = "declined" };
            return item;
        }).ToArray();
        return (insertedRecipes.Count, updatedRecipes.Count, results);
    }

    private static RecipeTargetImage? AcceptedImage(RecipeSourceRecord source,
        IReadOnlyDictionary<string, RecipeTargetImage>? images)
    {
        if (string.IsNullOrWhiteSpace(source.ImageReference)) return null;
        return images?.GetValueOrDefault(source.LegacyId)
            ?? throw new InvalidOperationException("An accepted Recipe image was not verified and staged; no Recipes were committed.");
    }
}
