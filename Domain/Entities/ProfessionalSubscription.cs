using CarHub.Api.Domain.Enums;

namespace CarHub.Api.Domain.Entities;

public sealed class ProfessionalSubscription : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string PlanCode { get; set; } = "PRO_MONTHLY";
    public decimal MonthlyPrice { get; set; }
    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Pending;
    public string? Notes { get; set; }
    public DateTime? ExpiryReminderEmailSentAtUtc { get; set; }
    public DateTime? ExpiryReminderSmsSentAtUtc { get; set; }

    public User? User { get; set; }
}
