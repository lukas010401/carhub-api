using CarHub.Api.Domain.Entities;
using CarHub.Api.Domain.Enums;
using CarHub.Api.Infrastructure.Persistence;
using CarHub.Api.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace CarHub.Api.Infrastructure.Seed;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext dbContext, IConfiguration configuration, IPasswordService passwordService)
    {
        if (!await dbContext.Brands.AnyAsync())
        {
            var toyota = new Brand { Name = "Toyota", Slug = "toyota" };
            var nissan = new Brand { Name = "Nissan", Slug = "nissan" };
            dbContext.Brands.AddRange(toyota, nissan);

            dbContext.Models.AddRange(
                new VehicleModel { Name = "RAV4", Slug = "rav4", Brand = toyota },
                new VehicleModel { Name = "Hilux", Slug = "hilux", Brand = toyota },
                new VehicleModel { Name = "Navara", Slug = "navara", Brand = nissan },
                new VehicleModel { Name = "X-Trail", Slug = "x-trail", Brand = nissan });
        }

        if (!await dbContext.Cities.AnyAsync())
        {
            dbContext.Cities.AddRange(
                new City { Name = "Antananarivo", Slug = "antananarivo", Province = "Analamanga" },
                new City { Name = "Toamasina", Slug = "toamasina", Province = "Atsinanana" },
                new City { Name = "Mahajanga", Slug = "mahajanga", Province = "Boeny" });
        }

        if (!await dbContext.Categories.AnyAsync())
        {
            dbContext.Categories.AddRange(
                new Category { Name = "SUV", Slug = "suv" },
                new Category { Name = "Pickup", Slug = "pickup" },
                new Category { Name = "Citadine", Slug = "citadine" },
                new Category { Name = "4x4", Slug = "4x4" });
        }

        var adminEmail = configuration["AdminSeed:Email"] ?? "admin@carmada.local";
        var adminPassword = configuration["AdminSeed:Password"] ?? "Admin123!";

        if (!await dbContext.Users.AnyAsync(x => x.Email == adminEmail))
        {
            dbContext.Users.Add(new User
            {
                Email = adminEmail,
                PasswordHash = passwordService.Hash(adminPassword),
                FullName = "Platform Administrator",
                PhoneNumber = "000000000",
                Role = UserRole.Admin,
                AccountType = AccountType.Individual,
                IsActive = true
            });
        }

        await dbContext.SaveChangesAsync();
    }
}

