using CarHub.Api.Domain.Enums;

namespace CarHub.Api.Application.Contracts.Listings;

public sealed class PublicListingQuery
{
    public Guid? BrandId { get; set; }
    public Guid? ModelId { get; set; }
    public Guid? CityId { get; set; }
    public Guid? CategoryId { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public int? MinYear { get; set; }
    public int? MaxYear { get; set; }
    public int? MaxMileage { get; set; }
    public FuelType? FuelType { get; set; }
    public TransmissionType? TransmissionType { get; set; }
    public string? Keyword { get; set; }
    public string SortBy { get; set; } = "recent";
    public bool ProsFirst { get; set; } = false;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
