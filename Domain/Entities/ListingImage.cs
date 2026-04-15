using System.Text.Json.Serialization;

namespace CarHub.Api.Domain.Entities;

public sealed class ListingImage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ListingId { get; set; }
    public string Url { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [JsonIgnore]
    public Listing? Listing { get; set; }
}

