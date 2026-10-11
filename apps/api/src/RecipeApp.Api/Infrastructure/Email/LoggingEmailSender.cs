using RecipeApp.Api.Auth.PasswordReset;

namespace RecipeApp.Api.Infrastructure.Email;

// Development-only stand-in so the reset flow works without an SMTP server: the link lands in the log.
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        logger.LogInformation("Email to {To}: {Subject}\n{Body}", message.To, message.Subject, message.Body);
        return Task.CompletedTask;
    }
}
