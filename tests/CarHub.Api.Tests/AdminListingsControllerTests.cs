using CarHub.Api.Application.Contracts.Listings;
using CarHub.Api.Controllers;
using CarHub.Api.Domain.Entities;
using CarHub.Api.Domain.Enums;
using CarHub.Api.Infrastructure.Audit;
using CarHub.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Xunit;

namespace CarHub.Api.Tests;

public sealed class AdminListingsControllerTests
{
    [Fact]
    public async Task Approve_Pending_Listing_Changes_Status_And_PublishedAt()
    {
        await using var db = CreateDbContext();

        var listing = new Listing
        {
            SellerId = Guid.NewGuid(),
            BrandId = Guid.NewGuid(),
            ModelId = Guid.NewGuid(),
            Year = 2022,
            Price = 30000,
            Mileage = 10000,
            FuelType = FuelType.Gasoline,
            TransmissionType = TransmissionType.Automatic,
            CityId = Guid.NewGuid(),
            CategoryId = Guid.NewGuid(),
            Title = "Toyota Yaris",
            Description = "desc",
            PhoneNumber = "0340000000",
            Status = ListingStatus.PendingReview
        };

        db.Listings.Add(listing);
        await db.SaveChangesAsync();

        var controller = CreateController(db);

        var result = await controller.Approve(listing.Id);

        Assert.IsType<OkObjectResult>(result);
        var saved = await db.Listings.SingleAsync(x => x.Id == listing.Id);
        Assert.Equal(ListingStatus.Approved, saved.Status);
        Assert.NotNull(saved.PublishedAt);
        Assert.Null(saved.RejectionReason);
    }

    [Fact]
    public async Task Reject_Without_Reason_Returns_BadRequest()
    {
        await using var db = CreateDbContext();

        var listing = new Listing
        {
            SellerId = Guid.NewGuid(),
            BrandId = Guid.NewGuid(),
            ModelId = Guid.NewGuid(),
            Year = 2021,
            Price = 24000,
            Mileage = 30000,
            FuelType = FuelType.Diesel,
            TransmissionType = TransmissionType.Manual,
            CityId = Guid.NewGuid(),
            CategoryId = Guid.NewGuid(),
            Title = "Mazda CX-5",
            Description = "desc",
            PhoneNumber = "0340000001",
            Status = ListingStatus.PendingReview
        };

        db.Listings.Add(listing);
        await db.SaveChangesAsync();

        var controller = CreateController(db);

        var result = await controller.Reject(listing.Id, new RejectListingRequest { Reason = "   " });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Reject_Pending_Listing_Changes_Status_And_Sets_Reason()
    {
        await using var db = CreateDbContext();

        var listing = new Listing
        {
            SellerId = Guid.NewGuid(),
            BrandId = Guid.NewGuid(),
            ModelId = Guid.NewGuid(),
            Year = 2020,
            Price = 18000,
            Mileage = 50000,
            FuelType = FuelType.Gasoline,
            TransmissionType = TransmissionType.Manual,
            CityId = Guid.NewGuid(),
            CategoryId = Guid.NewGuid(),
            Title = "Suzuki Swift",
            Description = "desc",
            PhoneNumber = "0340000002",
            Status = ListingStatus.PendingReview,
            PublishedAt = DateTime.UtcNow
        };

        db.Listings.Add(listing);
        await db.SaveChangesAsync();

        var controller = CreateController(db);

        var result = await controller.Reject(listing.Id, new RejectListingRequest { Reason = "Informations incomplètes" });

        Assert.IsType<OkObjectResult>(result);
        var saved = await db.Listings.SingleAsync(x => x.Id == listing.Id);
        Assert.Equal(ListingStatus.Rejected, saved.Status);
        Assert.Equal("Informations incomplètes", saved.RejectionReason);
        Assert.Null(saved.PublishedAt);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static AdminListingsController CreateController(AppDbContext dbContext)
    {
        var controller = new AdminListingsController(dbContext, new NoopAuditService());

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Email, "admin@test.local"),
            new Claim(ClaimTypes.Role, UserRole.Admin.ToString())
        };

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
            }
        };

        return controller;
    }

    private sealed class NoopAuditService : IAdminAuditService
    {
        public Task LogAsync(
            Guid adminUserId,
            string adminEmail,
            string action,
            string entityType,
            Guid? entityId = null,
            object? details = null,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}



