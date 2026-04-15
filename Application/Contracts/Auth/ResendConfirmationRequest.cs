namespace CarHub.Api.Application.Contracts.Auth;

public sealed class ResendConfirmationRequest
{
    public string Email { get; set; } = string.Empty;
}
