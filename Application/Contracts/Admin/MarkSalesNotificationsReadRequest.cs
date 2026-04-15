namespace CarHub.Api.Application.Contracts.Admin;

public sealed class MarkSalesNotificationsReadRequest
{
    public List<Guid> ListingIds { get; set; } = new();
}
