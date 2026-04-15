using CarHub.Api.Domain.Enums;

namespace CarHub.Api.Domain.Entities;

public sealed class ManualPaymentRequest : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public Guid? ListingId { get; set; }
    public Listing? Listing { get; set; }

    public ManualPaymentType Type { get; set; }
    public ManualPaymentStatus Status { get; set; } = ManualPaymentStatus.Initiated;

    public MobileMoneyProvider? Provider { get; set; }
    public string? ReceiverNumber { get; set; }
    public string InternalReference { get; set; } = string.Empty;
    public decimal ExpectedAmount { get; set; }

    public int? RequestedMonths { get; set; }
    public decimal? RequestedMonthlyPrice { get; set; }

    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public Guid? ReviewedByAdminId { get; set; }

    public string? ProviderTransactionReference { get; set; }
    public string? SenderNumber { get; set; }
    public string? SenderName { get; set; }
    public DateTime? PaidAtLocal { get; set; }
    public string? Notes { get; set; }

    public string? ProofFileUrl { get; set; }
    public string? ProofFileHash { get; set; }

    public string? ReviewNote { get; set; }

    public ICollection<ManualPaymentDecision> Decisions { get; set; } = new List<ManualPaymentDecision>();
}