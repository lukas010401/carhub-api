using System.Globalization;
using System.Net;
using CarHub.Api.Domain.Entities;
using CarHub.Api.Infrastructure.Email;

namespace CarHub.Api.Infrastructure.Notifications;

public sealed class PaymentNotificationService(IEmailSender emailSender, IWhatsAppSender whatsAppSender, ILogger<PaymentNotificationService> logger) : IPaymentNotificationService
{
    public async Task NotifyPaymentReviewedAsync(ManualPaymentRequest payment, User seller, bool approved, CancellationToken cancellationToken = default)
    {
        var statusLabel = approved ? "validé" : "rejeté";
        var subject = approved ? "Paiement validé - CarHub Madagascar" : "Paiement rejeté - CarHub Madagascar";

        var typeLabel = payment.Type switch
        {
            Domain.Enums.ManualPaymentType.ListingPublication => "Publication d'annonce",
            Domain.Enums.ManualPaymentType.ProfessionalSubscriptionRenewal => "Abonnement professionnel",
            _ => payment.Type.ToString()
        };

        var htmlBody = BuildPaymentReviewedHtml(
            sellerName: seller.FullName,
            approved: approved,
            internalReference: payment.InternalReference,
            typeLabel: typeLabel,
            amountAr: payment.ExpectedAmount,
            reviewedAtUtc: DateTime.UtcNow);

        var textBody = BuildPaymentReviewedText(
            sellerName: seller.FullName,
            statusLabel: statusLabel,
            internalReference: payment.InternalReference,
            typeLabel: typeLabel,
            amountAr: payment.ExpectedAmount);

        try
        {
            if (!string.IsNullOrWhiteSpace(seller.Email))
            {
                await emailSender.SendAsync(seller.Email, subject, htmlBody, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send payment status email for payment {PaymentId}", payment.Id);
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(seller.WhatsAppNumber))
            {
                await whatsAppSender.SendAsync(seller.WhatsAppNumber, textBody, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send payment status WhatsApp for payment {PaymentId}", payment.Id);
        }
    }

    private static string BuildPaymentReviewedText(string? sellerName, string statusLabel, string internalReference, string typeLabel, decimal amountAr)
    {
        var safeName = string.IsNullOrWhiteSpace(sellerName) ? "" : $" {sellerName.Trim()}";
        return $"Bonjour{safeName},\n\nVotre paiement ({internalReference}) a été {statusLabel}.\nType: {typeLabel}\nMontant: {amountAr:N0} Ar\n\nMerci,\nCarHub Madagascar";
    }

    private static string BuildPaymentReviewedHtml(string? sellerName, bool approved, string internalReference, string typeLabel, decimal amountAr, DateTime reviewedAtUtc)
    {
        var safeName = string.IsNullOrWhiteSpace(sellerName) ? "Utilisateur" : WebUtility.HtmlEncode(sellerName.Trim());
        var statusLabel = approved ? "Validé" : "Rejeté";
        var statusColor = approved ? "#166534" : "#b42318";
        var statusBg = approved ? "#e8f8ef" : "#feeceb";
        var reviewedAt = reviewedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

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
                <div style=""margin-top:6px;font-size:22px;font-weight:800;line-height:1.2;"">Mise à jour paiement</div>
              </td>
            </tr>
            <tr>
              <td style=""padding:20px 22px 8px 22px;font-size:16px;line-height:1.45;"">
                Bonjour <strong>{safeName}</strong>, votre paiement a été traité.
              </td>
            </tr>
            <tr>
              <td style=""padding:2px 22px 0 22px;"">
                <span style=""display:inline-block;padding:6px 12px;border-radius:999px;background:{statusBg};color:{statusColor};font-size:12px;font-weight:800;letter-spacing:.03em;text-transform:uppercase;"">{statusLabel}</span>
              </td>
            </tr>
            <tr>
              <td style=""padding:14px 22px 6px 22px;"">
                <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" style=""border:1px solid #e6edf7;border-radius:10px;overflow:hidden;"">
                  <tr><td style=""padding:10px 12px;background:#f8fafc;font-size:13px;color:#475569;width:44%;"">Référence CarHub</td><td style=""padding:10px 12px;font-size:14px;font-weight:700;color:#0f172a;"">{WebUtility.HtmlEncode(internalReference)}</td></tr>
                  <tr><td style=""padding:10px 12px;background:#f8fafc;font-size:13px;color:#475569;"">Type</td><td style=""padding:10px 12px;font-size:14px;color:#0f172a;"">{WebUtility.HtmlEncode(typeLabel)}</td></tr>
                  <tr><td style=""padding:10px 12px;background:#f8fafc;font-size:13px;color:#475569;"">Montant</td><td style=""padding:10px 12px;font-size:14px;color:#0f172a;"">{amountAr:N0} Ar</td></tr>
                  <tr><td style=""padding:10px 12px;background:#f8fafc;font-size:13px;color:#475569;"">Date traitement</td><td style=""padding:10px 12px;font-size:14px;color:#0f172a;"">{reviewedAt}</td></tr>
                </table>
              </td>
            </tr>
            <tr>
              <td style=""padding:14px 22px 22px 22px;font-size:13px;line-height:1.5;color:#475569;"">
                Merci d'utiliser CarHub Madagascar.<br/>
                Si vous avez une question, répondez simplement à cet email.
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
