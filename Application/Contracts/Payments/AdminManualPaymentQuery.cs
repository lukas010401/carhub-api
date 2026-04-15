namespace CarHub.Api.Application.Contracts.Payments;

public sealed class AdminManualPaymentQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Status { get; set; }
    public string? Keyword { get; set; }
    public string? Type { get; set; }
}