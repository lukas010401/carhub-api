using CarHub.Api.Application.Contracts.Common;
using CarHub.Api.Application.Contracts.Listings;
using CarHub.Api.Application.Contracts.Media;
using CarHub.Api.Domain.Entities;
using CarHub.Api.Domain.Enums;
using CarHub.Api.Infrastructure.Media;
using CarHub.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace CarHub.Api.Controllers;

[ApiController]
[Authorize(Policy = "SellerOrAdminPolicy")]
[Route("api/seller/listings")]
public sealed class SellerListingsController(
    AppDbContext dbContext,
    IListingImageStorage imageStorage,
    IOptions<MediaOptions> mediaOptions) : ControllerBase
{
    private readonly MediaOptions _mediaOptions = mediaOptions.Value;

    [HttpGet]
    public async Task<IActionResult> GetMine(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 8,
        [FromQuery] string? status = null,
        [FromQuery] string? keyword = null,
        [FromQuery] string? sortBy = null)
    {
        var userId = GetUserId();
        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 50);

        var baseQuery = dbContext.Listings
            .AsNoTracking()
            .Include(x => x.Brand)
            .Include(x => x.Model)
            .Where(x => x.SellerId == userId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ListingStatus>(status, true, out var parsedStatus))
        {
            baseQuery = baseQuery.Where(x => x.Status == parsedStatus);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim().ToLower();
            baseQuery = baseQuery.Where(x =>
                x.Title.ToLower().Contains(kw) ||
                x.Brand!.Name.ToLower().Contains(kw) ||
                x.Model!.Name.ToLower().Contains(kw));
        }

        baseQuery = (sortBy ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "price_asc" or "price-asc" => baseQuery.OrderBy(x => x.Price),
            "price_desc" or "price-desc" => baseQuery.OrderByDescending(x => x.Price),
            "year_asc" or "year-asc" => baseQuery.OrderBy(x => x.Year),
            "year_desc" or "year-desc" => baseQuery.OrderByDescending(x => x.Year),
            "mileage_asc" or "mileage-asc" => baseQuery.OrderBy(x => x.Mileage),
            "mileage_desc" or "mileage-desc" => baseQuery.OrderByDescending(x => x.Mileage),
            _ => baseQuery.OrderByDescending(x => x.CreatedAt)
        };

        var statusCounts = await dbContext.Listings
            .AsNoTracking()
            .Where(x => x.SellerId == userId)
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

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
                RejectionReason = x.RejectionReason,
                Brand = x.Brand!.Name,
                Model = x.Model!.Name,
                CoverImage = x.Images.OrderBy(i => i.DisplayOrder).Select(i => i.Url).FirstOrDefault(),
                LatestManualPayment = dbContext.ManualPaymentRequests
                    .Where(p => p.ListingId == x.Id)
                    .OrderByDescending(p => p.CreatedAt)
                    .Select(p => new { p.Id, Status = p.Status.ToString(), p.InternalReference })
                    .FirstOrDefault(),
                x.CreatedAt,
                x.UpdatedAt,
                x.PublishedAt
            })
            .ToListAsync();

        int Count(ListingStatus target) => statusCounts.FirstOrDefault(x => x.Status == target)?.Count ?? 0;

        return Ok(new
        {
            page = safePage,
            pageSize = safePageSize,
            total,
            totalCount = total,
            items,
            stats = new
            {
                total = statusCounts.Sum(x => x.Count),
                approved = Count(ListingStatus.Published),
                published = Count(ListingStatus.Published),
                pending = Count(ListingStatus.PendingReview),
                sold = Count(ListingStatus.Sold),
                draft = Count(ListingStatus.Draft),
                rejected = Count(ListingStatus.Rejected),
                archived = Count(ListingStatus.Archived)
            }
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var userId = GetUserId();
        var listing = await dbContext.Listings
            .AsNoTracking()
            .Include(x => x.Images.OrderBy(i => i.DisplayOrder))
            .SingleOrDefaultAsync(x => x.Id == id && x.SellerId == userId);

        return listing is null ? NotFound() : Ok(listing);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ListingUpsertRequest request)
    {
        var userId = GetUserId();
        var listing = new Listing
        {
            SellerId = userId,
            Status = ListingStatus.Draft
        };

        ApplyRequest(listing, request);

        dbContext.Listings.Add(listing);
        await dbContext.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = listing.Id }, new { listing.Id, Status = listing.Status.ToString() });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] ListingUpsertRequest request)
    {
        var userId = GetUserId();
        var listing = await dbContext.Listings.SingleOrDefaultAsync(x => x.Id == id && x.SellerId == userId);
        if (listing is null)
        {
            return NotFound();
        }

        if (listing.Status is ListingStatus.Sold or ListingStatus.Archived)
        {
            return BadRequest("Sold or archived listings cannot be edited.");
        }

        ApplyRequest(listing, request);
        await dbContext.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = GetUserId();
        var listing = await dbContext.Listings.SingleOrDefaultAsync(x => x.Id == id && x.SellerId == userId);
        if (listing is null)
        {
            return NotFound();
        }

        dbContext.Listings.Remove(listing);
        await dbContext.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> SubmitForReview(Guid id)
    {
        var userId = GetUserId();
        var listing = await dbContext.Listings.SingleOrDefaultAsync(x => x.Id == id && x.SellerId == userId);
        if (listing is null)
        {
            return NotFound();
        }

        if (listing.Status == ListingStatus.Published)
        {
            return BadRequest("Listing is already published.");
        }

        var seller = await dbContext.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId);
        if (seller is null) return NotFound();

        if (seller.AccountType == AccountType.Professional)
        {
            if (!await HasActiveProfessionalSubscription(userId))
            {
                return BadRequest("Active professional subscription required before publishing.");
            }
        }
        else
        {
            if (!await HasApprovedListingPayment(listing.Id, userId))
            {
                return BadRequest("Validated payment required before publishing this listing.");
            }
        }

        listing.Status = ListingStatus.Published;
        listing.RejectionReason = null;
        listing.PublishedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        return Ok(new { listing.Id, Status = listing.Status.ToString(), listing.PublishedAt });
    }

    [HttpPost("{id:guid}/mark-sold")]
    public async Task<IActionResult> MarkAsSold(Guid id)
    {
        var userId = GetUserId();
        var listing = await dbContext.Listings.SingleOrDefaultAsync(x => x.Id == id && x.SellerId == userId);
        if (listing is null)
        {
            return NotFound();
        }

        if (listing.Status == ListingStatus.Sold)
        {
            return BadRequest("Listing is already marked as sold.");
        }

        if (listing.Status != ListingStatus.Published)
        {
            return BadRequest("Only published listings can be marked as sold.");
        }

        listing.Status = ListingStatus.Sold;
        listing.RejectionReason = null;
        listing.PublishedAt = null;
        await dbContext.SaveChangesAsync();

        return Ok(new { listing.Id, Status = listing.Status.ToString(), listing.PublishedAt });
    }

    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id)
    {
        var userId = GetUserId();
        var listing = await dbContext.Listings.SingleOrDefaultAsync(x => x.Id == id && x.SellerId == userId);
        if (listing is null)
        {
            return NotFound();
        }

        if (listing.Status == ListingStatus.Archived)
        {
            return BadRequest("Listing is already archived.");
        }

        listing.Status = ListingStatus.Archived;
        listing.PublishedAt = null;
        await dbContext.SaveChangesAsync();

        return Ok(new { listing.Id, Status = listing.Status.ToString(), listing.PublishedAt });
    }

    [HttpPost("{id:guid}/relist")]
    public async Task<IActionResult> Relist(Guid id)
    {
        var userId = GetUserId();
        var listing = await dbContext.Listings.SingleOrDefaultAsync(x => x.Id == id && x.SellerId == userId);
        if (listing is null)
        {
            return NotFound();
        }

        if (listing.Status is not (ListingStatus.Sold or ListingStatus.Archived))
        {
            return BadRequest("Only sold or archived listings can be relisted.");
        }

        listing.Status = ListingStatus.Published;
        listing.RejectionReason = null;
        listing.PublishedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        return Ok(new { listing.Id, Status = listing.Status.ToString(), listing.PublishedAt });
    }

    [HttpPost("{id:guid}/duplicate")]
    public async Task<IActionResult> Duplicate(Guid id)
    {
        var userId = GetUserId();
        var source = await dbContext.Listings
            .Include(x => x.Images)
            .SingleOrDefaultAsync(x => x.Id == id && x.SellerId == userId);

        if (source is null)
        {
            return NotFound();
        }

        var copy = new Listing
        {
            SellerId = userId,
            BrandId = source.BrandId,
            ModelId = source.ModelId,
            Year = source.Year,
            Price = source.Price,
            Mileage = source.Mileage,
            FuelType = source.FuelType,
            TransmissionType = source.TransmissionType,
            CityId = source.CityId,
            CategoryId = source.CategoryId,
            Title = source.Title,
            Description = source.Description,
            EngineSize = source.EngineSize,
            Color = source.Color,
            Doors = source.Doors,
            Condition = source.Condition,
            PhoneNumber = source.PhoneNumber,
            WhatsAppNumber = source.WhatsAppNumber,
            Status = ListingStatus.Draft,
            PublishedAt = null,
            RejectionReason = null
        };

        foreach (var image in source.Images.OrderBy(x => x.DisplayOrder))
        {
            copy.Images.Add(new ListingImage
            {
                Url = image.Url,
                StorageKey = image.StorageKey,
                DisplayOrder = image.DisplayOrder
            });
        }

        dbContext.Listings.Add(copy);
        await dbContext.SaveChangesAsync();

        return Ok(new { copy.Id, Status = copy.Status.ToString() });
    }

    [HttpPost("bulk-action")]
    public async Task<IActionResult> BulkAction([FromBody] BulkListingActionRequest request)
    {
        var userId = GetUserId();
        var action = (request.Action ?? string.Empty).Trim().ToLowerInvariant();
        var ids = request.ListingIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return BadRequest("ListingIds is required.");
        }

        var listings = await dbContext.Listings
            .Where(x => x.SellerId == userId && ids.Contains(x.Id))
            .ToListAsync();

        var seller = action == "submit"
            ? await dbContext.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId)
            : null;

        if (action == "submit" && seller is null)
        {
            return NotFound();
        }

        var hasActivePro = false;
        if (action == "submit" && seller!.AccountType == AccountType.Professional)
        {
            hasActivePro = await HasActiveProfessionalSubscription(userId);
            if (!hasActivePro)
            {
                return BadRequest("Active professional subscription required before publishing.");
            }
        }

        var updated = 0;
        var failed = new List<Guid>();

        foreach (var listing in listings)
        {
            var ok = action switch
            {
                "submit" => await TryApplySubmitAsync(listing, seller!),
                "mark_sold" => ApplyMarkSold(listing),
                "archive" => ApplyArchive(listing),
                "relist" => ApplyRelist(listing),
                "delete" => ApplyDelete(listing),
                _ => false
            };

            if (ok) updated++;
            else failed.Add(listing.Id);
        }

        if (action is not ("submit" or "mark_sold" or "archive" or "relist" or "delete"))
        {
            return BadRequest("Unknown bulk action.");
        }

        await dbContext.SaveChangesAsync();
        return Ok(new { Action = action, Requested = ids.Count, Updated = updated, Failed = failed });
    }

    [HttpPost("{id:guid}/images")]
    [RequestSizeLimit(52_428_800)]
    public async Task<IActionResult> UploadImages(Guid id, [FromForm] List<IFormFile> files, CancellationToken cancellationToken)
    {
        if (files.Count == 0)
        {
            return BadRequest("At least one image is required.");
        }

        var userId = GetUserId();
        var listing = await dbContext.Listings
            .Include(x => x.Images)
            .SingleOrDefaultAsync(x => x.Id == id && x.SellerId == userId, cancellationToken);

        if (listing is null)
        {
            return NotFound();
        }

        var existing = listing.Images.Count;
        if (existing + files.Count > _mediaOptions.MaxImagesPerListing)
        {
            return BadRequest($"Maximum {_mediaOptions.MaxImagesPerListing} images per listing.");
        }

        var nextOrder = listing.Images.Count == 0 ? 1 : listing.Images.Max(x => x.DisplayOrder) + 1;
        var created = new List<object>();

        foreach (var file in files)
        {
            var stored = await imageStorage.SaveAsync(id, file, cancellationToken);
            var image = new ListingImage
            {
                ListingId = id,
                Url = stored.Url,
                StorageKey = stored.StorageKey,
                DisplayOrder = nextOrder++
            };

            dbContext.ListingImages.Add(image);
            created.Add(new { image.Id, image.Url, image.DisplayOrder });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(created);
    }

    [HttpDelete("{id:guid}/images/{imageId:guid}")]
    public async Task<IActionResult> DeleteImage(Guid id, Guid imageId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var image = await dbContext.ListingImages
            .Include(x => x.Listing)
            .SingleOrDefaultAsync(x => x.Id == imageId && x.ListingId == id, cancellationToken);

        if (image is null || image.Listing is null || image.Listing.SellerId != userId)
        {
            return NotFound();
        }

        dbContext.ListingImages.Remove(image);
        await dbContext.SaveChangesAsync(cancellationToken);
        await imageStorage.DeleteAsync(image.StorageKey, cancellationToken);

        return NoContent();
    }

    [HttpPut("{id:guid}/images/reorder")]
    public async Task<IActionResult> ReorderImages(Guid id, [FromBody] ReorderListingImagesRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var listing = await dbContext.Listings
            .Include(x => x.Images)
            .SingleOrDefaultAsync(x => x.Id == id && x.SellerId == userId, cancellationToken);

        if (listing is null)
        {
            return NotFound();
        }

        if (request.ImageIds.Count != listing.Images.Count)
        {
            return BadRequest("ImageIds must contain all listing images.");
        }

        var dbIds = listing.Images.Select(x => x.Id).OrderBy(x => x).ToList();
        var reqIds = request.ImageIds.OrderBy(x => x).ToList();
        if (!dbIds.SequenceEqual(reqIds))
        {
            return BadRequest("ImageIds do not match listing images.");
        }

        for (var i = 0; i < request.ImageIds.Count; i++)
        {
            var image = listing.Images.Single(x => x.Id == request.ImageIds[i]);
            image.DisplayOrder = i + 1;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<bool> TryApplySubmitAsync(Listing listing, User seller)
    {
        if (listing.Status == ListingStatus.Published) return false;

        if (seller.AccountType == AccountType.Professional)
        {
            if (!await HasActiveProfessionalSubscription(seller.Id)) return false;
        }
        else
        {
            if (!await HasApprovedListingPayment(listing.Id, seller.Id)) return false;
        }

        listing.Status = ListingStatus.Published;
        listing.RejectionReason = null;
        listing.PublishedAt = DateTime.UtcNow;
        return true;
    }

    private static bool ApplyMarkSold(Listing listing)
    {
        if (listing.Status != ListingStatus.Published) return false;
        listing.Status = ListingStatus.Sold;
        listing.RejectionReason = null;
        listing.PublishedAt = null;
        return true;
    }

    private static bool ApplyArchive(Listing listing)
    {
        if (listing.Status == ListingStatus.Archived) return false;
        listing.Status = ListingStatus.Archived;
        listing.PublishedAt = null;
        return true;
    }

    private static bool ApplyRelist(Listing listing)
    {
        if (listing.Status is not (ListingStatus.Sold or ListingStatus.Archived)) return false;
        listing.Status = ListingStatus.Published;
        listing.RejectionReason = null;
        listing.PublishedAt = DateTime.UtcNow;
        return true;
    }

    private bool ApplyDelete(Listing listing)
    {
        dbContext.Listings.Remove(listing);
        return true;
    }

    private async Task<bool> HasApprovedListingPayment(Guid listingId, Guid userId)
    {
        return await dbContext.ManualPaymentRequests
            .AsNoTracking()
            .AnyAsync(x => x.ListingId == listingId
                && x.UserId == userId
                && x.Type == ManualPaymentType.ListingPublication
                && x.Status == ManualPaymentStatus.Approved);
    }

    private async Task<bool> HasActiveProfessionalSubscription(Guid userId)
    {
        var now = DateTime.UtcNow;
        return await dbContext.ProfessionalSubscriptions
            .AsNoTracking()
            .AnyAsync(x => x.UserId == userId
                && x.Status == SubscriptionStatus.Active
                && x.StartsAtUtc <= now
                && x.EndsAtUtc >= now);
    }

    private static void ApplyRequest(Listing listing, ListingUpsertRequest request)
    {
        listing.BrandId = request.BrandId;
        listing.ModelId = request.ModelId;
        listing.Year = request.Year;
        listing.Price = request.Price;
        listing.Mileage = request.Mileage;
        listing.FuelType = request.FuelType;
        listing.TransmissionType = request.TransmissionType;
        listing.CityId = request.CityId;
        listing.CategoryId = request.CategoryId;
        listing.Title = request.Title.Trim();
        listing.Description = request.Description.Trim();
        listing.EngineSize = request.EngineSize?.Trim();
        listing.Color = request.Color?.Trim();
        listing.Doors = request.Doors;
        listing.Condition = request.Condition?.Trim();
        listing.PhoneNumber = request.PhoneNumber.Trim();
        listing.WhatsAppNumber = request.WhatsAppNumber?.Trim();
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.Parse(sub!);
    }
}




