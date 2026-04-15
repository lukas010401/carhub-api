namespace CarHub.Api.Application.Contracts.Admin;

public sealed class AdminProfessionalSubscriptionQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? Keyword { get; set; }
    public string? Status { get; set; }
}
