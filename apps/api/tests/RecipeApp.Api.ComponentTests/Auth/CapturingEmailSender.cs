using System.Collections.Concurrent;
using RecipeApp.Api.Auth.PasswordReset;

namespace RecipeApp.Api.ComponentTests.Auth;

public sealed class CapturingEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _sent = new();

    public bool ShouldFail { get; set; }

    public IReadOnlyList<EmailMessage> Sent => _sent.ToArray();

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (ShouldFail)
        {
            throw new InvalidOperationException("Simulated SMTP outage.");
        }

        _sent.Enqueue(message);
        return Task.CompletedTask;
    }
}
