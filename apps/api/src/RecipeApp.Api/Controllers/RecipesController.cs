using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecipeApp.Api.Recipes;

namespace RecipeApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/recipes")]
public sealed class RecipesController(IRecipeReadService recipes) : ControllerBase
{
    [HttpGet]
    [EndpointSummary("List recipes")]
    [EndpointDescription("Returns a page of the authenticated user's active recipes, with name search.")]
    [ProducesResponseType<RecipeListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RecipeListResponse>> GetList(
        [FromQuery] RecipeListQuery query,
        CancellationToken cancellationToken)
    {
        var response = await recipes.GetListAsync(query, cancellationToken);
        if (response is not null)
        {
            return Ok(response);
        }

        ModelState.AddModelError(
            nameof(query.Page),
            "Page must not exceed the last page of results.");
        return ValidationProblem(ModelState);
    }

    [HttpGet("{id:guid}")]
    [EndpointSummary("Get recipe details")]
    [EndpointDescription("Returns ordered details only when the recipe is active and belongs to the authenticated user.")]
    [ProducesResponseType<RecipeDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RecipeDetailResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var response = await recipes.GetByIdAsync(id, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }
}
