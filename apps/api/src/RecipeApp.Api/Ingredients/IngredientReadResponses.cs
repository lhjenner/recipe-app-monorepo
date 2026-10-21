namespace RecipeApp.Api.Ingredients;

/// <summary>A store mapping state shown in the ingredient list.</summary>
public sealed record IngredientStoreSummaryResponse(
    string StoreCode,
    IngredientMappingState State,
    string? DepartmentName);

/// <summary>A single ingredient row returned by the paged list.</summary>
public sealed record IngredientSummaryResponse(
    Guid Id,
    string Name,
    string? CategoryName,
    bool ExcludeFromGroceryList,
    bool Archived,
    IReadOnlyList<IngredientStoreSummaryResponse> Stores);

/// <summary>A page of ingredients matching the requested search and archive filter.</summary>
public sealed record IngredientListResponse(
    IReadOnlyList<IngredientSummaryResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);

/// <summary>A store mapping state for a single ingredient.</summary>
public sealed record IngredientStoreDetailResponse(
    Guid StoreId,
    string StoreCode,
    string StoreName,
    IngredientMappingState State,
    Guid? DepartmentId,
    string? DepartmentName);

/// <summary>A supermarket product URL associated with an ingredient.</summary>
public sealed record IngredientProductLinkResponse(
    Guid Id,
    string ChainCode,
    string ChainName,
    string Url,
    string? VariantLabel,
    string? Note,
    bool SupportsBothPricingMethods,
    bool IsPreferred,
    int Position);

/// <summary>Per-100-gram nutrition values; null values are unknown.</summary>
public sealed record IngredientNutritionResponse(
    decimal? EnergyKcal,
    decimal? ProteinG,
    decimal? CarbohydrateG,
    decimal? SugarG,
    decimal? FatG,
    decimal? SaturatedFatG,
    decimal? FibreG,
    decimal? SodiumMg);

/// <summary>Full read-only ingredient details including lookup and store data.</summary>
public sealed record IngredientDetailResponse(
    Guid Id,
    string Name,
    Guid? CategoryId,
    string? CategoryName,
    bool ExcludeFromGroceryList,
    IngredientNutritionResponse? Nutrition,
    string? NutritionSource,
    DateTimeOffset? NutritionUpdatedAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? ArchivedAtUtc,
    uint Version,
    IReadOnlyList<IngredientStoreDetailResponse> StoreMappings,
    IReadOnlyList<IngredientProductLinkResponse> ProductLinks);
