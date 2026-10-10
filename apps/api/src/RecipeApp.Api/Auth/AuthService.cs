namespace RecipeApp.Api.Auth;

// Application service for account registration and login.
public class AuthService(IUserRepository users, IPasswordHasher hasher, TimeProvider timeProvider)
{
    public const int MaxFailedLoginAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

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

        if (user is null)
        {
            throw new InvalidCredentialsException();
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (user.LockedUntilUtc is { } lockedUntil)
        {
            if (lockedUntil > now)
            {
                throw new AccountLockedException(lockedUntil);
            }

            // The lockout has expired, so the next failure starts a fresh count.
            user.LockedUntilUtc = null;
            user.FailedLoginAttempts = 0;
        }

        if (!hasher.Verify(request.Password, user.PasswordHash))
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= MaxFailedLoginAttempts)
            {
                user.LockedUntilUtc = now.Add(LockoutDuration);
            }

            await users.UpdateAsync(user, cancellationToken);
            throw new InvalidCredentialsException();
        }

        if (user.FailedLoginAttempts != 0 || user.LockedUntilUtc is not null)
        {
            user.FailedLoginAttempts = 0;
            user.LockedUntilUtc = null;
            await users.UpdateAsync(user, cancellationToken);
        }

        return new UserDto(user.Id, user.Email, user.CreatedAtUtc);
    }

    public async Task<UserDto?> GetUserByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(id, cancellationToken);
        return user is null ? null : new UserDto(user.Id, user.Email, user.CreatedAtUtc);
    }
}
