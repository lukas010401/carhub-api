using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace carhub.mg.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddManualPaymentsModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "manual_payment_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ListingId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Provider = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ReceiverNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    InternalReference = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ExpectedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RequestedMonths = table.Column<int>(type: "integer", nullable: true),
                    RequestedMonthlyPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedByAdminId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProviderTransactionReference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    SenderNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    SenderName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    PaidAtLocal = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ProofFileUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ProofFileHash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReviewNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manual_payment_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_manual_payment_requests_listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_manual_payment_requests_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "manual_payment_decisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_manual_payment_decisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_manual_payment_decisions_manual_payment_requests_PaymentReq~",
                        column: x => x.PaymentRequestId,
                        principalTable: "manual_payment_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_manual_payment_decisions_users_AdminUserId",
                        column: x => x.AdminUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_manual_payment_decisions_AdminUserId",
                table: "manual_payment_decisions",
                column: "AdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_manual_payment_decisions_PaymentRequestId",
                table: "manual_payment_decisions",
                column: "PaymentRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_manual_payment_requests_InternalReference",
                table: "manual_payment_requests",
                column: "InternalReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_manual_payment_requests_ListingId",
                table: "manual_payment_requests",
                column: "ListingId");

            migrationBuilder.CreateIndex(
                name: "IX_manual_payment_requests_ProviderTransactionReference",
                table: "manual_payment_requests",
                column: "ProviderTransactionReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_manual_payment_requests_Status",
                table: "manual_payment_requests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_manual_payment_requests_Type",
                table: "manual_payment_requests",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_manual_payment_requests_UserId",
                table: "manual_payment_requests",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "manual_payment_decisions");

            migrationBuilder.DropTable(
                name: "manual_payment_requests");
        }
    }
}
