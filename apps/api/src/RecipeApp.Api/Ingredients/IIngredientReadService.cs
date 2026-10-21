namespace RecipeApp.Api.Ingredients;

public interface IIngredientReadService
{
    Task<IngredientListResponse?> GetListAsync(
        IngredientListQuery query,
        CancellationToken cancellationToken);

    Task<IngredientDetailResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken);
}
