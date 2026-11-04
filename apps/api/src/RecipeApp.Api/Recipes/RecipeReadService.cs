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

    public async Task<RecipeDetailResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        await using var database = await databaseFactory.CreateDbContextAsync(cancellationToken);
        var recipe = await database.Recipes
            .AsNoTracking()
            .Where(item => item.Id == id
                && item.OwnerUserId == currentUser.UserId
                && !item.IsArchived)
            .Include(item => item.Tags)
            .Include(item => item.Steps)
                .ThenInclude(step => step.Items)
                    .ThenInclude(stepItem => stepItem.Ingredient)
            .Include(item => item.Image)
            .AsSplitQuery()
            .SingleOrDefaultAsync(cancellationToken);
        if (recipe is null)
        {
            return null;
        }

        var tags = recipe.Tags
            .OrderBy(tag => tag.Position)
            .Select(tag => new RecipeTagResponse(tag.Position, tag.Text))
            .ToArray();
        var steps = recipe.Steps
            .OrderBy(step => step.Position)
            .Select(step => new RecipeStepResponse(
                step.Position,
                step.Items
                    .OrderBy(stepItem => stepItem.Position)
                    .Select(stepItem => new RecipeStepItemResponse(
                        stepItem.Position,
                        ToReadKind(stepItem.Kind),
                        stepItem.Text,
                        stepItem.Ingredient?.Name,
                        stepItem.QuantityText,
                        stepItem.UnitText,
                        stepItem.NotesText))
                    .ToArray(),
                step.Instructions))
            .ToArray();
        var image = recipe.Image is null
            ? null
            : new RecipeImageMetadataResponse(
                $"/api/recipes/{recipe.Id:D}/image",
                recipe.Image.Width,
                recipe.Image.Height,
                recipe.Image.Sha256);

        return new RecipeDetailResponse(
            recipe.Id,
            recipe.Name,
            recipe.SourceText,
            recipe.SourceUrl,
            recipe.ServingsText,
            recipe.PrepTimeText,
            recipe.CookTimeText,
            recipe.DateAddedAtUtc,
            tags,
            steps,
            image);
    }

    private static RecipeReadItemKind ToReadKind(RecipeStepItemKind kind) =>
        kind switch
        {
            RecipeStepItemKind.Ingredient => RecipeReadItemKind.Ingredient,
            RecipeStepItemKind.Heading => RecipeReadItemKind.Heading,
            RecipeStepItemKind.Note => RecipeReadItemKind.Note,
            _ => throw new InvalidOperationException($"Unknown recipe step item kind: {kind}.")
        };
}
