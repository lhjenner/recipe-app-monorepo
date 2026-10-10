namespace RecipeApp.Api.Auth;

public sealed class AccountLockedException(DateTime lockedUntilUtc)
    : Exception("Too many failed login attempts. Try again later.")
{
    public DateTime LockedUntilUtc { get; } = lockedUntilUtc;
}
