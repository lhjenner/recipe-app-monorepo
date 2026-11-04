namespace RecipeApp.Api.Recipes;

public interface IRecipeReadService
{
    Task<RecipeListResponse?> GetListAsync(
        RecipeListQuery query,
        CancellationToken cancellationToken);

    Task<RecipeDetailResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken);
}
