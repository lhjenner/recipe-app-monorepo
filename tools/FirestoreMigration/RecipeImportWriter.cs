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
        string decisionFingerprint, CancellationToken cancellationToken)
    {
        var sourceByLegacyId = sourceRecipes.ToDictionary(recipe => recipe.LegacyId, StringComparer.Ordinal);
        var insertedRecipes = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var updatedRecipes = new Dictionary<string, Guid>(StringComparer.Ordinal);
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

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
}
