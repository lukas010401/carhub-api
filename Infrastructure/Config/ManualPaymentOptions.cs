namespace CarHub.Api.Infrastructure.Config;

public sealed class ManualPaymentOptions
{
    public const string SectionName = "ManualPayments";

    public decimal IndividualListingFee { get; set; } = 20000m;
    public decimal ProfessionalMonthlyFee { get; set; } = 150000m;
    public int RequestExpiryMinutes { get; set; } = 30;

    public string YasReceiverNumber { get; set; } = string.Empty;
    public string OrangeReceiverNumber { get; set; } = string.Empty;
    public string AirtelReceiverNumber { get; set; } = string.Empty;
}
