using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecipeApp.Api.Lookups;

namespace RecipeApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/supermarkets")]
public sealed class SupermarketsController(ILookupService lookups) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<SupermarketChainResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SupermarketChainResponse>>> Get(
        CancellationToken cancellationToken) =>
        Ok(await lookups.GetSupermarketsAsync(cancellationToken));
}
