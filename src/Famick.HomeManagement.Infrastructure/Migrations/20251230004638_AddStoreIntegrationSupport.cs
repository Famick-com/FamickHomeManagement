using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStoreIntegrationSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "external_chain_id",
                table: "shopping_locations",
                type: "character varying(100)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "external_location_id",
                table: "shopping_locations",
                type: "character varying(100)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "integration_type",
                table: "shopping_locations",
                type: "character varying(50)",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "latitude",
                table: "shopping_locations",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "longitude",
                table: "shopping_locations",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "oauth_access_token",
                table: "shopping_locations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "oauth_refresh_token",
                table: "shopping_locations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "oauth_token_expires_at",
                table: "shopping_locations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "store_address",
                table: "shopping_locations",
                type: "character varying(500)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "store_phone",
                table: "shopping_locations",
                type: "character varying(50)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "product_store_metadata",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shopping_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_product_id = table.Column<string>(type: "character varying(100)", nullable: true),
                    last_known_price = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    price_unit = table.Column<string>(type: "character varying(50)", nullable: true),
                    price_updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    aisle = table.Column<string>(type: "character varying(50)", nullable: true),
                    shelf = table.Column<string>(type: "character varying(50)", nullable: true),
                    department = table.Column<string>(type: "character varying(100)", nullable: true),
                    in_stock = table.Column<bool>(type: "boolean", nullable: true),
                    availability_checked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_store_metadata", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_store_metadata_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_product_store_metadata_shopping_locations_shopping_location~",
                        column: x => x.shopping_location_id,
                        principalTable: "shopping_locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_shopping_locations_tenant_integration_type",
                table: "shopping_locations",
                columns: new[] { "tenant_id", "integration_type" });

            migrationBuilder.CreateIndex(
                name: "IX_product_store_metadata_product_id",
                table: "product_store_metadata",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_product_store_metadata_shopping_location_id",
                table: "product_store_metadata",
                column: "shopping_location_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_store_metadata_tenant_id",
                table: "product_store_metadata",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_store_metadata_tenant_location",
                table: "product_store_metadata",
                columns: new[] { "tenant_id", "shopping_location_id" });

            migrationBuilder.CreateIndex(
                name: "ix_product_store_metadata_tenant_product",
                table: "product_store_metadata",
                columns: new[] { "tenant_id", "product_id" });

            migrationBuilder.CreateIndex(
                name: "ux_product_store_metadata_product_location",
                table: "product_store_metadata",
                columns: new[] { "tenant_id", "product_id", "shopping_location_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_store_metadata");

            migrationBuilder.DropIndex(
                name: "ix_shopping_locations_tenant_integration_type",
                table: "shopping_locations");

            migrationBuilder.DropColumn(
                name: "external_chain_id",
                table: "shopping_locations");

            migrationBuilder.DropColumn(
                name: "external_location_id",
                table: "shopping_locations");

            migrationBuilder.DropColumn(
                name: "integration_type",
                table: "shopping_locations");

            migrationBuilder.DropColumn(
                name: "latitude",
                table: "shopping_locations");

            migrationBuilder.DropColumn(
                name: "longitude",
                table: "shopping_locations");

            migrationBuilder.DropColumn(
                name: "oauth_access_token",
                table: "shopping_locations");

            migrationBuilder.DropColumn(
                name: "oauth_refresh_token",
                table: "shopping_locations");

            migrationBuilder.DropColumn(
                name: "oauth_token_expires_at",
                table: "shopping_locations");

            migrationBuilder.DropColumn(
                name: "store_address",
                table: "shopping_locations");

            migrationBuilder.DropColumn(
                name: "store_phone",
                table: "shopping_locations");
        }
    }
}
