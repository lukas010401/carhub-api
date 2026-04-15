using CarHub.Api.Domain.Enums;

namespace CarHub.Api.Domain.Entities;

public sealed class Listing : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SellerId { get; set; }
    public Guid BrandId { get; set; }
    public Guid ModelId { get; set; }
    public int Year { get; set; }
    public decimal Price { get; set; }
    public int Mileage { get; set; }
    public FuelType FuelType { get; set; }
    public TransmissionType TransmissionType { get; set; }
    public Guid CityId { get; set; }
    public Guid CategoryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? EngineSize { get; set; }
    public string? Color { get; set; }
    public short? Doors { get; set; }
    public string? Condition { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string? WhatsAppNumber { get; set; }
    public ListingStatus Status { get; set; } = ListingStatus.Draft;
    public string? RejectionReason { get; set; }
    public DateTime? PublishedAt { get; set; }

    public User? Seller { get; set; }
    public Brand? Brand { get; set; }
    public VehicleModel? Model { get; set; }
    public City? City { get; set; }
    public Category? Category { get; set; }
    public ICollection<ListingImage> Images { get; set; } = new List<ListingImage>();
}
