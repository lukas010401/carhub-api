using CarHub.Api.Domain.Entities;

namespace CarHub.Api.Infrastructure.Security;

public interface IJwtTokenService
{
    string GenerateAccessToken(User user);
    (string Token, DateTime ExpiresAtUtc) GenerateRefreshToken(int validDays);
}
