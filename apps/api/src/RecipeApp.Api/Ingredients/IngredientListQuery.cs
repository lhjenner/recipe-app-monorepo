using System.ComponentModel.DataAnnotations;

namespace RecipeApp.Api.Ingredients;

/// <summary>Filters and paging options for the ingredient list.</summary>
public sealed class IngredientListQuery
{
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 25;

    [MaxLength(200)]
    public string? Search { get; init; }

    public bool Archived { get; init; }
}
