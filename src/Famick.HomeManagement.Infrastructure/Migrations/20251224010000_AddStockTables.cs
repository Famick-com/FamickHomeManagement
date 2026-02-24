using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStockTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // =========================================================================
            // STOCK AND STOCK_LOG TABLES - GROCY TO POSTGRESQL MIGRATION
            // =========================================================================
            //
            // Creates the core inventory management tables:
            // 1. stock - Current stock entries (batches) for products
            // 2. stock_log - Complete audit trail of all stock transactions
            //
            // These tables enable:
            // - FIFO/FEFO inventory management
            // - Best before date tracking
            // - Stock entry location tracking
            // - Complete transaction history
            // - Price tracking per batch
            // - Opened/unopened tracking
            //
            // Migration Source: Grocy database schema (stock and stock_log tables)
            // =========================================================================

            // -------------------------------------------------------------------------
            // TABLE 1: stock
            // -------------------------------------------------------------------------
            // Current stock entries - each row represents a specific batch of a product
            // -------------------------------------------------------------------------
            migrationBuilder.CreateTable(
                name: "stock",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    best_before_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    purchased_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    stock_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    price = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    open = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    opened_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    shopping_location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock", x => x.id);
                    table.ForeignKey(
                        name: "fk_stock_products",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_locations",
                        column: x => x.location_id,
                        principalTable: "locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            // Indexes for stock table
            migrationBuilder.CreateIndex(
                name: "ix_stock_tenant_id",
                table: "stock",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_performance1",
                table: "stock",
                columns: new[] { "product_id", "open", "best_before_date", "amount" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_stock_id",
                table: "stock",
                column: "stock_id");

            migrationBuilder.CreateIndex(
                name: "ux_stock_tenant_stock_id",
                table: "stock",
                columns: new[] { "tenant_id", "stock_id" },
                unique: true);

            // -------------------------------------------------------------------------
            // TABLE 2: stock_log
            // -------------------------------------------------------------------------
            // Complete audit trail of all stock transactions
            // Transaction types: purchase, consume, inventory-correction, product-opened,
            //                    stock-edit-new, stock-edit-old, self-production
            // -------------------------------------------------------------------------
            migrationBuilder.CreateTable(
                name: "stock_log",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    best_before_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    purchased_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    used_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    spoiled = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    stock_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    transaction_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    price = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    undone = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    undone_timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    opened_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    recipe_id = table.Column<Guid>(type: "uuid", nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    transaction_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    stock_row_id = table.Column<Guid>(type: "uuid", nullable: true),
                    shopping_location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_log", x => x.id);
                    table.ForeignKey(
                        name: "fk_stock_log_products",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_log_locations",
                        column: x => x.location_id,
                        principalTable: "locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_stock_log_users",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_log_stock",
                        column: x => x.stock_row_id,
                        principalTable: "stock",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            // Indexes for stock_log table
            migrationBuilder.CreateIndex(
                name: "ix_stock_log_tenant_id",
                table: "stock_log",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_log_performance1",
                table: "stock_log",
                columns: new[] { "stock_id", "transaction_type", "amount" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_log_performance2",
                table: "stock_log",
                columns: new[] { "product_id", "best_before_date", "purchased_date", "transaction_type", "stock_id", "undone" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_log_correlation_id",
                table: "stock_log",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_log_transaction_id",
                table: "stock_log",
                column: "transaction_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_log_user_id",
                table: "stock_log",
                column: "user_id");

            // =========================================================================
            // STOCK TABLES COMPLETE
            // =========================================================================
            //
            // ✅ stock table created
            // ✅ stock_log table created
            // ✅ All indexes created for performance
            // ✅ Foreign key constraints configured
            // ✅ Multi-tenancy support (TenantId columns + indexes)
            //
            // Next Steps:
            // 1. Create Batch 2 database views (stock_current, stock_missing_products, etc.)
            // 2. Implement stock service layer
            // 3. Create stock API endpoints
            //
            // Note: Grocy triggers not migrated yet - will be handled separately
            // - Stock triggers maintain cache tables (products_average_price, products_last_purchased)
            // - Decision pending: Use triggers vs C# event handlers
            //
            // =========================================================================
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "stock_log");
            migrationBuilder.DropTable(name: "stock");
        }
    }
}
