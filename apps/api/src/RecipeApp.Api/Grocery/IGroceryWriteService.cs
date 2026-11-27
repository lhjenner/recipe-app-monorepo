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

    Task<GroceryCancelOutcome> CancelAllItemsAsync(
        GroceryCommandMetadata command,
        CancellationToken cancellationToken);

    Task<GroceryWriteOutcome> PurchaseItemAsync(
        Guid itemId,
        GroceryPurchaseRequest request,
        CancellationToken cancellationToken);

    Task<GroceryWriteOutcome> ReversePurchaseAsync(
        Guid eventId,
        GroceryCommandMetadata command,
        CancellationToken cancellationToken);
}
