using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChoresTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // =========================================================================
            // PHASE 2 BATCH 4: CHORES TABLES (FINAL PHASE 2 BATCH!)
            // =========================================================================
            //
            // Creates chore/task tracking functionality with two tables:
            // 1. chores - Periodic tasks/chores definition
            // 2. chores_log - Chore execution history (completed, skipped, undone)
            //
            // Features:
            // - Periodic chore scheduling (manually, daily, weekly, monthly, dynamic-regular)
            // - User assignment (round-robin, specific users)
            // - Product consumption on execution (e.g., consume cleaning supplies when chore is done)
            // - Undo/skip functionality
            // - Execution history tracking
            //
            // Migration Source: Grocy 0010.sql (habits) → 0035.sql (renamed to chores) + enhancements
            // Complexity: Medium (many optional fields, assignment patterns)
            // =========================================================================

            // -------------------------------------------------------------------------
            // TABLE 1: chores
            // -------------------------------------------------------------------------
            // Purpose: Define periodic tasks/chores
            // Examples: "Water plants", "Clean kitchen", "Check smoke detectors", "Feed pets"
            // Features:
            // - Period types: manually, dynamic-regular, daily, weekly, monthly
            // - User assignment configuration
            // - Optional product consumption on execution
            // - Rollover and date-only tracking options
            // -------------------------------------------------------------------------
            migrationBuilder.CreateTable(
                name: "chores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    period_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    period_days = table.Column<int>(type: "integer", nullable: true),
                    track_date_only = table.Column<short>(type: "smallint", nullable: false, defaultValue: 0),
                    rollover = table.Column<short>(type: "smallint", nullable: false, defaultValue: 0),
                    assignment_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    assignment_config = table.Column<string>(type: "text", nullable: true),
                    next_execution_assigned_to_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    consume_product_on_execution = table.Column<short>(type: "smallint", nullable: false, defaultValue: 0),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    product_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chores", x => x.id);
                    table.ForeignKey(
                        name: "fk_chores_products",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_chores_users",
                        column: x => x.next_execution_assigned_to_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            // Indexes for chores
            migrationBuilder.CreateIndex(
                name: "ix_chores_tenant_id",
                table: "chores",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_chores_tenant_name",
                table: "chores",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            // -------------------------------------------------------------------------
            // TABLE 2: chores_log
            // -------------------------------------------------------------------------
            // Purpose: Chore execution history
            // Features:
            // - Track when chores were completed (tracked_time)
            // - Track who completed them (done_by_user_id)
            // - Support undo operations (undone, undone_timestamp)
            // - Support skip operations (skipped)
            // - Track scheduled vs actual execution time
            // -------------------------------------------------------------------------
            migrationBuilder.CreateTable(
                name: "chores_log",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chore_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tracked_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    done_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    undone = table.Column<short>(type: "smallint", nullable: false, defaultValue: 0),
                    undone_timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    skipped = table.Column<short>(type: "smallint", nullable: false, defaultValue: 0),
                    scheduled_execution_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chores_log", x => x.id);
                    table.ForeignKey(
                        name: "fk_chores_log_chores",
                        column: x => x.chore_id,
                        principalTable: "chores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_chores_log_users",
                        column: x => x.done_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            // Indexes for chores_log
            migrationBuilder.CreateIndex(
                name: "ix_chores_log_tenant_id",
                table: "chores_log",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_chores_log_chore_id",
                table: "chores_log",
                column: "chore_id");

            migrationBuilder.CreateIndex(
                name: "ix_chores_log_done_by_user_id",
                table: "chores_log",
                column: "done_by_user_id");

            // =========================================================================
            // PHASE 2 COMPLETE! 🎉
            // =========================================================================
            //
            // ✅ chores table created
            // ✅ chores_log table created
            // ✅ All indexes and constraints created
            //
            // Features Enabled:
            // - Periodic chore tracking (manually, daily, weekly, monthly, dynamic-regular)
            // - User assignment with configuration (round-robin, specific users)
            // - Product consumption on execution (consume cleaning supplies, etc.)
            // - Execution history with undo/skip support
            // - Scheduled vs actual execution time tracking
            //
            // Field Notes:
            // - period_type: 'manually', 'dynamic-regular', 'daily', 'weekly', 'monthly'
            // - period_days: Days for dynamic-regular, day-of-month for monthly
            // - track_date_only: Track only date, not time (smallint: 0 or 1)
            // - rollover: Roll over to current day if not completed (smallint: 0 or 1)
            // - assignment_type: How chores are assigned (e.g., 'round-robin', 'specific-user')
            // - assignment_config: JSON/CSV of assigned user IDs
            // - consume_product_on_execution: Consume product stock when done (smallint: 0 or 1)
            // - undone: Whether this log entry was reversed (smallint: 0 or 1)
            // - skipped: Whether the chore was skipped instead of completed (smallint: 0 or 1)
            //
            // Phase 2 Batch Summary:
            // ✅ Batch 1: product_groups, shopping_locations
            // ✅ Batch 2: shopping_lists, shopping_list
            // ✅ Batch 3: recipes, recipes_pos, recipes_nestings
            // ✅ Batch 4: chores, chores_log
            //
            // Total Phase 2 Tables: 11 tables created
            // Total Database Tables: 22 tables (Phase 1: 11 + Phase 2: 11)
            // Total Database Views: 21 views (all Phase 1)
            //
            // Next Steps:
            // 1. Implement ChoresService with scheduling logic
            // 2. Implement assignment logic (round-robin, specific users)
            // 3. Create chore views (if needed from Grocy)
            // 4. Begin service layer implementation for all Phase 2 features
            // 5. Phase 3: API Controllers (UsersController, ProductsController, StockController, etc.)
            //
            // =========================================================================
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop tables in reverse dependency order
            migrationBuilder.DropTable(name: "chores_log");
            migrationBuilder.DropTable(name: "chores");
        }
    }
}
