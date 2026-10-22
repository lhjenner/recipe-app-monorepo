namespace RecipeApp.Api.Ingredients;

public interface IIngredientWriteService
{
    Task<IngredientWriteOutcome> CreateAsync(
        IngredientWriteRequest request,
        CancellationToken cancellationToken);

    Task<IngredientWriteOutcome> UpdateAsync(
        Guid id,
        IngredientWriteRequest request,
        CancellationToken cancellationToken);
}
