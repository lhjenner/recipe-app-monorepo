namespace RecipeApp.Api.Grocery;

public sealed record GroceryListResponse(
    Guid? Id,
    uint Version,
    IReadOnlyList<GroceryItemResponse> Items);

public sealed record GroceryItemResponse(
    Guid Id,
    Guid? IngredientId,
    string Name,
    decimal OutstandingPurchaseQuantity,
    string PurchaseUnit,
    IReadOnlyList<GroceryContributionResponse> Contributions);

public sealed record GroceryContributionResponse(
    Guid Id,
    GroceryContributionOrigin Origin,
    string? OriginalAmountText,
    string? OriginalUnitText,
    string? OriginalNotesText,
    decimal? InterpretedAmount,
    string? CanonicalUnit,
    string? UnresolvedReason,
    Guid? RecipeId,
    Guid? RecipeStepId,
    Guid? RecipeStepItemId,
    string? RecipeNameSnapshot);

public sealed record GroceryCommandMetadata(Guid CommandId, uint ExpectedListVersion);

public sealed record GroceryPurchaseRequest(
    Guid CommandId,
    uint ExpectedListVersion,
    decimal ExpectedOutstandingQuantity,
    string PurchaseUnit);

public sealed record GroceryHistoryQuery(
    DateOnly? From,
    DateOnly? To,
    int Page = 1,
    int PageSize = 25);

public sealed record GroceryCommandReceiptResponse(
    Guid CommandId,
    long ResultListVersion,
    IReadOnlyList<Guid> ResultIds);
