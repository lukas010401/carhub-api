using CarHub.Api.Application.Contracts.Admin;
using CarHub.Api.Application.Contracts.Common;
using CarHub.Api.Application.Contracts.Listings;
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
[Route("api/admin/listings")]
public sealed class AdminListingsController(
    AppDbContext dbContext,
    IAdminAuditService auditService) : AdminControllerBase
{
    [HttpGet("pending")]
    public async Task<IActionResult> GetPending(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 100);

        var baseQuery = dbContext.Listings
            .AsNoTracking()
            .Include(x => x.Seller)
            .Include(x => x.Brand)
            .Include(x => x.Model)
            .Where(x => x.Status == ListingStatus.PendingReview)
            .OrderBy(x => x.CreatedAt);

        var total = await baseQuery.CountAsync();
        var items = await baseQuery
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(x => new
            {
                x.Id,
                x.Title,
                x.Price,
                x.Year,
                x.Mileage,
                SellerId = x.SellerId,
                SellerEmail = x.Seller!.Email,
                SellerName = x.Seller.FullName,
                Brand = x.Brand!.Name,
                Model = x.Model!.Name,
                Status = x.Status.ToString(),
                CoverImage = x.Images.OrderBy(i => i.DisplayOrder).Select(i => i.Url).FirstOrDefault(),
                x.CreatedAt
            })
            .ToListAsync();

        return OkResponse(new PagedResult<object>
        {
            Page = safePage,
            PageSize = safePageSize,
            Total = total,
            Items = items.Cast<object>().ToList()
        }, "Pending listings loaded.");
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] AdminListingQuery query)
    {
        var safePage = Math.Max(1, query.Page);
        var safePageSize = Math.Clamp(query.PageSize, 1, 100);

        var baseQuery = dbContext.Listings
            .AsNoTracking()
            .Include(x => x.Seller)
            .Include(x => x.Brand)
            .Include(x => x.Model)
            .Include(x => x.City)
            .Include(x => x.Category)
            .AsQueryable();

        if (query.SellerId is not null)
        {
            baseQuery = baseQuery.Where(x => x.SellerId == query.SellerId.Value);
        }

        if (query.BrandId is not null)
        {
            baseQuery = baseQuery.Where(x => x.BrandId == query.BrandId.Value);
        }

        if (query.ModelId is not null)
        {
            baseQuery = baseQuery.Where(x => x.ModelId == query.ModelId.Value);
        }

        if (query.CategoryId is not null)
        {
            baseQuery = baseQuery.Where(x => x.CategoryId == query.CategoryId.Value);
        }

        if (query.CityId is not null)
        {
            baseQuery = baseQuery.Where(x => x.CityId == query.CityId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!Enum.TryParse<ListingStatus>(query.Status.Trim(), true, out var status))
            {
                return BadRequestResponse("Invalid status filter.", "status must be a valid ListingStatus value.");
            }

            baseQuery = baseQuery.Where(x => x.Status == status);
        }

        if (query.DateFromUtc is not null)
        {
            baseQuery = baseQuery.Where(x => x.CreatedAt >= query.DateFromUtc.Value);
        }

        if (query.DateToUtc is not null)
        {
            baseQuery = baseQuery.Where(x => x.CreatedAt <= query.DateToUtc.Value);
        }

        if (query.MinPrice is not null)
        {
            baseQuery = baseQuery.Where(x => x.Price >= query.MinPrice.Value);
        }

        if (query.MaxPrice is not null)
        {
            baseQuery = baseQuery.Where(x => x.Price <= query.MaxPrice.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim().ToLowerInvariant();
            baseQuery = baseQuery.Where(x =>
                x.Title.ToLower().Contains(keyword) ||
                x.Seller!.Email.ToLower().Contains(keyword) ||
                x.Brand!.Name.ToLower().Contains(keyword) ||
                x.Model!.Name.ToLower().Contains(keyword) ||
                x.City!.Name.ToLower().Contains(keyword) ||
                x.Category!.Name.ToLower().Contains(keyword));
        }

        baseQuery = string.Equals(query.Status, "Sold", StringComparison.OrdinalIgnoreCase)
            ? baseQuery.OrderByDescending(x => x.UpdatedAt)
            : baseQuery.OrderByDescending(x => x.CreatedAt);

        var total = await baseQuery.CountAsync();
        var items = await baseQuery
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(x => new
            {
                x.Id,
                x.Title,
                x.Price,
                x.Year,
                x.Mileage,
                Status = x.Status.ToString(),
                x.RejectionReason,
                x.PublishedAt,
                x.CreatedAt,
                SellerId = x.SellerId,
                SellerEmail = x.Seller!.Email,
                SellerName = x.Seller.FullName,
                SellerPhoneNumber = x.Seller.PhoneNumber,
                Brand = x.Brand!.Name,
                Model = x.Model!.Name,
                City = x.City!.Name,
                Category = x.Category!.Name,
                x.UpdatedAt,
                CoverImage = x.Images.OrderBy(i => i.DisplayOrder).Select(i => i.Url).FirstOrDefault()
            })
            .ToListAsync();

        return OkResponse(new PagedResult<object>
        {
            Page = safePage,
            PageSize = safePageSize,
            Total = total,
            Items = items.Cast<object>().ToList()
        }, "Listings loaded.");
    }

    [HttpGet("sales-summary")]
    public async Task<IActionResult> SalesSummary([FromQuery] AdminListingQuery query)
    {
        var baseQuery = dbContext.Listings
            .AsNoTracking()
            .Include(x => x.Seller)
            .Include(x => x.Brand)
            .Include(x => x.Model)
            .Include(x => x.City)
            .Include(x => x.Category)
            .Where(x => x.Status == ListingStatus.Sold)
            .AsQueryable();

        if (query.SellerId is not null)
        {
            baseQuery = baseQuery.Where(x => x.SellerId == query.SellerId.Value);
        }

        if (query.BrandId is not null)
        {
            baseQuery = baseQuery.Where(x => x.BrandId == query.BrandId.Value);
        }

        if (query.ModelId is not null)
        {
            baseQuery = baseQuery.Where(x => x.ModelId == query.ModelId.Value);
        }

        if (query.CategoryId is not null)
        {
            baseQuery = baseQuery.Where(x => x.CategoryId == query.CategoryId.Value);
        }

        if (query.CityId is not null)
        {
            baseQuery = baseQuery.Where(x => x.CityId == query.CityId.Value);
        }

        if (query.DateFromUtc is not null)
        {
            baseQuery = baseQuery.Where(x => x.UpdatedAt >= query.DateFromUtc.Value);
        }

        if (query.DateToUtc is not null)
        {
            baseQuery = baseQuery.Where(x => x.UpdatedAt <= query.DateToUtc.Value);
        }

        if (query.MinPrice is not null)
        {
            baseQuery = baseQuery.Where(x => x.Price >= query.MinPrice.Value);
        }

        if (query.MaxPrice is not null)
        {
            baseQuery = baseQuery.Where(x => x.Price <= query.MaxPrice.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim().ToLowerInvariant();
            baseQuery = baseQuery.Where(x =>
                x.Title.ToLower().Contains(keyword) ||
                x.Seller!.Email.ToLower().Contains(keyword) ||
                x.Brand!.Name.ToLower().Contains(keyword) ||
                x.Model!.Name.ToLower().Contains(keyword) ||
                x.City!.Name.ToLower().Contains(keyword) ||
                x.Category!.Name.ToLower().Contains(keyword));
        }

        var totalSold = await baseQuery.CountAsync();
        var totalAmount = totalSold == 0 ? 0m : await baseQuery.SumAsync(x => x.Price);

        var byBrand = await baseQuery
            .GroupBy(x => new { x.BrandId, BrandName = x.Brand!.Name })
            .Select(g => new
            {
                BrandId = g.Key.BrandId,
                Brand = g.Key.BrandName,
                SoldCount = g.Count(),
                TotalAmount = g.Sum(x => x.Price),
                AveragePrice = g.Average(x => x.Price),
                MinPrice = g.Min(x => x.Price),
                MaxPrice = g.Max(x => x.Price)
            })
            .OrderByDescending(x => x.SoldCount)
            .ThenBy(x => x.Brand)
            .Take(20)
            .ToListAsync();

        return OkResponse(new
        {
            totalSold,
            totalAmount,
            averagePrice = totalSold == 0 ? 0m : Math.Round(totalAmount / totalSold, 2),
            byBrand
        }, "Sales summary loaded.");
    }
    [HttpGet("sales-notifications")]
    public async Task<IActionResult> GetSalesNotifications(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 50);
        var admin = GetAdminIdentity();

        var soldQuery = dbContext.Listings
            .AsNoTracking()
            .Include(x => x.Seller)
            .Include(x => x.Brand)
            .Include(x => x.Model)
            .Where(x => x.Status == ListingStatus.Sold)
            .OrderByDescending(x => x.UpdatedAt)
            .ThenByDescending(x => x.CreatedAt);

        var total = await soldQuery.CountAsync();

        var pageItems = await soldQuery
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(x => new
            {
                x.Id,
                x.Title,
                x.Price,
                x.Year,
                x.Mileage,
                Status = x.Status.ToString(),
                x.PublishedAt,
                x.CreatedAt,
                x.UpdatedAt,
                SellerId = x.SellerId,
                SellerEmail = x.Seller!.Email,
                SellerName = x.Seller.FullName,
                SellerPhoneNumber = x.Seller.PhoneNumber,
                Brand = x.Brand!.Name,
                Model = x.Model!.Name,
                CoverImage = x.Images.OrderBy(i => i.DisplayOrder).Select(i => i.Url).FirstOrDefault()
            })
            .ToListAsync();

        var pageListingIds = pageItems.Select(x => x.Id).Distinct().ToList();
        var readIds = pageListingIds.Count == 0
            ? new HashSet<Guid>()
            : (await dbContext.AdminNotificationReads
                .AsNoTracking()
                .Where(x => x.AdminUserId == admin.UserId && pageListingIds.Contains(x.ListingId))
                .Select(x => x.ListingId)
                .ToListAsync())
                .ToHashSet();

        var unreadCount = await soldQuery
            .CountAsync(x => !dbContext.AdminNotificationReads.Any(r => r.AdminUserId == admin.UserId && r.ListingId == x.Id));

        var items = pageItems
            .Select(x => new
            {
                x.Id,
                x.Title,
                x.Price,
                x.Year,
                x.Mileage,
                x.Status,
                x.PublishedAt,
                x.CreatedAt,
                x.UpdatedAt,
                x.SellerId,
                x.SellerEmail,
                x.SellerName,
                x.SellerPhoneNumber,
                x.Brand,
                x.Model,
                x.CoverImage,
                IsRead = readIds.Contains(x.Id)
            })
            .ToList();

        return OkResponse(new
        {
            page = safePage,
            pageSize = safePageSize,
            total,
            unreadCount,
            items
        }, "Sales notifications loaded.");
    }

    [HttpPost("sales-notifications/read")]
    public async Task<IActionResult> MarkSalesNotificationsRead([FromBody] MarkSalesNotificationsReadRequest request)
    {
        var listingIds = (request.ListingIds ?? new List<Guid>())
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToList();

        if (listingIds.Count == 0)
        {
            return BadRequestResponse("No notifications to mark as read.", "listingIds must contain at least one valid id.");
        }

        var admin = GetAdminIdentity();

        var soldListingIds = await dbContext.Listings
            .AsNoTracking()
            .Where(x => listingIds.Contains(x.Id) && x.Status == ListingStatus.Sold)
            .Select(x => x.Id)
            .ToListAsync();

        if (soldListingIds.Count == 0)
        {
            return OkResponse(new { marked = 0 }, "No sold listings found for provided ids.");
        }

        var existingReadIds = await dbContext.AdminNotificationReads
            .AsNoTracking()
            .Where(x => x.AdminUserId == admin.UserId && soldListingIds.Contains(x.ListingId))
            .Select(x => x.ListingId)
            .ToListAsync();

        var existingSet = existingReadIds.ToHashSet();
        var toInsert = soldListingIds
            .Where(x => !existingSet.Contains(x))
            .Select(listingId => new AdminNotificationRead
            {
                AdminUserId = admin.UserId,
                ListingId = listingId,
                ReadAtUtc = DateTime.UtcNow
            })
            .ToList();

        if (toInsert.Count > 0)
        {
            dbContext.AdminNotificationReads.AddRange(toInsert);
            await dbContext.SaveChangesAsync();
        }

        var unreadCount = await dbContext.Listings
            .AsNoTracking()
            .Where(x => x.Status == ListingStatus.Sold)
            .CountAsync(x => !dbContext.AdminNotificationReads.Any(r => r.AdminUserId == admin.UserId && r.ListingId == x.Id));

        return OkResponse(new { marked = toInsert.Count, unreadCount }, "Notifications marked as read.");
    }
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var listing = await dbContext.Listings
            .AsNoTracking()
            .Include(x => x.Seller)
            .Include(x => x.Brand)
            .Include(x => x.Model)
            .Include(x => x.City)
            .Include(x => x.Category)
            .Include(x => x.Images.OrderBy(i => i.DisplayOrder))
            .SingleOrDefaultAsync(x => x.Id == id);

        if (listing is null)
        {
            return NotFoundResponse("Listing not found.");
        }

        var result = new
        {
            listing.Id,
            listing.Title,
            listing.Description,
            listing.Price,
            listing.Year,
            listing.Mileage,
            FuelType = listing.FuelType.ToString(),
            TransmissionType = listing.TransmissionType.ToString(),
            listing.EngineSize,
            listing.Color,
            listing.Doors,
            listing.Condition,
            listing.PhoneNumber,
            listing.WhatsAppNumber,
            Status = listing.Status.ToString(),
            listing.RejectionReason,
            listing.PublishedAt,
            listing.CreatedAt,
            listing.UpdatedAt,
            Seller = new
            {
                listing.SellerId,
                listing.Seller!.Email,
                listing.Seller.FullName,
                listing.Seller.PhoneNumber,
                listing.Seller.WhatsAppNumber
            },
            Brand = listing.Brand!.Name,
            Model = listing.Model!.Name,
            City = listing.City!.Name,
            Category = listing.Category!.Name,
            Photos = listing.Images
                .OrderBy(x => x.DisplayOrder)
                .Select(x => new { x.Id, x.Url, x.DisplayOrder })
                .ToList()
        };

        return OkResponse((object)result, "Listing details loaded.");
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id)
    {
        var listing = await dbContext.Listings.SingleOrDefaultAsync(x => x.Id == id);
        if (listing is null)
        {
            return NotFoundResponse("Listing not found.");
        }

        if (listing.Status == ListingStatus.Published) { return OkResponse(new { listing.Id, Status = listing.Status.ToString(), listing.PublishedAt }, "Listing already published."); }

        var previousStatus = listing.Status;
        listing.Status = ListingStatus.Published;
        listing.RejectionReason = null;
        listing.PublishedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();

        var admin = GetAdminIdentity();
        await auditService.LogAsync(
            admin.UserId,
            admin.Email,
            action: "listing.approved",
            entityType: "listing",
            entityId: listing.Id,
            details: new { previousStatus = previousStatus.ToString(), newStatus = listing.Status.ToString() });

        return OkResponse(new { listing.Id, Status = listing.Status.ToString(), listing.PublishedAt }, "Listing approved.");
    }

    [HttpPost("{id:guid}/reject")]
    public IActionResult Reject(Guid id, [FromBody] RejectListingRequest request)
    {
        return BadRequestResponse("Manual rejection workflow disabled.", "Use archive/hide for published listings.");
    }

    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id)
    {
        return await SetArchivedState(id, "listing.archived", "Listing archived.");
    }

    [HttpPost("{id:guid}/hide")]
    public async Task<IActionResult> Hide(Guid id)
    {
        return await SetArchivedState(id, "listing.hidden", "Listing hidden.");
    }

    [HttpPost("{id:guid}/restore")]
    public async Task<IActionResult> Restore(Guid id)
    {
        var listing = await dbContext.Listings.SingleOrDefaultAsync(x => x.Id == id);
        if (listing is null)
        {
            return NotFoundResponse("Listing not found.");
        }

        if (listing.Status != ListingStatus.Archived)
        {
            return BadRequestResponse("Listing cannot be restored.", "Only archived listings can be restored.");
        }

        var previousStatus = listing.Status;
        listing.Status = ListingStatus.Published;
        listing.RejectionReason = null;
        listing.PublishedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();

        var admin = GetAdminIdentity();
        await auditService.LogAsync(
            admin.UserId,
            admin.Email,
            action: "listing.restored",
            entityType: "listing",
            entityId: listing.Id,
            details: new { previousStatus = previousStatus.ToString(), newStatus = listing.Status.ToString() });

        return OkResponse(new { listing.Id, Status = listing.Status.ToString(), listing.PublishedAt }, "Listing restored as published.");
    }

    [HttpGet("{id:guid}/decisions")]
    public async Task<IActionResult> DecisionHistory(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 100);

        var exists = await dbContext.Listings.AsNoTracking().AnyAsync(x => x.Id == id);
        if (!exists)
        {
            return NotFoundResponse("Listing not found.");
        }

        var decisions = dbContext.AdminAuditLogs
            .AsNoTracking()
            .Where(x => x.EntityType == "listing"
                && x.EntityId == id
                && (x.Action == "listing.approved" || x.Action == "listing.rejected"))
            .OrderByDescending(x => x.CreatedAt);

        var total = await decisions.CountAsync();
        var items = await decisions
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(x => new
            {
                x.Id,
                x.Action,
                x.AdminUserId,
                x.AdminEmail,
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
        }, "Listing decisions loaded.");
    }

    private async Task<IActionResult> SetArchivedState(Guid id, string action, string message)
    {
        var listing = await dbContext.Listings.SingleOrDefaultAsync(x => x.Id == id);
        if (listing is null)
        {
            return NotFoundResponse("Listing not found.");
        }

        if (listing.Status == ListingStatus.Archived)
        {
            return BadRequestResponse("Listing already archived.");
        }

        var previousStatus = listing.Status;
        listing.Status = ListingStatus.Archived;
        listing.PublishedAt = null;

        await dbContext.SaveChangesAsync();

        var admin = GetAdminIdentity();
        await auditService.LogAsync(
            admin.UserId,
            admin.Email,
            action: action,
            entityType: "listing",
            entityId: listing.Id,
            details: new { previousStatus = previousStatus.ToString(), newStatus = listing.Status.ToString() });

        return OkResponse(new { listing.Id, Status = listing.Status.ToString() }, message);
    }
}
































