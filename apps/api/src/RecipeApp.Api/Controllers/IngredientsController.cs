using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecipeApp.Api.Ingredients;

namespace RecipeApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/ingredients")]
public sealed class IngredientsController(
    IIngredientReadService ingredients,
    IIngredientWriteService writes) : ControllerBase
{
    [HttpGet]
    [EndpointSummary("List ingredients")]
    [EndpointDescription("Returns a page of the authenticated user's ingredients, with search, archive, category, and store-mapping state.")]
    [ProducesResponseType<IngredientListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IngredientListResponse>> GetList(
        [FromQuery] IngredientListQuery query,
        CancellationToken cancellationToken)
    {
        var response = await ingredients.GetListAsync(query, cancellationToken);
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
    [EndpointSummary("Get ingredient details")]
    [EndpointDescription("Returns full ingredient details only when the ingredient belongs to the authenticated user.")]
    [ProducesResponseType<IngredientDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IngredientDetailResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var response = await ingredients.GetByIdAsync(id, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPost]
    [EndpointSummary("Create an ingredient")]
    [ProducesResponseType<IngredientDetailResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<IngredientDetailResponse>> Create(
        IngredientWriteRequest request,
        CancellationToken cancellationToken)
    {
        var outcome = await writes.CreateAsync(request, cancellationToken);
        return ToActionResult(outcome);
    }

    [HttpPut("{id:guid}")]
    [EndpointSummary("Update an ingredient")]
    [ProducesResponseType<IngredientDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IngredientDetailResponse>> Update(
        Guid id,
        IngredientWriteRequest request,
        CancellationToken cancellationToken)
    {
        var outcome = await writes.UpdateAsync(id, request, cancellationToken);
        return ToActionResult(outcome);
    }

    private ActionResult<IngredientDetailResponse> ToActionResult(IngredientWriteOutcome outcome)
    {
        if (outcome.Errors is not null)
        {
            return BadRequest(new ValidationProblemDetails(outcome.Errors));
        }

        if (outcome.StatusCode == StatusCodes.Status404NotFound)
        {
            return NotFound();
        }

        if (outcome.StatusCode == StatusCodes.Status409Conflict)
        {
            var problem = new ProblemDetails
            {
                Status = outcome.StatusCode,
                Title = outcome.Title,
                Detail = outcome.Detail
            };
            if (outcome.ArchivedIngredientId is not null)
            {
                problem.Extensions["archivedIngredientId"] = outcome.ArchivedIngredientId;
            }

            return new ObjectResult(problem) { StatusCode = outcome.StatusCode };
        }

        if (outcome.StatusCode == StatusCodes.Status201Created)
        {
            return CreatedAtAction(
                nameof(GetById),
                new { id = outcome.Ingredient!.Id },
                outcome.Ingredient);
        }

        return Ok(outcome.Ingredient);
    }
}
