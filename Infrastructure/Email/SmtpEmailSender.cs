using CarHub.Api.Infrastructure.Config;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

namespace CarHub.Api.Infrastructure.Email;

public sealed class SmtpEmailSender(IOptions<EmailNotificationOptions> options) : IEmailSender
{
    private readonly EmailNotificationOptions _options = options.Value;

    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            throw new InvalidOperationException("Email notifications are disabled.");
        }

        if (string.IsNullOrWhiteSpace(_options.SmtpHost)
            || string.IsNullOrWhiteSpace(_options.From)
            || string.IsNullOrWhiteSpace(to))
        {
            throw new InvalidOperationException("SMTP configuration is incomplete.");
        }

        using var client = new SmtpClient(_options.SmtpHost, _options.SmtpPort)
        {
            EnableSsl = _options.UseSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = string.IsNullOrWhiteSpace(_options.Username)
                ? CredentialCache.DefaultNetworkCredentials
                : new NetworkCredential(_options.Username, _options.Password)
        };

        using var message = new MailMessage
        {
            From = new MailAddress(_options.From),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };

        message.To.Add(to);

        await client.SendMailAsync(message, cancellationToken);
    }
}
