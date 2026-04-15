namespace CarHub.Api.Application.Contracts.Auth;

public sealed class RegisterSellerRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? WhatsAppNumber { get; set; }
    public string AccountType { get; set; } = "Individual";

    // Company fields are already captured for future pro onboarding workflows.
    public string? CompanyName { get; set; }
    public string? CompanyRegistrationNumber { get; set; }
    public string? CompanyTaxNumber { get; set; }
    public string? CompanyAddress { get; set; }
    public string? CompanyContactName { get; set; }
}
