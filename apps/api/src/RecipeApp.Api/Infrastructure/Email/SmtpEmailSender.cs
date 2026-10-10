using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using RecipeApp.Api.Auth.PasswordReset;

namespace RecipeApp.Api.Infrastructure.Email;

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string From { get; set; } = string.Empty;
    public string? Username { get; set; }
    // Supplied through User Secrets locally and environment variables elsewhere, never appsettings.json.
    public string? Password { get; set; }
    public bool EnableSsl { get; set; } = true;
}

public sealed class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.Host) || string.IsNullOrWhiteSpace(settings.From))
        {
            throw new InvalidOperationException("SMTP is not configured (Smtp:Host and Smtp:From are required).");
        }

        using var client = new SmtpClient(settings.Host, settings.Port) { EnableSsl = settings.EnableSsl };
        if (!string.IsNullOrEmpty(settings.Username))
        {
            client.Credentials = new NetworkCredential(settings.Username, settings.Password);
        }

        using var mail = new MailMessage(settings.From, message.To, message.Subject, message.Body);
        await client.SendMailAsync(mail, cancellationToken);
    }
}
