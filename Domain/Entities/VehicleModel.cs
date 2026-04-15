namespace CarHub.Api.Domain.Entities;

public sealed class VehicleModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BrandId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public Brand? Brand { get; set; }
    public ICollection<Listing> Listings { get; set; } = new List<Listing>();
}
