using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCacheExpirationToProductStoreMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ProductUrl",
                table: "product_store_metadata",
                newName: "product_url");

            migrationBuilder.AddColumn<DateTime>(
                name: "cache_expires_at",
                table: "product_store_metadata",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cache_expires_at",
                table: "product_store_metadata");

            migrationBuilder.RenameColumn(
                name: "product_url",
                table: "product_store_metadata",
                newName: "ProductUrl");
        }
    }
}
