using RecipeApp.Api.Ingredients;

namespace RecipeApp.Api.Grocery;

public sealed record GroceryManualEntryCommand(
    Guid CommandId,
    uint ExpectedListVersion,
    IReadOnlyList<GroceryManualEntryRequest> Items,
    IReadOnlyList<GroceryDuplicateReview> ReviewedDuplicates);

public sealed record GroceryManualEntryRequest(
    string? Name,
    string? Note,
    decimal PurchaseQuantity,
    string? PurchaseUnit,
    Guid? IngredientId = null,
    bool SaveAsIngredient = false,
    Guid? CategoryId = null,
    IReadOnlyList<IngredientStoreMappingRequest>? StoreMappings = null,
    Guid? AdHocStoreId = null,
    Guid? AdHocDepartmentId = null);

public sealed record GroceryDuplicateReview(
    int EntryIndex,
    Guid? TargetItemId,
    int? TargetEntryIndex,
    decimal ExistingQuantity,
    decimal Increment,
    decimal ResultingQuantity,
    string ItemName,
    string PurchaseUnit);

public sealed record GroceryItemUpdateRequest(
    Guid CommandId,
    uint ExpectedListVersion,
    decimal OutstandingPurchaseQuantity,
    string? PurchaseUnit,
    string? Note,
    GroceryDepartmentUpdate? DepartmentUpdate,
    Guid? AdHocStoreId,
    Guid? AdHocDepartmentId);

public sealed record GroceryDepartmentUpdate(
    Guid StoreId,
    Guid? DepartmentId,
    uint ExpectedIngredientVersion);

public sealed record GroceryWriteOutcome(
    GroceryCommandReceiptResponse? Receipt,
    int StatusCode,
    string? Title = null,
    string? Detail = null,
    Dictionary<string, string[]>? Errors = null,
    IReadOnlyList<GroceryDuplicateReview>? DuplicateReviews = null);

public sealed record GroceryCancelOutcome(
    int StatusCode,
    string? Title = null,
    string? Detail = null);
