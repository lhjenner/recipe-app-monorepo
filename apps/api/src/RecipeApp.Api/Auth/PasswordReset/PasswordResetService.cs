using Microsoft.Extensions.Options;

namespace RecipeApp.Api.Auth.PasswordReset;

public class PasswordResetService(
    IUserRepository users,
    IPasswordResetTokenRepository tokens,
    IPasswordHasher hasher,
    IEmailSender emailSender,
    TimeProvider timeProvider,
    IOptions<PasswordResetOptions> options,
    ILogger<PasswordResetService> logger)
{
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(1);

    // Never reveals whether the email is registered: callers get the same outcome either way.
    public async Task RequestAsync(string email, CancellationToken cancellationToken)
    {
        var user = await users.GetByEmailAsync(email.Trim().ToLowerInvariant(), cancellationToken);
        if (user is null)
        {
            return;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var rawToken = PasswordResetTokenHasher.Generate();

        await tokens.InvalidateOutstandingAsync(user.Id, cancellationToken);
        await tokens.AddAsync(new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = PasswordResetTokenHasher.Hash(rawToken),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.Add(TokenLifetime)
        }, cancellationToken);

        try
        {
            await emailSender.SendAsync(BuildMessage(user.Email, rawToken), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The client must still see success, otherwise delivery failures would leak which emails exist.
            logger.LogError(exception, "Failed to send a password reset email for user {UserId}", user.Id);
        }
    }

    public async Task ResetAsync(string rawToken, string newPassword, CancellationToken cancellationToken)
    {
        var token = await tokens.FindByHashAsync(PasswordResetTokenHasher.Hash(rawToken), cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        if (token is null || token.Consumed || token.ExpiresAtUtc <= now)
        {
            throw new InvalidResetTokenException();
        }

        // Consuming first closes the race where two requests present the same token.
        if (!await tokens.TryConsumeAsync(token.Id, cancellationToken))
        {
            throw new InvalidResetTokenException();
        }

        var user = await users.GetByIdAsync(token.UserId, cancellationToken)
            ?? throw new InvalidResetTokenException();

        user.PasswordHash = hasher.Hash(newPassword);
        user.FailedLoginAttempts = 0;
        user.LockedUntilUtc = null;
        await users.UpdateAsync(user, cancellationToken);
    }

    private EmailMessage BuildMessage(string to, string rawToken)
    {
        var link = $"{options.Value.PublicBaseUrl.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(rawToken)}";
        var body = $"We received a request to reset your Recipe App password.\n\n" +
                   $"Open this link within 1 hour to choose a new password:\n{link}\n\n" +
                   "If you did not request this, you can ignore this email.";
        return new EmailMessage(to, "Reset your Recipe App password", body);
    }
}
