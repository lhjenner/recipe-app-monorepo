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

    Task<IngredientArchiveOutcome> ArchiveAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<IngredientArchiveOutcome> RestoreAsync(
        Guid id,
        CancellationToken cancellationToken);
}
