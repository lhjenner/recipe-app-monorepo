using Microsoft.EntityFrameworkCore;
using RecipeApp.Api.Infrastructure.Persistence;

namespace RecipeApp.Api.Grocery;

public sealed class GroceryRecipePreviewService(
    IDbContextFactory<AppDbContext> databaseFactory,
    ICurrentUser currentUser)
{
    public async Task<GroceryRecipePreviewResponse?> GetPreviewAsync(
        Guid recipeId,
        CancellationToken cancellationToken)
    {
        await using var database = await databaseFactory.CreateDbContextAsync(cancellationToken);
        var ownerId = currentUser.UserId;
        var recipe = await GroceryRecipeServiceSupport.LoadRecipeAsync(
            database, recipeId, ownerId, tracked: false, cancellationToken);
        if (recipe is null || GroceryRecipeServiceSupport.HasMissingIngredientReference(recipe))
        {
            return null;
        }

        var list = await database.GroceryLists.AsNoTracking()
            .SingleOrDefaultAsync(value => value.OwnerUserId == ownerId, cancellationToken);
        var hasOutstandingAddition = list is not null
            && await GroceryRecipeServiceSupport.HasOutstandingAdditionAsync(
                database, recipeId, ownerId, list.Id, cancellationToken);
        var included = new List<GroceryRecipePreviewContribution>();
        var excluded = new List<GroceryRecipePreviewExclusion>();
        foreach (var step in recipe.Steps.OrderBy(value => value.Position))
        {
            foreach (var item in step.Items.OrderBy(value => value.Position))
            {
                if (item.Kind != RecipeStepItemKind.Ingredient)
                {
                    excluded.Add(new GroceryRecipePreviewExclusion(
                        step.Position, item.Position, item.Text, "Headings and notes are not grocery items."));
                    continue;
                }

                var ingredient = item.Ingredient!;
                if (ingredient.ExcludeFromGroceryList)
                {
                    excluded.Add(new GroceryRecipePreviewExclusion(
                        step.Position, item.Position, ingredient.Name, "This Ingredient is excluded from grocery lists."));
                    continue;
                }

                var interpretation = QuantityInterpreter.InterpretRecipe(item.QuantityText, item.UnitText);
                included.Add(new GroceryRecipePreviewContribution(
                    step.Id,
                    step.Position,
                    item.Id,
                    item.Position,
                    ingredient.Id,
                    ingredient.Name,
                    ingredient.ArchivedAtUtc is not null,
                    item.Text,
                    item.QuantityText,
                    item.UnitText,
                    item.NotesText,
                    interpretation.Status,
                    interpretation.Amount,
                    interpretation.Dimension,
                    interpretation.CanonicalUnit,
                    interpretation.CountLabel,
                    interpretation.UnresolvedReason));
            }
        }

        return new GroceryRecipePreviewResponse(
            recipe.Id,
            recipe.Name,
            recipe.Version,
            GroceryRecipeServiceSupport.GetIngredientFingerprint(recipe),
            list?.Id,
            list?.Version ?? 0,
            hasOutstandingAddition,
            included,
            excluded);
    }
}
