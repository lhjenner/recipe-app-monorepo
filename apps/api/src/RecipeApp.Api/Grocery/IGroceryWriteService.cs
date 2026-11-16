namespace RecipeApp.Api.Grocery;

public interface IGroceryWriteService
{
    Task<GroceryWriteOutcome> AddManualItemsAsync(
        GroceryManualEntryCommand command,
        CancellationToken cancellationToken);

    Task<GroceryWriteOutcome> UpdateItemAsync(
        Guid itemId,
        GroceryItemUpdateRequest request,
        CancellationToken cancellationToken);

    Task<GroceryCancelOutcome> CancelItemAsync(
        Guid itemId,
        GroceryCommandMetadata command,
        CancellationToken cancellationToken);
}
