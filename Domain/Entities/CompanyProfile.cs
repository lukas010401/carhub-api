namespace CarHub.Api.Domain.Entities;

public sealed class CompanyProfile : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? RegistrationNumber { get; set; }
    public string? TaxNumber { get; set; }
    public string? Address { get; set; }
    public string? ContactName { get; set; }
    public bool IsVerified { get; set; }

    public User? User { get; set; }
}
