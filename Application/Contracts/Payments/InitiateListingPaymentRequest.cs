namespace CarHub.Api.Application.Contracts.Payments;

public sealed class InitiateListingPaymentRequest
{
    public Guid ListingId { get; set; }
}