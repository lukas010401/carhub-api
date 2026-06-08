using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace carhub.mg.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSiteVisitsCounter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "site_visits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Path = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_site_visits", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_site_visits_CreatedAt",
                table: "site_visits",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_site_visits_SessionKey",
                table: "site_visits",
                column: "SessionKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "site_visits");
        }
    }
}
