using CarHub.Api.Domain.Enums;

namespace CarHub.Api.Application.Contracts.Listings;

public sealed class ListingUpsertRequest
{
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
}
