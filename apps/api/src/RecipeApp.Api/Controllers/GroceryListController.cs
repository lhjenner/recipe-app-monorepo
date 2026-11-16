using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecipeApp.Api.Grocery;

namespace RecipeApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/grocery-list")]
public sealed class GroceryListController(
    IGroceryReadService groceryReads,
    IGroceryWriteService groceryWrites) : ControllerBase
{
    [HttpPost("items")]
    [EndpointSummary("Add grocery items")]
    [EndpointDescription("Atomically adds one or more manual items and any explicitly selected Ingredients.")]
    [ProducesResponseType<GroceryCommandReceiptResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GroceryCommandReceiptResponse>> AddItems(
        GroceryManualEntryCommand command,
        CancellationToken cancellationToken)
    {
        var outcome = await groceryWrites.AddManualItemsAsync(command, cancellationToken);
        return ToWriteResult(outcome);
    }

    [HttpPut("items/{id:guid}")]
    [EndpointSummary("Update a grocery item")]
    [ProducesResponseType<GroceryCommandReceiptResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GroceryCommandReceiptResponse>> UpdateItem(
        Guid id,
        GroceryItemUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var outcome = await groceryWrites.UpdateItemAsync(id, request, cancellationToken);
        return ToWriteResult(outcome);
    }

    [HttpDelete("items/{id:guid}")]
    [EndpointSummary("Remove an outstanding grocery item")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelItem(
        Guid id,
        [FromHeader(Name = "X-Command-Id")] Guid commandId,
        [FromHeader(Name = "X-Expected-List-Version")] uint expectedListVersion,
        CancellationToken cancellationToken)
    {
        var outcome = await groceryWrites.CancelItemAsync(
            id,
            new GroceryCommandMetadata(commandId, expectedListVersion),
            cancellationToken);
        if (outcome.StatusCode == StatusCodes.Status204NoContent)
        {
            return NoContent();
        }

        if (outcome.StatusCode == StatusCodes.Status404NotFound)
        {
            return NotFound();
        }

        return Problem(
            statusCode: outcome.StatusCode,
            title: outcome.Title,
            detail: outcome.Detail);
    }

    [HttpGet]
    [EndpointSummary("Read the grocery list")]
    [EndpointDescription("Returns the authenticated user's outstanding items without creating a list.")]
    [ProducesResponseType<GroceryListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GroceryListResponse>> GetList(
        [FromQuery] GroceryListQuery query,
        CancellationToken cancellationToken)
    {
        if (!ValidateDates(query.From, query.To))
        {
            return ValidationProblem(ModelState);
        }

        return Ok(await groceryReads.GetListAsync(query, cancellationToken));
    }

    [HttpGet("purchase-history")]
    [EndpointSummary("Read grocery purchase history")]
    [EndpointDescription("Returns owner-scoped, date-filtered purchase events in stable pages.")]
    [ProducesResponseType<GroceryHistoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GroceryHistoryResponse>> GetHistory(
        [FromQuery] GroceryHistoryQuery query,
        CancellationToken cancellationToken)
    {
        if (query.Page < 1)
        {
            ModelState.AddModelError(nameof(query.Page), "Page must be greater than zero.");
        }

        if (query.PageSize is < 1 or > 100)
        {
            ModelState.AddModelError(nameof(query.PageSize), "Page size must be between 1 and 100.");
        }

        if (query.AllDates && (query.From is not null || query.To is not null))
        {
            ModelState.AddModelError(nameof(query.AllDates), "All-dates history cannot include date boundaries.");
        }
        else if (!query.AllDates && !ValidateDates(query.From, query.To))
        {
            return ValidationProblem(ModelState);
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var response = await groceryReads.GetHistoryAsync(query, cancellationToken);
        if (response is not null)
        {
            return Ok(response);
        }

        ModelState.AddModelError(nameof(query.Page), "Page must not exceed the last page of results.");
        return ValidationProblem(ModelState);
    }

    private bool ValidateDates(DateOnly? from, DateOnly? to)
    {
        if ((from is null) != (to is null))
        {
            ModelState.AddModelError(nameof(from), "Both start and end dates are required.");
        }
        else if (from == DateOnly.MinValue || to == DateOnly.MaxValue)
        {
            ModelState.AddModelError(nameof(from), "Dates are outside the supported range.");
        }
        else if (from is not null && to < from)
        {
            ModelState.AddModelError(nameof(to), "End date must not be before start date.");
        }

        return ModelState.IsValid;
    }

    private ActionResult<GroceryCommandReceiptResponse> ToWriteResult(GroceryWriteOutcome outcome)
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
            if (outcome.DuplicateReviews is not null)
            {
                problem.Extensions["duplicateReviews"] = outcome.DuplicateReviews;
            }

            return new ObjectResult(problem) { StatusCode = outcome.StatusCode };
        }

        return outcome.StatusCode == StatusCodes.Status201Created
            ? CreatedAtAction(nameof(GetList), value: outcome.Receipt)
            : Ok(outcome.Receipt);
    }
}
