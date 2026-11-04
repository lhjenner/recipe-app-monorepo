namespace RecipeApp.Api.Recipes;

public sealed record RecipeSummaryResponse(
    Guid Id,
    string Name);

public sealed record RecipeListResponse(
    IReadOnlyList<RecipeSummaryResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);

public enum RecipeReadItemKind
{
    Ingredient,
    Heading,
    Note
}

public sealed record RecipeTagResponse(
    int Position,
    string Text);

public sealed record RecipeStepItemResponse(
    int Position,
    RecipeReadItemKind Kind,
    string Text,
    string? IngredientName,
    string? QuantityText,
    string? UnitText,
    string? NotesText);

public sealed record RecipeStepResponse(
    int Position,
    IReadOnlyList<RecipeStepItemResponse> Items,
    string Instructions);

public sealed record RecipeImageMetadataResponse(
    string Url,
    int Width,
    int Height,
    string Sha256);

public sealed record RecipeDetailResponse(
    Guid Id,
    string Name,
    string? SourceText,
    string? SourceUrl,
    string? ServingsText,
    string? PrepTimeText,
    string? CookTimeText,
    DateTime? DateAddedAtUtc,
    IReadOnlyList<RecipeTagResponse> Tags,
    IReadOnlyList<RecipeStepResponse> Steps,
    RecipeImageMetadataResponse? Image);
