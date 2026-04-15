namespace CarHub.Api.Domain.Entities;

public sealed class Brand
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<VehicleModel> Models { get; set; } = new List<VehicleModel>();
    public ICollection<Listing> Listings { get; set; } = new List<Listing>();
}
