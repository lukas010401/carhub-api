using Microsoft.EntityFrameworkCore.Migrations;
using System.IO;

#nullable disable

namespace carhub.mg.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedInitialData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var sqlFilePath = Path.Combine(
                AppContext.BaseDirectory,
                "Infrastructure",
                "Persistence",
                "Migrations",
                "Scripts",
                "20260316123411_SeedInitialData.sql");

            if (!File.Exists(sqlFilePath))
            {
                sqlFilePath = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "Infrastructure",
                    "Persistence",
                    "Migrations",
                    "Scripts",
                    "20260316123411_SeedInitialData.sql");
            }

            if (!File.Exists(sqlFilePath))
            {
                throw new FileNotFoundException("Seed SQL file not found.", sqlFilePath);
            }

            migrationBuilder.Sql(File.ReadAllText(sqlFilePath));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM refresh_tokens WHERE \"Id\"::text LIKE '80000000-%';
                DELETE FROM listing_images WHERE \"Id\"::text LIKE '70000000-%';
                DELETE FROM listings WHERE \"Id\"::text LIKE '60000000-%';
                DELETE FROM models WHERE \"Id\"::text LIKE '50000000-%';
                DELETE FROM users WHERE \"Id\"::text LIKE '40000000-%';
                DELETE FROM cities WHERE \"Id\"::text LIKE '30000000-%';
                DELETE FROM categories WHERE \"Id\"::text LIKE '20000000-%';
                DELETE FROM brands WHERE \"Id\"::text LIKE '10000000-%';
                """);
        }
    }
}
