using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RecipeApp.Api.Infrastructure.Persistence;
using RecipeApp.Api.Lookups;

namespace FirestoreMigration;

public sealed class RecipeImportWriter(AppDbContext db)
{
    public async Task<(int Inserted, IReadOnlyList<RecipePreviewItem> Recipes)> CommitNewRecipesAsync(
        RecipePreviewPlan plan, IReadOnlyList<RecipeSourceRecord> sourceRecipes,
        MigrationOptions options, Guid runId, string sourceFileSha256,
        string decisionFingerprint, CancellationToken cancellationToken)
    {
        var sourceByLegacyId = sourceRecipes.ToDictionary(recipe => recipe.LegacyId, StringComparer.Ordinal);
        var insertedRecipes = new Dictionary<string, Guid>(StringComparer.Ordinal);
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

        var now = DateTime.UtcNow;
        foreach (var item in plan.Recipes.Where(recipe => recipe.Outcome == "proposed-insert"))
        {
            if (!sourceByLegacyId.TryGetValue(item.LegacyId, out var source))
                throw new InvalidOperationException(
                    $"Validated source Recipe '{item.LegacyId}' was not available for insertion.");

            var recipe = CreateRecipe(source, options.Owner, sourceFileSha256,
                decisionFingerprint, runId, now);
            db.Recipes.Add(recipe);
            insertedRecipes.Add(item.LegacyId, recipe.Id);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return (insertedRecipes.Count, plan.Recipes.Select(item =>
            insertedRecipes.TryGetValue(item.LegacyId, out var recipeId)
                ? item with { ExistingRecipeId = recipeId, Outcome = "committed-insert" }
                : item).ToArray());
    }

    private static Recipe CreateRecipe(RecipeSourceRecord source, Guid ownerId,
        string sourceFileSha256, string decisionFingerprint, Guid runId, DateTime now)
    {
        var recipe = new Recipe
        {
            LegacyId = source.LegacyId,
            OwnerUserId = ownerId,
            Name = source.Name,
            NormalizedName = LookupName.Normalize(source.Name),
            SourceText = source.SourceText,
            SourceUrl = source.SourceUrl,
            ServingsText = source.ServingsText,
            PrepTimeText = source.PrepTimeText,
            CookTimeText = source.CookTimeText,
            DateAddedAtUtc = source.DateAddedAtUtc,
            IsArchived = source.IsArchived,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        foreach (var (text, position) in source.Tags.Select((text, position) => (text, position)))
            recipe.Tags.Add(new()
            {
                RecipeId = recipe.Id,
                Position = position,
                Text = text
            });

        foreach (var sourceStep in source.Steps)
        {
            var step = new RecipeStep
            {
                RecipeId = recipe.Id,
                OwnerUserId = ownerId,
                Position = sourceStep.Position,
                Instructions = sourceStep.Instructions
            };
            foreach (var sourceItem in sourceStep.Items)
            {
                step.Items.Add(new()
                {
                    RecipeStepId = step.Id,
                    OwnerUserId = ownerId,
                    Position = sourceItem.Position,
                    Kind = sourceItem.Kind switch
                    {
                        RecipeSourceItemKind.Ingredient => RecipeStepItemKind.Ingredient,
                        RecipeSourceItemKind.Heading => RecipeStepItemKind.Heading,
                        RecipeSourceItemKind.Note => RecipeStepItemKind.Note,
                        _ => throw new ArgumentOutOfRangeException(nameof(sourceItem.Kind))
                    },
                    IngredientId = sourceItem.IngredientId,
                    Text = sourceItem.Text,
                    QuantityText = sourceItem.QuantityText,
                    UnitText = sourceItem.UnitText,
                    NotesText = sourceItem.NotesText
                });
            }
            recipe.Steps.Add(step);
        }

        recipe.ImportProvenance = new()
        {
            RecipeId = recipe.Id,
            SourceDocumentJson = source.SourceDocumentJson,
            DocumentSha256 = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(source.SourceDocumentJson))),
            SourceFileSha256 = sourceFileSha256,
            MappingVersion = "PRD04-v1",
            DecisionFingerprint = decisionFingerprint,
            LastAppliedRunId = runId,
            LastAppliedAtUtc = now
        };
        return recipe;
    }
}
