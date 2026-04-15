namespace CarHub.Api.Application.Contracts.Auth;

public sealed class RefreshRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}
