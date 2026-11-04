using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using RecipeApp.Api.Recipes;

namespace RecipeApp.Api.UnitTests.Recipes;

public sealed class RecipeListQueryTests
{
    [Fact]
    public void Defaults_match_the_recipe_list_contract()
    {
        var query = new RecipeListQuery();

        query.Page.Should().Be(1);
        query.PageSize.Should().Be(25);
        query.Search.Should().BeNull();
        Validate(query).Should().BeEmpty();
    }

    [Theory]
    [InlineData(1, 1, null)]
    [InlineData(3, 100, null)]
    [InlineData(1, 25, "")]
    public void Query_validation_accepts_values_within_contract_bounds(
        int page,
        int pageSize,
        string? search)
    {
        var query = new RecipeListQuery
        {
            Page = page == 0 ? 1 : page,
            PageSize = pageSize == 0 ? 25 : pageSize,
            Search = search
        };

        Validate(query).Should().BeEmpty();
    }

    [Theory]
    [InlineData(0, 25)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void Query_validation_rejects_values_outside_paging_bounds(int page, int pageSize)
    {
        var query = new RecipeListQuery { Page = page, PageSize = pageSize };

        Validate(query).Should().NotBeEmpty();
    }

    [Fact]
    public void Search_is_limited_to_200_characters()
    {
        var query = new RecipeListQuery { Search = new string('x', 201) };

        Validate(query).Should().ContainSingle();
    }

    private static IReadOnlyList<ValidationResult> Validate(RecipeListQuery query)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(query, new ValidationContext(query), results, validateAllProperties: true);
        return results;
    }
}
