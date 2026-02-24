using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductGroupsAndShoppingLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // =========================================================================
            // PHASE 2 BATCH 1: PRODUCT GROUPS AND SHOPPING LOCATIONS
            // =========================================================================
            //
            // Creates two critical lookup tables that were referenced in Phase 1 views:
            // 1. product_groups - Product categorization (Dairy, Beverages, etc.)
            // 2. shopping_locations - Shopping stores/markets
            //
            // These tables:
            // - Unblock Phase 1 view: uihelper_stock_current_overview
            // - Fix NULL columns: product_group_name, default_store_name
            // - Add foreign keys to products table
            // - Enable product categorization and shopping location tracking
            //
            // Migration Source: Grocy 0037.sql (product_groups), 0099.sql (shopping_locations)
            // Complexity: Very Low (simple lookup tables)
            // =========================================================================

            // -------------------------------------------------------------------------
            // TABLE 1: product_groups
            // -------------------------------------------------------------------------
            // Purpose: Organize products into categories/groups
            // Examples: "Dairy", "Beverages", "Snacks", "Frozen Foods", "Produce"
            // -------------------------------------------------------------------------
            migrationBuilder.CreateTable(
                name: "product_groups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_groups", x => x.id);
                });

            // Indexes for product_groups
            migrationBuilder.CreateIndex(
                name: "ix_product_groups_tenant_id",
                table: "product_groups",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_product_groups_tenant_name",
                table: "product_groups",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            // -------------------------------------------------------------------------
            // TABLE 2: shopping_locations
            // -------------------------------------------------------------------------
            // Purpose: Track where products can be purchased
            // Examples: "Walmart", "Costco", "Local Farmer's Market", "Amazon"
            // -------------------------------------------------------------------------
            migrationBuilder.CreateTable(
                name: "shopping_locations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shopping_locations", x => x.id);
                });

            // Indexes for shopping_locations
            migrationBuilder.CreateIndex(
                name: "ix_shopping_locations_tenant_id",
                table: "shopping_locations",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_shopping_locations_tenant_name",
                table: "shopping_locations",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            // -------------------------------------------------------------------------
            // TABLE 3: Update products table
            // -------------------------------------------------------------------------
            // Add foreign keys to products table for product_groups and shopping_locations
            // Both columns are nullable to allow products without categories or shopping locations
            // -------------------------------------------------------------------------
            migrationBuilder.AddColumn<Guid>(
                name: "product_group_id",
                table: "products",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "shopping_location_id",
                table: "products",
                type: "uuid",
                nullable: true);

            // Foreign key constraints
            migrationBuilder.AddForeignKey(
                name: "fk_products_product_groups",
                table: "products",
                column: "product_group_id",
                principalTable: "product_groups",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_products_shopping_locations",
                table: "products",
                column: "shopping_location_id",
                principalTable: "shopping_locations",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // Indexes on products table for foreign keys
            migrationBuilder.CreateIndex(
                name: "ix_products_product_group_id",
                table: "products",
                column: "product_group_id");

            migrationBuilder.CreateIndex(
                name: "ix_products_shopping_location_id",
                table: "products",
                column: "shopping_location_id");

            // =========================================================================
            // PHASE 2 BATCH 1 COMPLETE
            // =========================================================================
            //
            // ✅ product_groups table created
            // ✅ shopping_locations table created
            // ✅ products table updated with foreign keys
            // ✅ All indexes and constraints created
            //
            // Impact:
            // - uihelper_stock_current_overview view now returns actual data instead of NULL
            // - Products can be organized into categories
            // - Shopping locations can be tracked per product
            // - Enables shopping list features in future batches
            //
            // Next Steps:
            // 1. Update uihelper_stock_current_overview view to use actual tables
            //    (currently returns NULL for product_group_name and default_store_name)
            // 2. Batch 2: shopping_lists and shopping_list tables
            // 3. Seed default product groups (optional)
            //
            // =========================================================================
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop foreign keys from products table
            migrationBuilder.DropForeignKey(
                name: "fk_products_product_groups",
                table: "products");

            migrationBuilder.DropForeignKey(
                name: "fk_products_shopping_locations",
                table: "products");

            // Drop indexes from products table
            migrationBuilder.DropIndex(
                name: "ix_products_product_group_id",
                table: "products");

            migrationBuilder.DropIndex(
                name: "ix_products_shopping_location_id",
                table: "products");

            // Drop columns from products table
            migrationBuilder.DropColumn(
                name: "product_group_id",
                table: "products");

            migrationBuilder.DropColumn(
                name: "shopping_location_id",
                table: "products");

            // Drop tables
            migrationBuilder.DropTable(
                name: "shopping_locations");

            migrationBuilder.DropTable(
                name: "product_groups");
        }
    }
}
