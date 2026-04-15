namespace CarHub.Api.Infrastructure.Notifications;

public interface IWhatsAppSender
{
    Task SendAsync(string toNumber, string message, CancellationToken cancellationToken = default);
}