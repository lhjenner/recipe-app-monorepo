namespace RecipeApp.Api.Grocery;

public sealed class GroceryWriteService(
    GroceryManualEntryService manualEntries,
    GroceryItemMutationService itemMutations,
    GroceryItemCancellationService itemCancellations,
    GroceryPurchaseService purchases,
    GroceryPurchaseReversalService reversals) : IGroceryWriteService
{
    public Task<GroceryWriteOutcome> AddManualItemsAsync(
        GroceryManualEntryCommand command,
        CancellationToken cancellationToken) =>
        manualEntries.AddManualItemsAsync(command, cancellationToken);

    public Task<GroceryWriteOutcome> UpdateItemAsync(
        Guid itemId,
        GroceryItemUpdateRequest request,
        CancellationToken cancellationToken) =>
        itemMutations.UpdateItemAsync(itemId, request, cancellationToken);

    public Task<GroceryCancelOutcome> CancelItemAsync(
        Guid itemId,
        GroceryCommandMetadata command,
        CancellationToken cancellationToken) =>
        itemCancellations.CancelItemAsync(itemId, command, cancellationToken);

    public Task<GroceryCancelOutcome> CancelAllItemsAsync(
        GroceryCommandMetadata command,
        CancellationToken cancellationToken) =>
        itemCancellations.CancelAllItemsAsync(command, cancellationToken);

    public Task<GroceryWriteOutcome> PurchaseItemAsync(
        Guid itemId,
        GroceryPurchaseRequest request,
        CancellationToken cancellationToken) =>
        purchases.PurchaseItemAsync(itemId, request, cancellationToken);

    public Task<GroceryWriteOutcome> ReversePurchaseAsync(
        Guid eventId,
        GroceryCommandMetadata command,
        CancellationToken cancellationToken) =>
        reversals.ReversePurchaseAsync(eventId, command, cancellationToken);
}
