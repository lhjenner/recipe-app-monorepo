using System.ComponentModel.DataAnnotations;

namespace RecipeApp.Api.Ingredients;

public sealed record IngredientWriteRequest
{
    [Required]
    public required string Name { get; init; }
    public Guid? CategoryId { get; init; }
    public bool ExcludeFromGroceryList { get; init; }
    public IngredientNutritionRequest? Nutrition { get; init; }
    public string? NutritionSource { get; init; }
    public IReadOnlyList<IngredientStoreMappingRequest> StoreMappings { get; init; } = [];
    public IReadOnlyList<IngredientProductLinkRequest> ProductLinks { get; init; } = [];
    public uint Version { get; init; }
}

public sealed record IngredientNutritionRequest(
    decimal? EnergyKcal,
    decimal? ProteinG,
    decimal? CarbohydrateG,
    decimal? SugarG,
    decimal? FatG,
    decimal? SaturatedFatG,
    decimal? FibreG,
    decimal? SodiumMg);

public sealed record IngredientStoreMappingRequest(
    Guid StoreId,
    Guid? DepartmentId,
    bool IsNotStocked);

public sealed record IngredientProductLinkRequest(
    Guid ChainId,
    string? Url,
    string? VariantLabel,
    string? Note,
    bool SupportsBothPricingMethods,
    bool IsPreferred);

public sealed record IngredientWriteOutcome(
    IngredientDetailResponse? Ingredient,
    int StatusCode,
    string? Title = null,
    string? Detail = null,
    Guid? ArchivedIngredientId = null,
    Dictionary<string, string[]>? Errors = null);
