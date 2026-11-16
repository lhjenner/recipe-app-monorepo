namespace RecipeApp.Api.Grocery;

public sealed class GroceryWriteService(
    GroceryManualEntryService manualEntries,
    GroceryItemMutationService itemMutations,
    GroceryItemCancellationService itemCancellations) : IGroceryWriteService
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
}
