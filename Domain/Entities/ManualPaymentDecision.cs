namespace CarHub.Api.Domain.Entities;

public sealed class ManualPaymentDecision : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PaymentRequestId { get; set; }
    public ManualPaymentRequest? PaymentRequest { get; set; }

    public Guid AdminUserId { get; set; }
    public User? AdminUser { get; set; }

    public string Action { get; set; } = string.Empty;
    public string? Note { get; set; }
}