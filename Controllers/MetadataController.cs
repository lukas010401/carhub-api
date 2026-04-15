using CarHub.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarHub.Api.Controllers;

[ApiController]
[Route("api/metadata")]
public sealed class MetadataController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet("brands")]
    public async Task<IActionResult> GetBrands()
    {
        var items = await dbContext.Brands
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Slug,
                LogoUrl = $"/brand-logos/{x.Slug}.svg"
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("brands/{brandId:guid}/models")]
    public async Task<IActionResult> GetModels(Guid brandId)
    {
        var items = await dbContext.Models
            .AsNoTracking()
            .Where(x => x.IsActive && x.BrandId == brandId)
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Name, x.Slug, x.BrandId })
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("cities")]
    public async Task<IActionResult> GetCities()
    {
        var items = await dbContext.Cities
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Name, x.Slug, x.Province })
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        var items = await dbContext.Categories
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Name, x.Slug })
            .ToListAsync();

        return Ok(items);
    }
}
