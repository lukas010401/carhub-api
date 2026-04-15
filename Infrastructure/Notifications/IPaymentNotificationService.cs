using CarHub.Api.Domain.Entities;

namespace CarHub.Api.Infrastructure.Notifications;

public interface IPaymentNotificationService
{
    Task NotifyPaymentReviewedAsync(ManualPaymentRequest payment, User seller, bool approved, CancellationToken cancellationToken = default);
}