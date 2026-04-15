using System.Globalization;
using System.Net;
using CarHub.Api.Domain.Enums;
using CarHub.Api.Infrastructure.Config;
using CarHub.Api.Infrastructure.Email;
using CarHub.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CarHub.Api.Infrastructure.Notifications;

public sealed class SubscriptionExpiryReminderWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<SubscriptionReminderOptions> reminderOptions,
    ILogger<SubscriptionExpiryReminderWorker> logger) : BackgroundService
{
    private readonly SubscriptionReminderOptions _options = reminderOptions.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Subscription expiry reminder worker disabled.");
            return;
        }

        var intervalMinutes = Math.Clamp(_options.CheckIntervalMinutes, 5, 720);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(intervalMinutes));

        await ProcessOnceAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await ProcessOnceAsync(stoppingToken);
        }
    }

    private async Task ProcessOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
            var smsSender = scope.ServiceProvider.GetRequiredService<ISmsSender>();

            var now = DateTime.UtcNow;
            var leadHours = Math.Clamp(_options.LeadTimeHours, 1, 168);
            var windowMinutes = Math.Clamp(_options.WindowMinutes, 10, 360);

            var target = now.AddHours(leadHours);
            var windowStart = target.AddMinutes(-windowMinutes);
            var windowEnd = target.AddMinutes(windowMinutes);

            var subscriptions = await dbContext.ProfessionalSubscriptions
                .Include(x => x.User)
                .Where(x => x.Status == SubscriptionStatus.Active
                    && x.EndsAtUtc >= windowStart
                    && x.EndsAtUtc <= windowEnd
                    && (x.ExpiryReminderEmailSentAtUtc == null || x.ExpiryReminderSmsSentAtUtc == null)
                    && x.User != null
                    && x.User.IsActive)
                .OrderBy(x => x.EndsAtUtc)
                .Take(200)
                .ToListAsync(cancellationToken);

            foreach (var subscription in subscriptions)
            {
                if (subscription.User is null) continue;

                var subject = "Votre abonnement CarHub expire dans 1 jour";
                var emailHtml = BuildEmailHtml(subscription.User.FullName, subscription.EndsAtUtc);
                var smsText = BuildSmsText(subscription.User.FullName, subscription.EndsAtUtc);

                if (subscription.ExpiryReminderEmailSentAtUtc == null && !string.IsNullOrWhiteSpace(subscription.User.Email))
                {
                    try
                    {
                        await emailSender.SendAsync(subscription.User.Email, subject, emailHtml, cancellationToken);
                        subscription.ExpiryReminderEmailSentAtUtc = DateTime.UtcNow;
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to send subscription expiry email reminder for subscription {SubscriptionId}", subscription.Id);
                    }
                }

                var smsNumber = string.IsNullOrWhiteSpace(subscription.User.PhoneNumber)
                    ? subscription.User.WhatsAppNumber
                    : subscription.User.PhoneNumber;

                if (subscription.ExpiryReminderSmsSentAtUtc == null && !string.IsNullOrWhiteSpace(smsNumber))
                {
                    try
                    {
                        await smsSender.SendAsync(smsNumber!, smsText, cancellationToken);
                        subscription.ExpiryReminderSmsSentAtUtc = DateTime.UtcNow;
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to send subscription expiry SMS reminder for subscription {SubscriptionId}", subscription.Id);
                    }
                }
            }

            if (subscriptions.Count > 0)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // normal shutdown
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Subscription expiry reminder worker failed.");
        }
    }

    private static string BuildSmsText(string? fullName, DateTime endsAtUtc)
    {
        var safeName = string.IsNullOrWhiteSpace(fullName) ? "" : $" {fullName}";
        var endText = endsAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

        return $"Bonjour{safeName},\n\nVotre abonnement professionnel CarHub expire le {endText}.\nRenouvelez-le avant expiration pour continuer à publier vos nouvelles annonces.\n\nCarHub Madagascar";
    }

    private static string BuildEmailHtml(string? fullName, DateTime endsAtUtc)
    {
        var safeName = string.IsNullOrWhiteSpace(fullName) ? "Utilisateur" : WebUtility.HtmlEncode(fullName.Trim());
        var endText = endsAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

        return $@"<!doctype html>
<html lang=""fr"">
  <body style=""margin:0;padding:0;background:#f3f6fb;font-family:Segoe UI,Arial,sans-serif;color:#0f172a;"">
    <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" style=""padding:24px 12px;"">
      <tr>
        <td align=""center"">
          <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" style=""max-width:640px;background:#ffffff;border:1px solid #dbe4f0;border-radius:14px;overflow:hidden;"">
            <tr>
              <td style=""padding:16px 22px;background:linear-gradient(90deg,#1d4ed8 0%,#2563eb 100%);color:#ffffff;"">
                <div style=""font-size:12px;letter-spacing:.08em;text-transform:uppercase;opacity:.92;font-weight:700;"">CarHub Madagascar</div>
                <div style=""margin-top:6px;font-size:22px;font-weight:800;line-height:1.2;"">Rappel d'abonnement</div>
              </td>
            </tr>
            <tr>
              <td style=""padding:20px 22px 8px 22px;font-size:16px;line-height:1.45;"">
                Bonjour <strong>{safeName}</strong>,
              </td>
            </tr>
            <tr>
              <td style=""padding:2px 22px 12px 22px;font-size:15px;line-height:1.5;color:#1f3558;"">
                Votre abonnement professionnel expire le <strong>{endText}</strong>.
                Pensez à le renouveler avant expiration pour continuer à publier vos nouvelles annonces.
              </td>
            </tr>
            <tr>
              <td style=""padding:0 22px 20px 22px;"">
                <a href=""https://carhub.mg/dashboard/subscription"" style=""display:inline-block;background:#1d4ed8;color:#ffffff;text-decoration:none;font-weight:700;border-radius:10px;padding:10px 16px;"">Gérer mon abonnement</a>
              </td>
            </tr>
            <tr>
              <td style=""padding:0 22px 20px 22px;font-size:13px;line-height:1.5;color:#64748b;"">
                Merci d'utiliser CarHub Madagascar.
              </td>
            </tr>
          </table>
        </td>
      </tr>
    </table>
  </body>
</html>";
    }
}
