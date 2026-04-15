using CarHub.Api.Domain.Enums;
using CarHub.Api.Infrastructure.Config;
using CarHub.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace CarHub.Api.Controllers;

[ApiController]
[Authorize(Policy = "SellerOrAdminPolicy")]
[Route("api/seller/subscription")]
public sealed class SellerSubscriptionController(AppDbContext dbContext, IOptions<ManualPaymentOptions> paymentOptions) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetMine()
    {
        var userId = GetUserId();

        var user = await dbContext.Users
            .AsNoTracking()
            .Include(x => x.CompanyProfile)
            .SingleOrDefaultAsync(x => x.Id == userId && x.IsActive);

        if (user is null)
        {
            return NotFound();
        }

        var latest = await dbContext.ProfessionalSubscriptions
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.EndsAtUtc)
            .ThenByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                Status = x.Status.ToString(),
                x.PlanCode,
                x.MonthlyPrice,
                x.StartsAtUtc,
                x.EndsAtUtc,
                x.Notes
            })
            .FirstOrDefaultAsync();

        var now = DateTime.UtcNow;
        var hasActiveSubscription = await dbContext.ProfessionalSubscriptions
            .AsNoTracking()
            .AnyAsync(x => x.UserId == userId
                && x.Status == SubscriptionStatus.Active
                && x.StartsAtUtc <= now
                && x.EndsAtUtc >= now);

        var isProfessional = user.AccountType == AccountType.Professional;
        var professionalMonthlyFee = paymentOptions.Value.ProfessionalMonthlyFee < 0
            ? 0
            : paymentOptions.Value.ProfessionalMonthlyFee;

        return Ok(new
        {
            accountType = user.AccountType.ToString(),
            isProfessional,
            companyName = user.CompanyProfile?.CompanyName,
            companyVerified = user.CompanyProfile?.IsVerified ?? false,
            latestSubscription = latest,
            hasActiveSubscription,
            canPublish = !isProfessional || hasActiveSubscription,
            professionalMonthlyFee,
            message = !isProfessional
                ? "Compte particulier: 20 000 Ar par annonce mise en ligne."
                : hasActiveSubscription
                    ? "Abonnement professionnel actif."
                    : "Aucun abonnement professionnel actif."
        });
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.Parse(sub!);
    }
}
