using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLocationAndCategoryToStorageBin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "category",
                table: "storage_bins",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "location_id",
                table: "storage_bins",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_storage_bins_location_id",
                table: "storage_bins",
                column: "location_id");

            migrationBuilder.CreateIndex(
                name: "ix_storage_bins_tenant_category",
                table: "storage_bins",
                columns: new[] { "tenant_id", "category" });

            migrationBuilder.CreateIndex(
                name: "ix_storage_bins_tenant_location",
                table: "storage_bins",
                columns: new[] { "tenant_id", "location_id" });

            migrationBuilder.AddForeignKey(
                name: "FK_storage_bins_locations_location_id",
                table: "storage_bins",
                column: "location_id",
                principalTable: "locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_storage_bins_locations_location_id",
                table: "storage_bins");

            migrationBuilder.DropIndex(
                name: "IX_storage_bins_location_id",
                table: "storage_bins");

            migrationBuilder.DropIndex(
                name: "ix_storage_bins_tenant_category",
                table: "storage_bins");

            migrationBuilder.DropIndex(
                name: "ix_storage_bins_tenant_location",
                table: "storage_bins");

            migrationBuilder.DropColumn(
                name: "category",
                table: "storage_bins");

            migrationBuilder.DropColumn(
                name: "location_id",
                table: "storage_bins");
        }
    }
}
