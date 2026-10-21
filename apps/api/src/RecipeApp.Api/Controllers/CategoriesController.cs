using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecipeApp.Api.Lookups;

namespace RecipeApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/categories")]
public sealed class CategoriesController(ILookupService lookups) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<LookupResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LookupResponse>>> Get(
        CancellationToken cancellationToken) =>
        Ok(await lookups.GetCategoriesAsync(cancellationToken));

    [HttpPost]
    [ProducesResponseType<LookupResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LookupResponse>> Create(
        CreateLookupRequest request,
        CancellationToken cancellationToken) =>
        Ok(await lookups.CreateCategoryAsync(request, cancellationToken));
}
