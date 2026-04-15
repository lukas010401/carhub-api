using CarHub.Api.Application.Contracts.Auth;
using CarHub.Api.Domain.Entities;
using CarHub.Api.Domain.Enums;
using CarHub.Api.Infrastructure.Config;
using CarHub.Api.Infrastructure.Email;
using CarHub.Api.Infrastructure.Persistence;
using CarHub.Api.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Security.Cryptography;

namespace CarHub.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    AppDbContext dbContext,
    IPasswordService passwordService,
    IJwtTokenService tokenService,
    IEmailSender emailSender,
    IOptions<JwtOptions> jwtOptions,
    IOptions<EmailNotificationOptions> emailOptions,
    IWebHostEnvironment environment,
    ILogger<AuthController> logger) : ControllerBase
{
    [HttpPost("register-seller")]
    public async Task<IActionResult> RegisterSeller([FromBody] RegisterSellerRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password) || string.IsNullOrWhiteSpace(request.FullName))
        {
            return BadRequest("Email, password and fullName are required.");
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var exists = await dbContext.Users.AnyAsync(x => x.Email == normalizedEmail, cancellationToken);
        if (exists)
        {
            return Conflict("Email already registered.");
        }

        var accountType = ParseAccountType(request.AccountType);

        var confirmationToken = GenerateEmailConfirmationToken();

        var user = new User
        {
            Email = normalizedEmail,
            PasswordHash = passwordService.Hash(request.Password),
            FullName = request.FullName.Trim(),
            PhoneNumber = request.PhoneNumber.Trim(),
            WhatsAppNumber = request.WhatsAppNumber?.Trim(),
            Role = UserRole.Seller,
            AccountType = accountType,
            IsActive = true,
            IsEmailConfirmed = false,
            EmailConfirmationToken = confirmationToken,
            EmailConfirmationTokenExpiresAt = DateTime.UtcNow.AddHours(24)
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (accountType == AccountType.Professional)
        {
            dbContext.CompanyProfiles.Add(new CompanyProfile
            {
                UserId = user.Id,
                CompanyName = string.IsNullOrWhiteSpace(request.CompanyName) ? user.FullName : request.CompanyName.Trim(),
                RegistrationNumber = request.CompanyRegistrationNumber?.Trim(),
                TaxNumber = request.CompanyTaxNumber?.Trim(),
                Address = request.CompanyAddress?.Trim(),
                ContactName = request.CompanyContactName?.Trim(),
                IsVerified = false
            });

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var sent = await TrySendConfirmationEmailAsync(user, confirmationToken, cancellationToken);

        return Ok(new
        {
            requiresEmailConfirmation = true,
            email = user.Email,
            accountType = user.AccountType.ToString(),
            emailSent = sent,
            message = sent
                ? "Compte cree. Email de confirmation envoye."
                : "Compte cree, mais l'email de confirmation n'a pas pu etre envoye."
        });
    }

    [HttpPost("resend-confirmation")]
    public async Task<IActionResult> ResendConfirmation([FromBody] ResendConfirmationRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest("Email is required.");
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await dbContext.Users.SingleOrDefaultAsync(x => x.Email == normalizedEmail && x.IsActive, cancellationToken);

        if (user is null || user.IsEmailConfirmed)
        {
            return Ok(new { sent = true, message = "Si le compte existe, un email de confirmation a ete envoye." });
        }


        var confirmationToken = GenerateEmailConfirmationToken();
        user.EmailConfirmationToken = confirmationToken;
        user.EmailConfirmationTokenExpiresAt = DateTime.UtcNow.AddHours(24);
        await dbContext.SaveChangesAsync(cancellationToken);

        var sent = await TrySendConfirmationEmailAsync(user, confirmationToken, cancellationToken);
        return Ok(new
        {
            sent,
            message = sent
                ? "Email de confirmation renvoye."
                : "Impossible d'envoyer l'email de confirmation pour le moment."
        });
    }

    [HttpGet("confirm-email")]
    public async Task<IActionResult> ConfirmEmail([FromQuery] string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return BadRequest("Confirmation token is required.");
        }

        var now = DateTime.UtcNow;
        var user = await dbContext.Users.SingleOrDefaultAsync(x =>
            x.EmailConfirmationToken == token
            && x.EmailConfirmationTokenExpiresAt != null
            && x.EmailConfirmationTokenExpiresAt > now,
            cancellationToken);

        if (user is null)
        {
            return BadRequest("Invalid or expired confirmation token.");
        }

        user.IsEmailConfirmed = true;
        user.EmailConfirmationToken = null;
        user.EmailConfirmationTokenExpiresAt = null;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            confirmed = true,
            email = user.Email,
            accountType = user.AccountType.ToString(),
            message = "Email confirmed successfully."
        });
    }

    [HttpPost("login")]
    public async Task<ActionResult<TokenResponse>> Login([FromBody] LoginRequest request)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await dbContext.Users.SingleOrDefaultAsync(x => x.Email == normalizedEmail);
        if (user is null || !user.IsActive || !passwordService.Verify(request.Password, user.PasswordHash))
        {
            return Unauthorized("Invalid credentials.");
        }

        if (!user.IsEmailConfirmed)
        {
            return Unauthorized("Email not confirmed.");
        }

        return await IssueTokensAsync(user);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(sub, out var userId))
        {
            return Unauthorized();
        }

        var user = await dbContext.Users
            .AsNoTracking()
            .Where(x => x.Id == userId && x.IsActive)
            .Select(x => new
            {
                x.Id,
                x.Email,
                x.FullName,
                x.PhoneNumber,
                x.WhatsAppNumber,
                x.ProfileImageUrl,
                AccountType = x.AccountType.ToString(),
                Role = x.Role.ToString()
            })
            .SingleOrDefaultAsync();

        return user is null ? NotFound() : Ok(user);
    }

    [Authorize]
    [HttpPost("profile-image")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(5_242_880)]
    public async Task<IActionResult> UploadProfileImage(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest("Profile image file is required.");
        }

        var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant() ?? string.Empty;
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };
        if (!allowed.Contains(ext))
        {
            return BadRequest("Unsupported image format.");
        }

        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(sub, out var userId))
        {
            return Unauthorized();
        }

        var user = await dbContext.Users.SingleOrDefaultAsync(x => x.Id == userId && x.IsActive, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        var folder = Path.Combine(environment.ContentRootPath, "wwwroot", "uploads", "profiles");
        Directory.CreateDirectory(folder);

        var fileName = $"{userId:N}_{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(folder, fileName);
        await using (var stream = System.IO.File.Create(fullPath))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(user.ProfileImageUrl))
        {
            try
            {
                var oldFileName = Path.GetFileName(user.ProfileImageUrl);
                if (!string.IsNullOrWhiteSpace(oldFileName))
                {
                    var oldPath = Path.Combine(folder, oldFileName);
                    if (System.IO.File.Exists(oldPath))
                    {
                        System.IO.File.Delete(oldPath);
                    }
                }
            }
            catch
            {
                // Non bloquant: ne pas echouer l'upload si suppression ancien fichier impossible.
            }
        }

        user.ProfileImageUrl = $"/uploads/profiles/{fileName}";
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new { profileImageUrl = user.ProfileImageUrl });
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<TokenResponse>> Refresh([FromBody] RefreshRequest request)
    {
        var existing = await dbContext.RefreshTokens
            .Include(x => x.User)
            .SingleOrDefaultAsync(x => x.Token == request.RefreshToken);

        if (existing is null || !existing.IsActive || existing.User is null || !existing.User.IsActive || !existing.User.IsEmailConfirmed)
        {
            return Unauthorized("Invalid refresh token.");
        }

        existing.RevokedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        return await IssueTokensAsync(existing.User);
    }

    private async Task<bool> TrySendConfirmationEmailAsync(User user, string token, CancellationToken cancellationToken)
    {
        try
        {
            var link = BuildConfirmationLink(token);
            var subject = "Confirmez votre email - CarHub Madagascar";
            var safeName = System.Net.WebUtility.HtmlEncode(user.FullName);

            var body = $@"<!doctype html>
<html lang=""fr"">
  <body style=""margin:0;padding:0;background:#f3f6fb;font-family:Segoe UI,Arial,sans-serif;color:#0f172a;"">
    <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" style=""padding:24px 12px;"">
      <tr>
        <td align=""center"">
          <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" style=""max-width:640px;background:#ffffff;border:1px solid #dbe4f0;border-radius:14px;overflow:hidden;"">
            <tr>
              <td style=""padding:16px 22px;background:linear-gradient(90deg,#1d4ed8 0%,#2563eb 100%);color:#ffffff;"">
                <div style=""font-size:12px;letter-spacing:.08em;text-transform:uppercase;opacity:.92;font-weight:700;"">CarHub Madagascar</div>
                <div style=""margin-top:6px;font-size:22px;font-weight:800;line-height:1.2;"">Confirmation d'email</div>
              </td>
            </tr>
            <tr>
              <td style=""padding:20px 22px 10px 22px;font-size:15px;line-height:1.55;color:#1f3558;"">
                Bonjour <strong>{safeName}</strong>,<br/><br/>
                Merci pour votre inscription sur CarHub Madagascar.
                Cliquez sur le bouton ci-dessous pour confirmer votre email.
              </td>
            </tr>
            <tr>
              <td style=""padding:0 22px 18px 22px;"">
                <a href=""{link}"" style=""display:inline-block;background:#1d4ed8;color:#ffffff;text-decoration:none;font-weight:700;border-radius:10px;padding:11px 16px;"">Confirmer mon email</a>
              </td>
            </tr>
            <tr>
              <td style=""padding:0 22px 14px 22px;font-size:13px;line-height:1.5;color:#64748b;"">
                Ce lien expire dans 24 heures.
              </td>
            </tr>
            <tr>
              <td style=""padding:0 22px 22px 22px;font-size:12px;line-height:1.5;color:#94a3b8;"">
                Si le bouton ne fonctionne pas, copiez ce lien dans votre navigateur:<br/>
                <span style=""word-break:break-all;color:#1e3a8a;"">{link}</span>
              </td>
            </tr>
          </table>
        </td>
      </tr>
    </table>
  </body>
</html>";

            await emailSender.SendAsync(user.Email, subject, body, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send confirmation email to {Email}", user.Email);
            return false;
        }
    }

    private string BuildConfirmationLink(string token)
    {
        var baseUrl = emailOptions.Value.FrontendBaseUrl?.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = "https://localhost:3000";
        }

        return $"{baseUrl.TrimEnd('/')}/confirm-email?token={Uri.EscapeDataString(token)}";
    }


    private static AccountType ParseAccountType(string? raw)
    {
        var value = (raw ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(value)) return AccountType.Individual;

        if (string.Equals(value, "Professional", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "Pro", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "Professionnel", StringComparison.OrdinalIgnoreCase))
        {
            return AccountType.Professional;
        }

        if (string.Equals(value, "Individual", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "Particulier", StringComparison.OrdinalIgnoreCase))
        {
            return AccountType.Individual;
        }

        return AccountType.Individual;
    }
    private static string GenerateEmailConfirmationToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    private async Task<TokenResponse> IssueTokensAsync(User user)
    {
        var accessToken = tokenService.GenerateAccessToken(user);
        var refresh = tokenService.GenerateRefreshToken(jwtOptions.Value.RefreshTokenDays);

        dbContext.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = refresh.Token,
            ExpiresAt = refresh.ExpiresAtUtc
        });
        await dbContext.SaveChangesAsync();

        return new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = refresh.Token,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(jwtOptions.Value.ExpiryMinutes)
        };
    }
}







