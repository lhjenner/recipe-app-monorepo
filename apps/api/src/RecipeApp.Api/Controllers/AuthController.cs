using Microsoft.AspNetCore.Mvc;

namespace RecipeApp.Api.Auth;

[ApiController]
[Route("api/auth")]
public class AuthController(AuthService authService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<UserDto>> Register(
        RegisterRequest request, CancellationToken cancellationToken)
    {
        var user = await authService.RegisterAsync(request, cancellationToken);
        // CreatedAtAction produces 201 + a Location header pointing at the new resource.
        return CreatedAtAction(nameof(Register), new { id = user.Id }, user);
    }
}