namespace RecipeApp.Api.Auth;

// Application service for account registration and login.
public class AuthService(IUserRepository users, IPasswordHasher hasher)
{
    public async Task<UserDto> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await users.ExistsByEmailAsync(email, cancellationToken))
        {
            throw new DuplicateEmailException();
        }

        User user = new()
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = hasher.Hash(request.Password),
            CreatedAtUtc = DateTime.UtcNow
        };
        await users.AddAsync(user, cancellationToken);
        return new UserDto(user.Id, user.Email, user.CreatedAtUtc);
    }

    public async Task<UserDto> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await users.GetByEmailAsync(email, cancellationToken);

        if (user is null || !hasher.Verify(request.Password, user.PasswordHash))
        {
            throw new InvalidCredentialsException();
        }

        return new UserDto(user.Id, user.Email, user.CreatedAtUtc);
    }

    public async Task<UserDto?> GetUserByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(id, cancellationToken);
        return user is null ? null : new UserDto(user.Id, user.Email, user.CreatedAtUtc);
    }
}
