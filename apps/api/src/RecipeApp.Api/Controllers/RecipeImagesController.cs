using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using RecipeApp.Api.Images;

namespace RecipeApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/recipes/{id:guid}/image")]
public sealed class RecipeImagesController(IRecipeImageReadService images) : ControllerBase
{
    /// <summary>Returns the current owner's active recipe image from private storage.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var image = await images.GetAsync(id, cancellationToken);
        if (image is null) return NotFound();

        Response.Headers.CacheControl = "private, no-cache";
        Response.Headers.XContentTypeOptions = "nosniff";
        return new FileContentResult(image.Bytes, image.ContentType)
        {
            EntityTag = new EntityTagHeaderValue($"\"{image.Sha256}\"")
        };
    }
}
