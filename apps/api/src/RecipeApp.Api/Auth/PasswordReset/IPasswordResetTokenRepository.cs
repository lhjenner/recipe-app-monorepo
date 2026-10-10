namespace RecipeApp.Api.Auth.PasswordReset;

public interface IPasswordResetTokenRepository
{
    Task AddAsync(PasswordResetToken token, CancellationToken cancellationToken);
    Task<PasswordResetToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken);

    // Marks every unused token for the user as consumed.
    Task InvalidateOutstandingAsync(Guid userId, CancellationToken cancellationToken);

    // Atomically flips Consumed false -> true; returns false if another request got there first.
    Task<bool> TryConsumeAsync(Guid tokenId, CancellationToken cancellationToken);
}
