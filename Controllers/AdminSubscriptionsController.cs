using CarHub.Api.Application.Contracts.Admin;
using CarHub.Api.Application.Contracts.Common;
using CarHub.Api.Domain.Entities;
using CarHub.Api.Domain.Enums;
using CarHub.Api.Infrastructure.Audit;
using CarHub.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarHub.Api.Controllers;

[ApiController]
[Authorize(Policy = "AdminPolicy")]
[Route("api/admin/subscriptions")]
public sealed class AdminSubscriptionsController(
    AppDbContext dbContext,
    IAdminAuditService auditService) : AdminControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] AdminProfessionalSubscriptionQuery query)
    {
        var safePage = Math.Max(1, query.Page);
        var safePageSize = Math.Clamp(query.PageSize, 1, 100);

        var usersQuery = dbContext.Users
            .AsNoTracking()
            .Include(x => x.CompanyProfile)
            .Where(x => x.Role == UserRole.Seller && x.AccountType == AccountType.Professional)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword.Trim().ToLowerInvariant();
            usersQuery = usersQuery.Where(x =>
                x.Email.ToLower().Contains(kw)
                || x.FullName.ToLower().Contains(kw)
                || (x.CompanyProfile != null && x.CompanyProfile.CompanyName.ToLower().Contains(kw)));
        }

        var total = await usersQuery.CountAsync();

        var users = await usersQuery
            .OrderByDescending(x => x.CreatedAt)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(x => new
            {
                x.Id,
                x.FullName,
                x.Email,
                x.PhoneNumber,
                CompanyName = x.CompanyProfile != null ? x.CompanyProfile.CompanyName : null,
                CompanyVerified = x.CompanyProfile != null && x.CompanyProfile.IsVerified
            })
            .ToListAsync();

        var userIds = users.Select(x => x.Id).ToList();
        var latestByUser = userIds.Count == 0
            ? new Dictionary<Guid, object?>()
            : await dbContext.ProfessionalSubscriptions
                .AsNoTracking()
                .Where(x => userIds.Contains(x.UserId))
                .OrderByDescending(x => x.EndsAtUtc)
                .ThenByDescending(x => x.CreatedAt)
                .GroupBy(x => x.UserId)
                .Select(g => new
                {
                    UserId = g.Key,
                    Latest = g.Select(x => new
                    {
                        x.Id,
                        Status = x.Status.ToString(),
                        x.PlanCode,
                        x.MonthlyPrice,
                        x.StartsAtUtc,
                        x.EndsAtUtc,
                        x.Notes
                    }).FirstOrDefault()
                })
                .ToDictionaryAsync(x => x.UserId, x => (object?)x.Latest);

        var now = DateTime.UtcNow;
        var items = users.Select(u =>
        {
            latestByUser.TryGetValue(u.Id, out var latestObj);
            return new
            {
                u.Id,
                u.FullName,
                u.Email,
                u.PhoneNumber,
                u.CompanyName,
                u.CompanyVerified,
                LatestSubscription = latestObj,
                HasActiveSubscription = dbContext.ProfessionalSubscriptions.AsNoTracking().Any(s =>
                    s.UserId == u.Id
                    && s.Status == SubscriptionStatus.Active
                    && s.StartsAtUtc <= now
                    && s.EndsAtUtc >= now)
            };
        }).ToList();

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim().ToLowerInvariant();
            items = items.Where(x =>
            {
                var latest = x.LatestSubscription;
                if (latest is null) return status == "none";
                var latestStatus = (string?)latest.GetType().GetProperty("Status")?.GetValue(latest);
                return string.Equals(latestStatus, query.Status, StringComparison.OrdinalIgnoreCase);
            }).ToList();
        }

        return OkResponse(new PagedResult<object>
        {
            Page = safePage,
            PageSize = safePageSize,
            Total = total,
            Items = items.Cast<object>().ToList()
        }, "Professional subscriptions loaded.");
    }

    [HttpPost("{userId:guid}/activate")]
    public async Task<IActionResult> Activate(Guid userId, [FromBody] ManageProfessionalSubscriptionRequest request)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(x => x.Id == userId);
        if (user is null) return NotFoundResponse("User not found.");
        if (user.Role != UserRole.Seller || user.AccountType != AccountType.Professional)
        {
            return BadRequestResponse("User is not a professional seller.");
        }

        var months = Math.Clamp(request.Months <= 0 ? 1 : request.Months, 1, 24);
        var price = request.MonthlyPrice < 0 ? 0 : request.MonthlyPrice;
        var now = DateTime.UtcNow;

        var activeSubs = await dbContext.ProfessionalSubscriptions
            .Where(x => x.UserId == userId && x.Status == SubscriptionStatus.Active)
            .ToListAsync();

        foreach (var sub in activeSubs)
        {
            sub.Status = SubscriptionStatus.Cancelled;
            sub.EndsAtUtc = now;
        }

        var created = new ProfessionalSubscription
        {
            UserId = userId,
            PlanCode = "PRO_MONTHLY",
            MonthlyPrice = price,
            StartsAtUtc = now,
            EndsAtUtc = now.AddMonths(months),
            Status = SubscriptionStatus.Active,
            Notes = request.Notes?.Trim()
        };

        dbContext.ProfessionalSubscriptions.Add(created);
        await dbContext.SaveChangesAsync();

        var admin = GetAdminIdentity();
        await auditService.LogAsync(admin.UserId, admin.Email, "subscription.pro.activated", "user", userId,
            new { created.Id, created.MonthlyPrice, created.StartsAtUtc, created.EndsAtUtc, months });

        return OkResponse(new
        {
            created.Id,
            Status = created.Status.ToString(),
            created.MonthlyPrice,
            created.StartsAtUtc,
            created.EndsAtUtc
        }, "Professional subscription activated.");
    }

    [HttpPost("{userId:guid}/suspend")]
    public async Task<IActionResult> Suspend(Guid userId, [FromBody] ManageProfessionalSubscriptionRequest request)
    {
        var active = await dbContext.ProfessionalSubscriptions
            .Where(x => x.UserId == userId && x.Status == SubscriptionStatus.Active)
            .OrderByDescending(x => x.EndsAtUtc)
            .FirstOrDefaultAsync();

        if (active is null)
        {
            return BadRequestResponse("No active subscription found.");
        }

        active.Status = SubscriptionStatus.Cancelled;
        active.EndsAtUtc = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            active.Notes = string.IsNullOrWhiteSpace(active.Notes)
                ? request.Notes.Trim()
                : $"{active.Notes} | {request.Notes.Trim()}";
        }

        await dbContext.SaveChangesAsync();

        var admin = GetAdminIdentity();
        await auditService.LogAsync(admin.UserId, admin.Email, "subscription.pro.suspended", "subscription", active.Id,
            new { active.UserId, active.EndsAtUtc, active.Notes });

        return OkResponse(new { active.Id, Status = active.Status.ToString(), active.EndsAtUtc }, "Professional subscription suspended.");
    }

    [HttpPost("{userId:guid}/renew")]
    public async Task<IActionResult> Renew(Guid userId, [FromBody] ManageProfessionalSubscriptionRequest request)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(x => x.Id == userId);
        if (user is null) return NotFoundResponse("User not found.");

        var months = Math.Clamp(request.Months <= 0 ? 1 : request.Months, 1, 24);
        var now = DateTime.UtcNow;

        var latest = await dbContext.ProfessionalSubscriptions
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.EndsAtUtc)
            .ThenByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();

        var startsAt = latest is not null && latest.EndsAtUtc > now ? latest.EndsAtUtc : now;
        var price = request.MonthlyPrice > 0
            ? request.MonthlyPrice
            : latest?.MonthlyPrice ?? 0m;

        var created = new ProfessionalSubscription
        {
            UserId = userId,
            PlanCode = latest?.PlanCode ?? "PRO_MONTHLY",
            MonthlyPrice = price,
            StartsAtUtc = startsAt,
            EndsAtUtc = startsAt.AddMonths(months),
            Status = SubscriptionStatus.Active,
            Notes = request.Notes?.Trim()
        };

        dbContext.ProfessionalSubscriptions.Add(created);
        await dbContext.SaveChangesAsync();

        var admin = GetAdminIdentity();
        await auditService.LogAsync(admin.UserId, admin.Email, "subscription.pro.renewed", "user", userId,
            new { created.Id, created.MonthlyPrice, created.StartsAtUtc, created.EndsAtUtc, months });

        return OkResponse(new
        {
            created.Id,
            Status = created.Status.ToString(),
            created.MonthlyPrice,
            created.StartsAtUtc,
            created.EndsAtUtc
        }, "Professional subscription renewed.");
    }
}
