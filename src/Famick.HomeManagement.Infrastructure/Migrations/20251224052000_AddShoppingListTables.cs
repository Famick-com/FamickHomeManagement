using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddShoppingListTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // =========================================================================
            // PHASE 2 BATCH 2: SHOPPING LISTS
            // =========================================================================
            //
            // Creates shopping list functionality with two tables:
            // 1. shopping_lists - Parent container for multiple shopping lists
            // 2. shopping_list - Individual items within shopping lists
            //
            // Features:
            // - Multiple shopping lists per tenant ("Default", "Weekly", "Party", etc.)
            // - Items can reference products OR be note-only (for ad-hoc items)
            // - Quantity tracking per item
            // - Enables "on shopping list" feature in stock overview
            //
            // Migration Source: Grocy 0049.sql
            // Complexity: Low (simple parent-child relationship)
            // =========================================================================

            // -------------------------------------------------------------------------
            // TABLE 1: shopping_lists
            // -------------------------------------------------------------------------
            // Purpose: Container for shopping lists (can have multiple per tenant)
            // Examples: "Default", "Weekly Shopping", "Party Supplies", "Hardware Store"
            // -------------------------------------------------------------------------
            migrationBuilder.CreateTable(
                name: "shopping_lists",
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
                    table.PrimaryKey("PK_shopping_lists", x => x.id);
                });

            // Indexes for shopping_lists
            migrationBuilder.CreateIndex(
                name: "ix_shopping_lists_tenant_id",
                table: "shopping_lists",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_shopping_lists_tenant_name",
                table: "shopping_lists",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            // -------------------------------------------------------------------------
            // TABLE 2: shopping_list
            // -------------------------------------------------------------------------
            // Purpose: Items in shopping lists (products to purchase with amounts)
            // Features:
            // - Can reference a product OR be note-only (product_id nullable)
            // - Amount tracks quantity to purchase
            // - Note field for special instructions or ad-hoc items
            // -------------------------------------------------------------------------
            migrationBuilder.CreateTable(
                name: "shopping_list",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shopping_list_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false, defaultValue: 0),
                    note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shopping_list", x => x.id);
                    table.ForeignKey(
                        name: "fk_shopping_list_shopping_lists",
                        column: x => x.shopping_list_id,
                        principalTable: "shopping_lists",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_shopping_list_products",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Indexes for shopping_list
            migrationBuilder.CreateIndex(
                name: "ix_shopping_list_tenant_id",
                table: "shopping_list",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_shopping_list_shopping_list_id",
                table: "shopping_list",
                column: "shopping_list_id");

            migrationBuilder.CreateIndex(
                name: "ix_shopping_list_product_id",
                table: "shopping_list",
                column: "product_id");

            // =========================================================================
            // PHASE 2 BATCH 2 COMPLETE
            // =========================================================================
            //
            // ✅ shopping_lists table created
            // ✅ shopping_list table created
            // ✅ All indexes and constraints created
            //
            // Features Enabled:
            // - Multiple shopping lists per tenant
            // - Product-based or note-only items
            // - Quantity tracking
            // - Foundation for shopping list views
            //
            // Impact:
            // - Can now track products on shopping lists
            // - uihelper_stock_current_overview can show on_shopping_list status
            //   (needs view update to use actual table instead of returning false)
            //
            // Next Steps:
            // 1. Update uihelper_stock_current_overview to check shopping_list table
            // 2. Create shopping list views (if needed from Grocy)
            // 3. Batch 3: Recipes tables
            //
            // Note: Default shopping list can be seeded via application startup
            // or migration data seed (optional).
            //
            // =========================================================================
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop tables in reverse dependency order
            migrationBuilder.DropTable(name: "shopping_list");
            migrationBuilder.DropTable(name: "shopping_lists");
        }
    }
}
