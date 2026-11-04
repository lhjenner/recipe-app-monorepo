using Microsoft.EntityFrameworkCore;
using RecipeApp.Api.Infrastructure.Persistence;

namespace RecipeApp.Api.Recipes;

public sealed class RecipeReadService(
    IDbContextFactory<AppDbContext> databaseFactory,
    ICurrentUser currentUser) : IRecipeReadService
{
    public async Task<RecipeListResponse?> GetListAsync(
        RecipeListQuery query,
        CancellationToken cancellationToken)
    {
        await using var database = await databaseFactory.CreateDbContextAsync(cancellationToken);
        var recipesQuery = database.Recipes
            .AsNoTracking()
            .Where(recipe => recipe.OwnerUserId == currentUser.UserId && !recipe.IsArchived);

        var search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            var pattern = $"%{search.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
            recipesQuery = recipesQuery.Where(recipe =>
                EF.Functions.ILike(recipe.Name, pattern, "\\")
                || recipe.Steps.Any(step => step.Items.Any(item =>
                    item.Kind == RecipeStepItemKind.Ingredient
                    && item.Ingredient != null
                    && EF.Functions.ILike(item.Ingredient.Name, pattern, "\\"))));
        }

        var totalCount = await recipesQuery.CountAsync(cancellationToken);
        var pageCount = Math.Max(1, (int)Math.Ceiling(totalCount / (double)query.PageSize));
        if (query.Page > pageCount)
        {
            return null;
        }

        var recipes = await recipesQuery
            .OrderBy(recipe => recipe.NormalizedName)
            .ThenBy(recipe => recipe.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(recipe => new RecipeSummaryResponse(recipe.Id, recipe.Name))
            .ToListAsync(cancellationToken);

        return new RecipeListResponse(recipes, totalCount, query.Page, query.PageSize);
    }
}
