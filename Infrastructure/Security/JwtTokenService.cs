using CarHub.Api.Domain.Entities;
using CarHub.Api.Infrastructure.Config;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace CarHub.Api.Infrastructure.Security;

public sealed class JwtTokenService(IOptions<JwtOptions> options) : IJwtTokenService
{
    private readonly JwtOptions _options = options.Value;

    public string GenerateAccessToken(User user)
    {
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.Name, user.FullName),
            new("fullName", user.FullName),
            new(ClaimTypes.MobilePhone, user.PhoneNumber),
            new("phoneNumber", user.PhoneNumber),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("accountType", user.AccountType.ToString())
        };

        if (!string.IsNullOrWhiteSpace(user.WhatsAppNumber))
        {
            claims.Add(new Claim("whatsAppNumber", user.WhatsAppNumber));
        }

        if (!string.IsNullOrWhiteSpace(user.ProfileImageUrl))
        {
            claims.Add(new Claim("profileImageUrl", user.ProfileImageUrl));
        }

        var expires = DateTime.UtcNow.AddMinutes(_options.ExpiryMinutes);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public (string Token, DateTime ExpiresAtUtc) GenerateRefreshToken(int validDays)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var expiresAt = DateTime.UtcNow.AddDays(validDays);

        return (token, expiresAt);
    }
}
