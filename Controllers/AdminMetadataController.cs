using CarHub.Api.Application.Contracts.Common;
using CarHub.Api.Infrastructure.Audit;
using CarHub.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace CarHub.Api.Controllers;

[ApiController]
[Authorize(Policy = "AdminPolicy")]
[Route("api/admin/metadata")]
public sealed class AdminMetadataController(
    AppDbContext dbContext,
    IAdminAuditService auditService) : AdminControllerBase
{
    private const int DefaultPageSize = 10;
    private const int MaxPageSize = 100;

    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview()
    {
        var brands = await dbContext.Brands
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Name, x.Slug, x.IsActive })
            .ToListAsync();

        var categories = await dbContext.Categories
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Name, x.Slug, x.IsActive })
            .ToListAsync();

        var cities = await dbContext.Cities
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Name, x.Slug, x.Province, x.IsActive })
            .ToListAsync();

        var models = await dbContext.Models
            .AsNoTracking()
            .Include(x => x.Brand)
            .OrderBy(x => x.Brand!.Name)
            .ThenBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.BrandId,
                BrandName = x.Brand!.Name,
                x.Name,
                x.Slug,
                x.IsActive
            })
            .ToListAsync();

        return OkResponse(new { brands, categories, cities, models }, "Reference data loaded.");
    }

    [HttpGet("brands")]
    public async Task<IActionResult> GetBrands([FromQuery] MetadataListQuery query)
    {
        var (page, pageSize) = NormalizePaging(query.Page, query.PageSize);

        var q = dbContext.Brands.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim().ToLower();
            q = q.Where(x => x.Name.ToLower().Contains(keyword) || x.Slug.ToLower().Contains(keyword));
        }
        if (query.IsActive.HasValue) q = q.Where(x => x.IsActive == query.IsActive.Value);

        var total = await q.CountAsync();
        var items = await q
            .OrderBy(x => x.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new SimpleMetadataItemResponse
            {
                Id = x.Id,
                Name = x.Name,
                Slug = x.Slug,
                IsActive = x.IsActive
            })
            .ToListAsync();

        return OkResponse(new PagedResult<SimpleMetadataItemResponse>
        {
            Page = page,
            PageSize = pageSize,
            Total = total,
            Items = items
        }, "Brands loaded.");
    }

    [HttpGet("brands/options")]
    public async Task<IActionResult> GetBrandOptions()
    {
        var items = await dbContext.Brands
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new SimpleMetadataItemResponse
            {
                Id = x.Id,
                Name = x.Name,
                Slug = x.Slug,
                IsActive = x.IsActive
            })
            .ToListAsync();

        return OkResponse(items, "Brand options loaded.");
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories([FromQuery] MetadataListQuery query)
    {
        var (page, pageSize) = NormalizePaging(query.Page, query.PageSize);

        var q = dbContext.Categories.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim().ToLower();
            q = q.Where(x => x.Name.ToLower().Contains(keyword) || x.Slug.ToLower().Contains(keyword));
        }
        if (query.IsActive.HasValue) q = q.Where(x => x.IsActive == query.IsActive.Value);

        var total = await q.CountAsync();
        var items = await q
            .OrderBy(x => x.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new SimpleMetadataItemResponse
            {
                Id = x.Id,
                Name = x.Name,
                Slug = x.Slug,
                IsActive = x.IsActive
            })
            .ToListAsync();

        return OkResponse(new PagedResult<SimpleMetadataItemResponse>
        {
            Page = page,
            PageSize = pageSize,
            Total = total,
            Items = items
        }, "Categories loaded.");
    }

    [HttpGet("cities")]
    public async Task<IActionResult> GetCities([FromQuery] MetadataListQuery query)
    {
        var (page, pageSize) = NormalizePaging(query.Page, query.PageSize);

        var q = dbContext.Cities.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim().ToLower();
            q = q.Where(x => x.Name.ToLower().Contains(keyword) || x.Slug.ToLower().Contains(keyword));
        }
        if (query.IsActive.HasValue) q = q.Where(x => x.IsActive == query.IsActive.Value);

        var total = await q.CountAsync();
        var items = await q
            .OrderBy(x => x.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new CityMetadataItemResponse
            {
                Id = x.Id,
                Name = x.Name,
                Slug = x.Slug,
                Province = x.Province,
                IsActive = x.IsActive
            })
            .ToListAsync();

        return OkResponse(new PagedResult<CityMetadataItemResponse>
        {
            Page = page,
            PageSize = pageSize,
            Total = total,
            Items = items
        }, "Cities loaded.");
    }

    [HttpGet("models")]
    public async Task<IActionResult> GetModels([FromQuery] MetadataListQuery query)
    {
        var (page, pageSize) = NormalizePaging(query.Page, query.PageSize);

        var q = dbContext.Models
            .AsNoTracking()
            .Include(x => x.Brand)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim().ToLower();
            q = q.Where(x => x.Name.ToLower().Contains(keyword) || x.Slug.ToLower().Contains(keyword) || x.Brand!.Name.ToLower().Contains(keyword));
        }
        if (query.IsActive.HasValue) q = q.Where(x => x.IsActive == query.IsActive.Value);
        if (query.BrandId.HasValue && query.BrandId.Value != Guid.Empty) q = q.Where(x => x.BrandId == query.BrandId.Value);

        var total = await q.CountAsync();
        var items = await q
            .OrderBy(x => x.Brand!.Name)
            .ThenBy(x => x.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ModelMetadataItemResponse
            {
                Id = x.Id,
                BrandId = x.BrandId,
                BrandName = x.Brand!.Name,
                Name = x.Name,
                Slug = x.Slug,
                IsActive = x.IsActive
            })
            .ToListAsync();

        return OkResponse(new PagedResult<ModelMetadataItemResponse>
        {
            Page = page,
            PageSize = pageSize,
            Total = total,
            Items = items
        }, "Models loaded.");
    }

    [HttpPost("brands")]
    public async Task<IActionResult> CreateBrand([FromBody] UpsertSimpleMetadataRequest request)
    {
        var validation = ValidateSimpleRequest(request);
        if (validation is not null) return validation;

        var slug = ResolveSlug(request.Name, request.Slug);
        var exists = await dbContext.Brands.AnyAsync(x => x.Slug == slug || x.Name.ToLower() == request.Name.Trim().ToLower());
        if (exists) return BadRequestResponse("Brand already exists.");

        var admin = GetAdminIdentity();
        var brand = new Domain.Entities.Brand
        {
            Name = request.Name.Trim(),
            Slug = slug,
            IsActive = true
        };

        dbContext.Brands.Add(brand);
        await dbContext.SaveChangesAsync();

        await auditService.LogAsync(admin.UserId, admin.Email, "metadata.brand.created", "brand", brand.Id, new { brand.Name, brand.Slug });
        return OkResponse(new { brand.Id, brand.Name, brand.Slug, brand.IsActive }, "Brand created.");
    }

    [HttpPut("brands/{id:guid}")]
    public async Task<IActionResult> UpdateBrand(Guid id, [FromBody] UpsertSimpleMetadataRequest request)
    {
        var validation = ValidateSimpleRequest(request);
        if (validation is not null) return validation;

        var brand = await dbContext.Brands.SingleOrDefaultAsync(x => x.Id == id);
        if (brand is null) return NotFoundResponse("Brand not found.");

        var slug = ResolveSlug(request.Name, request.Slug);
        var duplicate = await dbContext.Brands.AnyAsync(x => x.Id != id && (x.Slug == slug || x.Name.ToLower() == request.Name.Trim().ToLower()));
        if (duplicate) return BadRequestResponse("Another brand already uses this name or slug.");

        var admin = GetAdminIdentity();
        var prev = new { brand.Name, brand.Slug };

        brand.Name = request.Name.Trim();
        brand.Slug = slug;
        await dbContext.SaveChangesAsync();

        await auditService.LogAsync(admin.UserId, admin.Email, "metadata.brand.updated", "brand", brand.Id, new { previous = prev, current = new { brand.Name, brand.Slug } });
        return OkResponse(new { brand.Id, brand.Name, brand.Slug, brand.IsActive }, "Brand updated.");
    }

    [HttpPatch("brands/{id:guid}/activation")]
    public async Task<IActionResult> SetBrandActivation(Guid id, [FromBody] SetActivationRequest request)
    {
        var brand = await dbContext.Brands.SingleOrDefaultAsync(x => x.Id == id);
        if (brand is null) return NotFoundResponse("Brand not found.");

        var admin = GetAdminIdentity();
        brand.IsActive = request.IsActive;
        await dbContext.SaveChangesAsync();

        await auditService.LogAsync(admin.UserId, admin.Email, "metadata.brand.activation", "brand", brand.Id, new { brand.IsActive });
        return OkResponse(new { brand.Id, brand.IsActive }, "Brand status updated.");
    }

    [HttpPost("categories")]
    public async Task<IActionResult> CreateCategory([FromBody] UpsertSimpleMetadataRequest request)
    {
        var validation = ValidateSimpleRequest(request);
        if (validation is not null) return validation;

        var slug = ResolveSlug(request.Name, request.Slug);
        var exists = await dbContext.Categories.AnyAsync(x => x.Slug == slug || x.Name.ToLower() == request.Name.Trim().ToLower());
        if (exists) return BadRequestResponse("Category already exists.");

        var admin = GetAdminIdentity();
        var category = new Domain.Entities.Category
        {
            Name = request.Name.Trim(),
            Slug = slug,
            IsActive = true
        };

        dbContext.Categories.Add(category);
        await dbContext.SaveChangesAsync();

        await auditService.LogAsync(admin.UserId, admin.Email, "metadata.category.created", "category", category.Id, new { category.Name, category.Slug });
        return OkResponse(new { category.Id, category.Name, category.Slug, category.IsActive }, "Category created.");
    }

    [HttpPut("categories/{id:guid}")]
    public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] UpsertSimpleMetadataRequest request)
    {
        var validation = ValidateSimpleRequest(request);
        if (validation is not null) return validation;

        var category = await dbContext.Categories.SingleOrDefaultAsync(x => x.Id == id);
        if (category is null) return NotFoundResponse("Category not found.");

        var slug = ResolveSlug(request.Name, request.Slug);
        var duplicate = await dbContext.Categories.AnyAsync(x => x.Id != id && (x.Slug == slug || x.Name.ToLower() == request.Name.Trim().ToLower()));
        if (duplicate) return BadRequestResponse("Another category already uses this name or slug.");

        var admin = GetAdminIdentity();
        var prev = new { category.Name, category.Slug };

        category.Name = request.Name.Trim();
        category.Slug = slug;
        await dbContext.SaveChangesAsync();

        await auditService.LogAsync(admin.UserId, admin.Email, "metadata.category.updated", "category", category.Id, new { previous = prev, current = new { category.Name, category.Slug } });
        return OkResponse(new { category.Id, category.Name, category.Slug, category.IsActive }, "Category updated.");
    }

    [HttpPatch("categories/{id:guid}/activation")]
    public async Task<IActionResult> SetCategoryActivation(Guid id, [FromBody] SetActivationRequest request)
    {
        var category = await dbContext.Categories.SingleOrDefaultAsync(x => x.Id == id);
        if (category is null) return NotFoundResponse("Category not found.");

        var admin = GetAdminIdentity();
        category.IsActive = request.IsActive;
        await dbContext.SaveChangesAsync();

        await auditService.LogAsync(admin.UserId, admin.Email, "metadata.category.activation", "category", category.Id, new { category.IsActive });
        return OkResponse(new { category.Id, category.IsActive }, "Category status updated.");
    }

    [HttpPost("cities")]
    public async Task<IActionResult> CreateCity([FromBody] UpsertCityRequest request)
    {
        var validation = ValidateCityRequest(request);
        if (validation is not null) return validation;

        var slug = ResolveSlug(request.Name, request.Slug);
        var exists = await dbContext.Cities.AnyAsync(x => x.Slug == slug || x.Name.ToLower() == request.Name.Trim().ToLower());
        if (exists) return BadRequestResponse("City already exists.");

        var admin = GetAdminIdentity();
        var city = new Domain.Entities.City
        {
            Name = request.Name.Trim(),
            Slug = slug,
            Province = string.IsNullOrWhiteSpace(request.Province) ? null : request.Province.Trim(),
            IsActive = true
        };

        dbContext.Cities.Add(city);
        await dbContext.SaveChangesAsync();

        await auditService.LogAsync(admin.UserId, admin.Email, "metadata.city.created", "city", city.Id, new { city.Name, city.Slug, city.Province });
        return OkResponse(new { city.Id, city.Name, city.Slug, city.Province, city.IsActive }, "City created.");
    }

    [HttpPut("cities/{id:guid}")]
    public async Task<IActionResult> UpdateCity(Guid id, [FromBody] UpsertCityRequest request)
    {
        var validation = ValidateCityRequest(request);
        if (validation is not null) return validation;

        var city = await dbContext.Cities.SingleOrDefaultAsync(x => x.Id == id);
        if (city is null) return NotFoundResponse("City not found.");

        var slug = ResolveSlug(request.Name, request.Slug);
        var duplicate = await dbContext.Cities.AnyAsync(x => x.Id != id && (x.Slug == slug || x.Name.ToLower() == request.Name.Trim().ToLower()));
        if (duplicate) return BadRequestResponse("Another city already uses this name or slug.");

        var admin = GetAdminIdentity();
        var prev = new { city.Name, city.Slug, city.Province };

        city.Name = request.Name.Trim();
        city.Slug = slug;
        city.Province = string.IsNullOrWhiteSpace(request.Province) ? null : request.Province.Trim();
        await dbContext.SaveChangesAsync();

        await auditService.LogAsync(admin.UserId, admin.Email, "metadata.city.updated", "city", city.Id, new { previous = prev, current = new { city.Name, city.Slug, city.Province } });
        return OkResponse(new { city.Id, city.Name, city.Slug, city.Province, city.IsActive }, "City updated.");
    }

    [HttpPatch("cities/{id:guid}/activation")]
    public async Task<IActionResult> SetCityActivation(Guid id, [FromBody] SetActivationRequest request)
    {
        var city = await dbContext.Cities.SingleOrDefaultAsync(x => x.Id == id);
        if (city is null) return NotFoundResponse("City not found.");

        var admin = GetAdminIdentity();
        city.IsActive = request.IsActive;
        await dbContext.SaveChangesAsync();

        await auditService.LogAsync(admin.UserId, admin.Email, "metadata.city.activation", "city", city.Id, new { city.IsActive });
        return OkResponse(new { city.Id, city.IsActive }, "City status updated.");
    }

    [HttpPost("models")]
    public async Task<IActionResult> CreateModel([FromBody] UpsertModelRequest request)
    {
        var validation = await ValidateModelRequest(request);
        if (validation is not null) return validation;

        var slug = ResolveSlug(request.Name, request.Slug);
        var brandId = request.BrandId!.Value;
        var exists = await dbContext.Models.AnyAsync(x => x.BrandId == brandId && (x.Slug == slug || x.Name.ToLower() == request.Name.Trim().ToLower()));
        if (exists) return BadRequestResponse("Model already exists for this brand.");

        var admin = GetAdminIdentity();
        var model = new Domain.Entities.VehicleModel
        {
            BrandId = brandId,
            Name = request.Name.Trim(),
            Slug = slug,
            IsActive = true
        };

        dbContext.Models.Add(model);
        await dbContext.SaveChangesAsync();

        await auditService.LogAsync(admin.UserId, admin.Email, "metadata.model.created", "model", model.Id, new { model.BrandId, model.Name, model.Slug });
        return OkResponse(new { model.Id, model.BrandId, model.Name, model.Slug, model.IsActive }, "Model created.");
    }

    [HttpPut("models/{id:guid}")]
    public async Task<IActionResult> UpdateModel(Guid id, [FromBody] UpsertModelRequest request)
    {
        var validation = await ValidateModelRequest(request);
        if (validation is not null) return validation;

        var model = await dbContext.Models.SingleOrDefaultAsync(x => x.Id == id);
        if (model is null) return NotFoundResponse("Model not found.");

        var slug = ResolveSlug(request.Name, request.Slug);
        var brandId = request.BrandId!.Value;
        var duplicate = await dbContext.Models.AnyAsync(x => x.Id != id && x.BrandId == brandId && (x.Slug == slug || x.Name.ToLower() == request.Name.Trim().ToLower()));
        if (duplicate) return BadRequestResponse("Another model already uses this name or slug for this brand.");

        var admin = GetAdminIdentity();
        var prev = new { model.BrandId, model.Name, model.Slug };

        model.BrandId = brandId;
        model.Name = request.Name.Trim();
        model.Slug = slug;
        await dbContext.SaveChangesAsync();

        await auditService.LogAsync(admin.UserId, admin.Email, "metadata.model.updated", "model", model.Id, new { previous = prev, current = new { model.BrandId, model.Name, model.Slug } });
        return OkResponse(new { model.Id, model.BrandId, model.Name, model.Slug, model.IsActive }, "Model updated.");
    }

    [HttpPatch("models/{id:guid}/activation")]
    public async Task<IActionResult> SetModelActivation(Guid id, [FromBody] SetActivationRequest request)
    {
        var model = await dbContext.Models.SingleOrDefaultAsync(x => x.Id == id);
        if (model is null) return NotFoundResponse("Model not found.");

        var admin = GetAdminIdentity();
        model.IsActive = request.IsActive;
        await dbContext.SaveChangesAsync();

        await auditService.LogAsync(admin.UserId, admin.Email, "metadata.model.activation", "model", model.Id, new { model.IsActive });
        return OkResponse(new { model.Id, model.IsActive }, "Model status updated.");
    }

    private static (int page, int pageSize) NormalizePaging(int page, int pageSize)
    {
        var normalizedPage = page <= 0 ? 1 : page;
        var normalizedPageSize = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);
        return (normalizedPage, normalizedPageSize);
    }

    private IActionResult? ValidateSimpleRequest(UpsertSimpleMetadataRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequestResponse("Name is required.");
        }

        return null;
    }

    private IActionResult? ValidateCityRequest(UpsertCityRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequestResponse("Name is required.");
        }

        return null;
    }

    private async Task<IActionResult?> ValidateModelRequest(UpsertModelRequest request)
    {
        if (request is null || request.BrandId is null || request.BrandId == Guid.Empty || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequestResponse("BrandId and name are required.");
        }

        var brandExists = await dbContext.Brands.AnyAsync(x => x.Id == request.BrandId.Value);
        if (!brandExists)
        {
            return BadRequestResponse("Brand not found.");
        }

        return null;
    }

    private static string ResolveSlug(string name, string? slug)
    {
        var candidate = string.IsNullOrWhiteSpace(slug) ? name : slug;
        return Slugify(candidate);
    }

    private static string Slugify(string value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        var prevDash = false;

        foreach (var ch in normalized)
        {
            var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == System.Globalization.UnicodeCategory.NonSpacingMark) continue;

            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(ch);
                prevDash = false;
            }
            else if (!prevDash)
            {
                sb.Append('-');
                prevDash = true;
            }
        }

        var slug = sb.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? Guid.NewGuid().ToString("N") : slug;
    }

    public sealed class UpsertSimpleMetadataRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Slug { get; set; }
    }

    public sealed class UpsertCityRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Slug { get; set; }
        public string? Province { get; set; }
    }

    public sealed class UpsertModelRequest
    {
        public Guid? BrandId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Slug { get; set; }
    }

    public sealed class SetActivationRequest
    {
        public bool IsActive { get; set; }
    }

    public sealed class MetadataListQuery
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = DefaultPageSize;
        public string? Keyword { get; set; }
        public bool? IsActive { get; set; }
        public Guid? BrandId { get; set; }
    }

    public class SimpleMetadataItemResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public sealed class CityMetadataItemResponse : SimpleMetadataItemResponse
    {
        public string? Province { get; set; }
    }

    public sealed class ModelMetadataItemResponse : SimpleMetadataItemResponse
    {
        public Guid BrandId { get; set; }
        public string BrandName { get; set; } = string.Empty;
    }
}

