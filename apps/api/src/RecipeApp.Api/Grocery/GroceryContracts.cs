namespace RecipeApp.Api.Grocery;

public sealed record GroceryListResponse(
    Guid? Id,
    uint Version,
    int CompletedItemCount,
    IReadOnlyList<GroceryItemResponse> Items);

public sealed record GroceryItemResponse(
    Guid Id,
    Guid? IngredientId,
    string Name,
    string State,
    string? Note,
    QuantityDimension? Dimension,
    string? CountLabel,
    decimal? RequirementAmount,
    string? RequirementUnit,
    decimal OutstandingPurchaseQuantity,
    string PurchaseUnit,
    uint? IngredientVersion,
    Guid? AdHocStoreId,
    Guid? AdHocDepartmentId,
    string? AdHocDepartmentName,
    IReadOnlyList<GroceryStoreMappingResponse> StoreMappings,
    IReadOnlyList<GroceryContributionResponse> Contributions);

public sealed record GroceryStoreMappingResponse(
    Guid StoreId,
    string State,
    Guid? DepartmentId,
    string? DepartmentName);

public sealed record GroceryContributionResponse(
    Guid Id,
    GroceryContributionOrigin Origin,
    string State,
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

public sealed record GroceryHistoryResponse(
    IReadOnlyList<GroceryPurchaseEventResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record GroceryPurchaseEventResponse(
    Guid Id,
    Guid GroceryItemId,
    GroceryPurchaseEventKind Kind,
    decimal Quantity,
    string PurchaseUnit,
    DateTimeOffset OccurredAtUtc,
    string ItemNameSnapshot,
    string? NoteSnapshot,
    Guid? StoreId,
    string? StoreName,
    GroceryPurchaseOrigin Origin,
    Guid? ReversesEventId,
    bool IsReversed);

public sealed record GroceryListQuery(
    bool IncludeCompleted = false,
    DateOnly? From = null,
    DateOnly? To = null);

public sealed record GroceryCommandMetadata(Guid CommandId, uint ExpectedListVersion);

public sealed record GroceryPurchaseRequest(
    Guid CommandId,
    uint ExpectedListVersion,
    decimal ExpectedOutstandingQuantity,
    string PurchaseUnit);

public sealed record GroceryHistoryQuery(
    DateOnly? From,
    DateOnly? To,
    bool AllDates = false,
    int Page = 1,
    int PageSize = 25);

public sealed record GroceryCommandReceiptResponse(
    Guid CommandId,
    long ResultListVersion,
    IReadOnlyList<Guid> ResultIds);
