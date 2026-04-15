using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace carhub.mg.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionExpiryReminderTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiryReminderEmailSentAtUtc",
                table: "professional_subscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiryReminderSmsSentAtUtc",
                table: "professional_subscriptions",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpiryReminderEmailSentAtUtc",
                table: "professional_subscriptions");

            migrationBuilder.DropColumn(
                name: "ExpiryReminderSmsSentAtUtc",
                table: "professional_subscriptions");
        }
    }
}
