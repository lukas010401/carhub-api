using Microsoft.EntityFrameworkCore.Migrations;
using System.IO;

#nullable disable

namespace carhub.mg.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedFullBrandModelCatalog : Migration
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
                "20260415160636_SeedFullBrandModelCatalog.sql");

            if (!File.Exists(sqlFilePath))
            {
                sqlFilePath = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "Infrastructure",
                    "Persistence",
                    "Migrations",
                    "Scripts",
                    "20260415160636_SeedFullBrandModelCatalog.sql");
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
            // Seed upsert only; no destructive rollback.
        }
    }
}
