namespace CarHub.Api.Infrastructure.Notifications;

public interface ISmsSender
{
    Task SendAsync(string toNumber, string message, CancellationToken cancellationToken = default);
}
