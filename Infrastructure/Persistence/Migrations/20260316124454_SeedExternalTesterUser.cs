using Microsoft.EntityFrameworkCore.Migrations;
using System.IO;

#nullable disable

namespace carhub.mg.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedExternalTesterUser : Migration
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
                "20260316124454_SeedExternalTesterUser.sql");

            if (!File.Exists(sqlFilePath))
            {
                sqlFilePath = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "Infrastructure",
                    "Persistence",
                    "Migrations",
                    "Scripts",
                    "20260316124454_SeedExternalTesterUser.sql");
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
                DELETE FROM users
                WHERE "Email" = 'external.tester@carhub.local'
                  AND "Id" = '90000000-0000-0000-0000-000000000001';
                """);
        }
    }
}
