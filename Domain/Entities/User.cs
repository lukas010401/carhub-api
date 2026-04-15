using CarHub.Api.Domain.Enums;

namespace CarHub.Api.Domain.Entities;

public sealed class User : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? WhatsAppNumber { get; set; }
    public string? ProfileImageUrl { get; set; }
    public UserRole Role { get; set; } = UserRole.Seller;
    public AccountType AccountType { get; set; } = AccountType.Individual;
    public bool IsActive { get; set; } = true;
    public bool IsEmailConfirmed { get; set; } = true;
    public string? EmailConfirmationToken { get; set; }
    public DateTime? EmailConfirmationTokenExpiresAt { get; set; }

    public ICollection<Listing> Listings { get; set; } = new List<Listing>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<ProfessionalSubscription> Subscriptions { get; set; } = new List<ProfessionalSubscription>();
    public CompanyProfile? CompanyProfile { get; set; }
}
