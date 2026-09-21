namespace RecipeApp.Api.Auth;

// The application-layer service: where registration business logic lives.
// Dependencies arrive via constructor injection — it receives "something that
// satisfies each contract" and neither knows nor cares which implementation.
// RegisterAsync deliberately throws: the skeleton's job is to compile and
// fail the test at runtime (proper red phase), not to work yet.
public class AuthService(IUserRepository users, IPasswordHasher hasher)
{
    public async Task<UserDto> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        User user = new()
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            PasswordHash = hasher.Hash(request.Password),
            CreatedAtUtc = DateTime.UtcNow
        };
        await users.AddAsync(user, cancellationToken);
        return new UserDto(user.Id, user.Email, user.CreatedAtUtc);
    }
}
