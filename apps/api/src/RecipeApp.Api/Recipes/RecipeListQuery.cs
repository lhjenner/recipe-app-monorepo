using System.ComponentModel.DataAnnotations;

namespace RecipeApp.Api.Recipes;

public sealed class RecipeListQuery
{
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 25;

    [MaxLength(200)]
    public string? Search { get; init; }
}
