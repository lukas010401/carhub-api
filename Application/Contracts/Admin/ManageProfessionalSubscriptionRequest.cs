namespace CarHub.Api.Application.Contracts.Admin;

public sealed class ManageProfessionalSubscriptionRequest
{
    public int Months { get; set; } = 1;
    public decimal MonthlyPrice { get; set; }
    public string? Notes { get; set; }
}
