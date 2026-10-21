using System.Security.Claims;

namespace RecipeApp.Api.Infrastructure.Persistence;

public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public Guid UserId
    {
        get
        {
            var userIdValue = httpContextAccessor.HttpContext?.User
                .FindFirstValue(ClaimTypes.NameIdentifier);
            if (Guid.TryParse(userIdValue, out var userId))
            {
                return userId;
            }

            throw new InvalidOperationException("The authenticated user ID claim is missing or invalid.");
        }
    }
}
