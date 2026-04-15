using Microsoft.AspNetCore.Http;
using CarHub.Api.Domain.Enums;

namespace CarHub.Api.Application.Contracts.Payments;

public sealed class SubmitManualPaymentProofRequest
{
    public MobileMoneyProvider Provider { get; set; }
    public string ProviderTransactionReference { get; set; } = string.Empty;
    public string SenderNumber { get; set; } = string.Empty;
    public string? SenderName { get; set; }
    public DateTime PaidAtLocal { get; set; }
    public string? Notes { get; set; }
    public IFormFile? ProofFile { get; set; }
}