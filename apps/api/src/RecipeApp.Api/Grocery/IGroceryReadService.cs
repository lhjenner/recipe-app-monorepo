namespace RecipeApp.Api.Grocery;

public interface IGroceryReadService
{
    Task<GroceryListResponse> GetListAsync(
        GroceryListQuery query,
        CancellationToken cancellationToken);

    Task<GroceryHistoryResponse?> GetHistoryAsync(
        GroceryHistoryQuery query,
        CancellationToken cancellationToken);
}
