using Microsoft.EntityFrameworkCore;
using RecipeApp.Api.Auth.PasswordReset;

namespace RecipeApp.Api.Infrastructure.Persistence;

public sealed class PasswordResetTokenRepository(AuthDbContext database) : IPasswordResetTokenRepository
{
    public async Task AddAsync(PasswordResetToken token, CancellationToken cancellationToken)
    {
        database.PasswordResetTokens.Add(token);
        await database.SaveChangesAsync(cancellationToken);
    }

    public Task<PasswordResetToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        database.PasswordResetTokens.AsNoTracking()
            .SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

    public Task InvalidateOutstandingAsync(Guid userId, CancellationToken cancellationToken) =>
        database.PasswordResetTokens
            .Where(token => token.UserId == userId && !token.Consumed)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.Consumed, true), cancellationToken);

    public async Task<bool> TryConsumeAsync(Guid tokenId, CancellationToken cancellationToken)
    {
        var updated = await database.PasswordResetTokens
            .Where(token => token.Id == tokenId && !token.Consumed)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.Consumed, true), cancellationToken);
        return updated == 1;
    }
}
