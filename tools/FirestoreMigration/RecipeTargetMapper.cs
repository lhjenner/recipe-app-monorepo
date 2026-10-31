using RecipeApp.Api.Infrastructure.Persistence;

namespace FirestoreMigration;

internal static class RecipeTargetMapper
{
    public static RecipeTargetRecord ToTarget(Recipe recipe) => new(
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
