namespace CarHub.Api.Domain.Entities;

public sealed class AdminNotificationRead : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AdminUserId { get; set; }
    public Guid ListingId { get; set; }
    public DateTime ReadAtUtc { get; set; } = DateTime.UtcNow;

    public User? AdminUser { get; set; }
    public Listing? Listing { get; set; }
}
