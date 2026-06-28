using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAppMetadataAndMasterProductSeedKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "seed_key",
                table: "master_products",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            // Backfill the stable seed key for existing seed-owned rows from their
            // (unique) image slug so the first post-upgrade seeder run matches by key
            // instead of inserting duplicates. Only Seeded rows (source = 0) are part
            // of the seed catalog; tenant/admin rows keep a null seed_key.
            migrationBuilder.Sql(
                "UPDATE master_products SET seed_key = image_slug " +
                "WHERE source = 0 AND image_slug IS NOT NULL;");

            migrationBuilder.CreateTable(
                name: "app_metadata",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    value = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_metadata", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_master_products_seed_key",
                table: "master_products",
                column: "seed_key",
                unique: true,
                filter: "seed_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_app_metadata_key",
                table: "app_metadata",
                column: "key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "app_metadata");

            migrationBuilder.DropIndex(
                name: "ux_master_products_seed_key",
                table: "master_products");

            migrationBuilder.DropColumn(
                name: "seed_key",
                table: "master_products");
        }
    }
}
