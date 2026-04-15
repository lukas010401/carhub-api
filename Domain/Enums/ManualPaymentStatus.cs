namespace CarHub.Api.Domain.Enums;

public enum ManualPaymentStatus
{
    Initiated = 1,
    ProofSubmitted = 2,
    UnderReview = 3,
    Approved = 4,
    Rejected = 5,
    Expired = 6,
    Cancelled = 7
}