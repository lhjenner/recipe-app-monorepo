namespace RecipeApp.Api.Grocery;

public sealed record GroceryRecipePreviewResponse(
    Guid RecipeId,
    string RecipeName,
    uint RecipeVersion,
    Guid? GroceryListId,
    uint ListVersion,
    bool HasOutstandingAddition,
    IReadOnlyList<GroceryRecipePreviewContribution> Included,
    IReadOnlyList<GroceryRecipePreviewExclusion> Excluded);

public sealed record GroceryRecipePreviewContribution(
    Guid RecipeStepId,
    int StepPosition,
    Guid RecipeStepItemId,
    int ItemPosition,
    Guid IngredientId,
    string IngredientName,
    bool IngredientIsArchived,
    string OriginalNameText,
    string? OriginalAmountText,
    string? OriginalUnitText,
    string? OriginalNotesText,
    QuantityInterpretationStatus InterpretationStatus,
    decimal? InterpretedAmount,
    QuantityDimension? Dimension,
    string? CanonicalUnit,
    string? CountLabel,
    string? UnresolvedReason);

public sealed record GroceryRecipePreviewExclusion(
    int StepPosition,
    int ItemPosition,
    string Text,
    string Reason);

public sealed record GroceryRecipeAddCommand(
    Guid CommandId,
    uint ExpectedListVersion,
    uint ExpectedRecipeVersion,
    bool ConfirmRepeat);

public sealed record GroceryRecipeRemovalCommand(
    Guid CommandId,
    uint ExpectedListVersion);
