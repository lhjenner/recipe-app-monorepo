using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RecipeApp.Api.Infrastructure.Persistence;
using RecipeApp.Api.Lookups;

namespace FirestoreMigration;

internal static class RecipeEntityMapper
{
    public static Recipe Create(RecipeSourceRecord source, Guid ownerId,
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
        AddChildren(recipe, source, ownerId);
        recipe.ImportProvenance = CreateProvenance(
            recipe.Id, source, sourceFileSha256, decisionFingerprint, runId, now);
        return recipe;
    }

    public static void Update(AppDbContext db, Recipe target, RecipeSourceRecord source,
        Guid ownerId, string sourceFileSha256, string decisionFingerprint, Guid runId, DateTime now)
    {
        target.Name = source.Name;
        target.NormalizedName = LookupName.Normalize(source.Name);
        target.SourceText = source.SourceText;
        target.SourceUrl = source.SourceUrl;
        target.ServingsText = source.ServingsText;
        target.PrepTimeText = source.PrepTimeText;
        target.CookTimeText = source.CookTimeText;
        target.DateAddedAtUtc = source.DateAddedAtUtc;
        target.IsArchived = source.IsArchived;
        target.UpdatedAtUtc = now;

        db.RecipeTags.RemoveRange(target.Tags);
        target.Tags.Clear();
        db.RecipeSteps.RemoveRange(target.Steps);
        target.Steps.Clear();
        AddChildren(target, source, ownerId);

        if (target.ImportProvenance is null)
            target.ImportProvenance = CreateProvenance(
                target.Id, source, sourceFileSha256, decisionFingerprint, runId, now);
        else
            SetProvenance(target.ImportProvenance, source, sourceFileSha256, decisionFingerprint, runId, now);
    }

    private static void AddChildren(Recipe recipe, RecipeSourceRecord source, Guid ownerId)
    {
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
                step.Items.Add(CreateItem(sourceItem, step.Id, ownerId));
            recipe.Steps.Add(step);
        }
    }

    private static RecipeStepItem CreateItem(RecipeSourceItem sourceItem, Guid recipeStepId, Guid ownerId) =>
        new()
        {
            RecipeStepId = recipeStepId,
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
        };

    private static RecipeImportProvenance CreateProvenance(Guid recipeId, RecipeSourceRecord source,
        string sourceFileSha256, string decisionFingerprint, Guid runId, DateTime now)
    {
        return new()
        {
            RecipeId = recipeId,
            SourceDocumentJson = source.SourceDocumentJson,
            DocumentSha256 = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(source.SourceDocumentJson))),
            SourceFileSha256 = sourceFileSha256,
            MappingVersion = "PRD04-v1",
            DecisionFingerprint = decisionFingerprint,
            LastAppliedRunId = runId,
            LastAppliedAtUtc = now
        };
    }

    private static void SetProvenance(RecipeImportProvenance provenance, RecipeSourceRecord source,
        string sourceFileSha256, string decisionFingerprint, Guid runId, DateTime now)
    {
        provenance.SourceDocumentJson = source.SourceDocumentJson;
        provenance.DocumentSha256 = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(source.SourceDocumentJson)));
        provenance.SourceFileSha256 = sourceFileSha256;
        provenance.MappingVersion = "PRD04-v1";
        provenance.DecisionFingerprint = decisionFingerprint;
        provenance.LastAppliedRunId = runId;
        provenance.LastAppliedAtUtc = now;
    }
}
