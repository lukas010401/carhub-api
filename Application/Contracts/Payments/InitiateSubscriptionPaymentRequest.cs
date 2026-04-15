namespace CarHub.Api.Application.Contracts.Payments;

public sealed class InitiateSubscriptionPaymentRequest
{
    public int Months { get; set; } = 1;
    public decimal MonthlyPrice { get; set; }
}