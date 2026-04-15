using CarHub.Api.Application.Contracts.Listings;
using CarHub.Api.Domain.Enums;
using CarHub.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarHub.Api.Controllers;

[ApiController]
[Route("api/public/listings")]
public sealed class PublicListingsController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] PublicListingQuery query)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 50);

        var listings = dbContext.Listings
            .AsNoTracking()
            .Where(x => x.Status == ListingStatus.Published && x.PublishedAt != null)
            .AsQueryable();

        if (query.BrandId.HasValue) listings = listings.Where(x => x.BrandId == query.BrandId.Value);
        if (query.ModelId.HasValue) listings = listings.Where(x => x.ModelId == query.ModelId.Value);
        if (query.CityId.HasValue) listings = listings.Where(x => x.CityId == query.CityId.Value);
        if (query.CategoryId.HasValue) listings = listings.Where(x => x.CategoryId == query.CategoryId.Value);
        if (query.MinPrice.HasValue) listings = listings.Where(x => x.Price >= query.MinPrice.Value);
        if (query.MaxPrice.HasValue) listings = listings.Where(x => x.Price <= query.MaxPrice.Value);
        if (query.MinYear.HasValue) listings = listings.Where(x => x.Year >= query.MinYear.Value);
        if (query.MaxYear.HasValue) listings = listings.Where(x => x.Year <= query.MaxYear.Value);
        if (query.MaxMileage.HasValue) listings = listings.Where(x => x.Mileage <= query.MaxMileage.Value);
        if (query.FuelType.HasValue) listings = listings.Where(x => x.FuelType == query.FuelType.Value);
        if (query.TransmissionType.HasValue) listings = listings.Where(x => x.TransmissionType == query.TransmissionType.Value);

        var keyword = string.IsNullOrWhiteSpace(query.Keyword)
            ? null
            : query.Keyword.Trim().ToLower();

        if (keyword is not null)
        {
            listings = listings.Where(x =>
                x.Title.ToLower().Contains(keyword) ||
                x.Brand!.Name.ToLower().Contains(keyword) ||
                x.Model!.Name.ToLower().Contains(keyword) ||
                x.City!.Name.ToLower().Contains(keyword) ||
                x.Category!.Name.ToLower().Contains(keyword));
        }

        var ranked = listings.Select(x => new
        {
            Listing = x,
            IsProfessionalSeller = x.Seller != null && x.Seller.AccountType == AccountType.Professional,
            RelevanceScore = keyword == null
                ? 0
                :
                    (x.Title.ToLower() == keyword ? 200 : 0) +
                    (x.Model!.Name.ToLower() == keyword ? 180 : 0) +
                    (x.Brand!.Name.ToLower() == keyword ? 160 : 0) +
                    (x.Category!.Name.ToLower() == keyword ? 120 : 0) +
                    (x.City!.Name.ToLower() == keyword ? 100 : 0) +
                    (x.Title.ToLower().StartsWith(keyword) ? 70 : 0) +
                    (x.Model!.Name.ToLower().StartsWith(keyword) ? 60 : 0) +
                    (x.Brand!.Name.ToLower().StartsWith(keyword) ? 50 : 0) +
                    (x.Title.ToLower().Contains(keyword) ? 30 : 0) +
                    (x.Model!.Name.ToLower().Contains(keyword) ? 24 : 0) +
                    (x.Brand!.Name.ToLower().Contains(keyword) ? 18 : 0)
        });

        var sortBy = (query.SortBy ?? string.Empty).Trim().ToLowerInvariant();

        var ordered = ranked.OrderByDescending(x => x.RelevanceScore);

        if (query.ProsFirst)
        {
            ordered = ordered.ThenByDescending(x => x.IsProfessionalSeller);
        }

        ordered = sortBy switch
        {
            "price_asc" or "price-asc" => ordered.ThenBy(x => x.Listing.Price),
            "price_desc" or "price-desc" => ordered.ThenByDescending(x => x.Listing.Price),
            "year_asc" or "year-asc" => ordered.ThenBy(x => x.Listing.Year),
            "year_desc" or "year-desc" => ordered.ThenByDescending(x => x.Listing.Year),
            "mileage_asc" or "mileage-asc" => ordered.ThenBy(x => x.Listing.Mileage),
            "mileage_desc" or "mileage-desc" => ordered.ThenByDescending(x => x.Listing.Mileage),
            _ => ordered.ThenByDescending(x => x.Listing.PublishedAt)
        };

        var total = await ranked.CountAsync();
        var data = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Listing.Id,
                x.Listing.Title,
                x.Listing.Price,
                x.Listing.Year,
                x.Listing.Mileage,
                FuelType = x.Listing.FuelType.ToString(),
                TransmissionType = x.Listing.TransmissionType.ToString(),
                Brand = x.Listing.Brand!.Name,
                Model = x.Listing.Model!.Name,
                City = x.Listing.City!.Name,
                Category = x.Listing.Category!.Name,
                CoverImage = x.Listing.Images.OrderBy(i => i.DisplayOrder).Select(i => i.Url).FirstOrDefault(),
                SellerAccountType = x.Listing.Seller != null ? x.Listing.Seller.AccountType.ToString() : null,
                x.IsProfessionalSeller,
                x.Listing.PublishedAt
            })
            .ToListAsync();

        return Ok(new
        {
            page,
            pageSize,
            total,
            totalCount = total,
            items = data
        });
    }

    [HttpGet("brands/popular")]
    public async Task<IActionResult> GetPopularBrands([FromQuery] int limit = 10)
    {
        var safeLimit = Math.Clamp(limit, 1, 30);

        var items = await dbContext.Listings
            .AsNoTracking()
            .Where(x => x.Status == ListingStatus.Published && x.PublishedAt != null)
            .GroupBy(x => new { x.BrandId, BrandName = x.Brand!.Name, BrandSlug = x.Brand!.Slug })
            .Select(g => new
            {
                Id = g.Key.BrandId,
                Name = g.Key.BrandName,
                Slug = g.Key.BrandSlug,
                ListingsCount = g.Count(),
                LogoUrl = $"/brand-logos/{g.Key.BrandSlug}.svg"
            })
            .OrderByDescending(x => x.ListingsCount)
            .ThenBy(x => x.Name)
            .Take(safeLimit)
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var listing = await dbContext.Listings
            .AsNoTracking()
            .Include(x => x.Brand)
            .Include(x => x.Model)
            .Include(x => x.City)
            .Include(x => x.Category)
            .Include(x => x.Seller)
            .Include(x => x.Images.OrderBy(i => i.DisplayOrder))
            .Where(x => x.Id == id && x.Status == ListingStatus.Published && x.PublishedAt != null)
            .Select(x => new
            {
                x.Id,
                x.Title,
                x.Description,
                x.Price,
                x.Year,
                x.Mileage,
                FuelType = x.FuelType.ToString(),
                TransmissionType = x.TransmissionType.ToString(),
                x.EngineSize,
                x.Color,
                x.Doors,
                x.Condition,
                Brand = x.Brand!.Name,
                Model = x.Model!.Name,
                City = x.City!.Name,
                Category = x.Category!.Name,
                Seller = new
                {
                    x.Seller!.FullName,
                    x.Seller.ProfileImageUrl,
                    PhoneNumber = x.PhoneNumber,
                    WhatsAppNumber = x.WhatsAppNumber,
                    AccountType = x.Seller.AccountType.ToString(),
                    IsProfessional = x.Seller.AccountType == AccountType.Professional
                },
                Images = x.Images.OrderBy(i => i.DisplayOrder).Select(i => new { i.Url, i.DisplayOrder }),
                x.PublishedAt
            })
            .SingleOrDefaultAsync();

        return listing is null ? NotFound() : Ok(listing);
    }
}

