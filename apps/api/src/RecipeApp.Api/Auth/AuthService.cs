namespace RecipeApp.Api.Auth;

// Application service for account registration.
public class AuthService(IUserRepository users, IPasswordHasher hasher)
{
    public async Task<UserDto> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        if (await users.ExistsByEmailAsync(request.Email, cancellationToken))
        {
            throw new DuplicateEmailException();
        }

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
