using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace carhub.mg.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeListingStatusToPublished : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE listings SET \"Status\" = 'Published', \"PublishedAt\" = COALESCE(\"PublishedAt\", CURRENT_TIMESTAMP) WHERE \"Status\" IN ('Approved', 'PendingReview');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE listings SET \"Status\" = 'Approved' WHERE \"Status\" = 'Published';");
        }
    }
}
