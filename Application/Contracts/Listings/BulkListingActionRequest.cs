namespace CarHub.Api.Application.Contracts.Listings;

public sealed class BulkListingActionRequest
{
    public List<Guid> ListingIds { get; set; } = new();
    public string Action { get; set; } = string.Empty;
}
