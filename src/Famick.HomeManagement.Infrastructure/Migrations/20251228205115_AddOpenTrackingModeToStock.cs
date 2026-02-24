using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOpenTrackingModeToStock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "open_tracking_mode",
                table: "stock",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "original_amount",
                table: "stock",
                type: "numeric(18,4)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "open_tracking_mode",
                table: "stock");

            migrationBuilder.DropColumn(
                name: "original_amount",
                table: "stock");
        }
    }
}
