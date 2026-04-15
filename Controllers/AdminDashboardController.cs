using CarHub.Api.Application.Contracts.Common;
using CarHub.Api.Domain.Enums;
using CarHub.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarHub.Api.Controllers;

[ApiController]
[Authorize(Policy = "AdminPolicy")]
[Route("api/admin")]
public sealed class AdminDashboardController(AppDbContext dbContext) : AdminControllerBase
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        var pending = await dbContext.Listings.AsNoTracking().CountAsync(x => x.Status == ListingStatus.PendingReview);
        var published = await dbContext.Listings.AsNoTracking().CountAsync(x => x.Status == ListingStatus.Published);
        var rejected = await dbContext.Listings.AsNoTracking().CountAsync(x => x.Status == ListingStatus.Rejected);
        var sold = await dbContext.Listings.AsNoTracking().CountAsync(x => x.Status == ListingStatus.Sold);

        return OkResponse(new
        {
            pending,
            published,
            rejected,
            sold
        }, "Dashboard KPIs loaded.");
    }

    [HttpGet("logs")]
    public async Task<IActionResult> Logs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 200);

        var query = dbContext.AdminAuditLogs
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt);

        var total = await query.CountAsync();
        var items = await query
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(x => new
            {
                x.Id,
                x.AdminUserId,
                x.AdminEmail,
                x.Action,
                x.EntityType,
                x.EntityId,
                x.DetailsJson,
                x.CreatedAt
            })
            .ToListAsync();

        return OkResponse(new PagedResult<object>
        {
            Page = safePage,
            PageSize = safePageSize,
            Total = total,
            Items = items.Cast<object>().ToList()
        }, "Admin logs loaded.");
    }
}


