using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStockManagementViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // =========================================================================
            // BATCH 2: STOCK MANAGEMENT VIEWS - GROCY TO POSTGRESQL MIGRATION
            // =========================================================================
            //
            // Adds 5 critical stock management views that enable:
            // - Current stock tracking with aggregation
            // - Below minimum stock alerts
            // - FIFO/FEFO consumption logic
            // - Shelf life tracking
            // - Stock entry edit history
            //
            // These views depend on:
            // - stock table (Migration 20251224010000)
            // - stock_log table (Migration 20251224010000)
            // - products_view (Migration 20251224000000)
            // - products_resolved (Migration 20251224000000)
            // - quantity_unit_conversions_resolved (Migration 20251224000000)
            //
            // SQLite to PostgreSQL conversions applied:
            // - IFNULL() → COALESCE()
            // - JULIANDAY() → EXTRACT(DAY FROM timestamp difference)
            // - ROUND() with explicit CAST to NUMERIC
            // - DROP VIEW → DROP VIEW IF EXISTS CASCADE
            //
            // View creation order follows dependency chain:
            // 1. stock_edited_entries (base view)
            // 2. stock_average_product_shelf_life (depends on #1)
            // 3. stock_current (base view)
            // 4. stock_next_use (base view)
            // 5. stock_missing_products (depends on #3)
            //
            // =========================================================================

            // -------------------------------------------------------------------------
            // VIEW 1: stock_edited_entries
            // -------------------------------------------------------------------------
            // Purpose: Track manually edited stock entries
            // Returns stock_id's which have been edited manually and recalculates origin amounts
            // Complexity: COMPLEX - Nested subqueries with temporal logic
            // Migration Source: Grocy 0230.sql
            // -------------------------------------------------------------------------
            migrationBuilder.Sql(@"
                CREATE VIEW stock_edited_entries AS
                /*
                    Returns stock_id's which have been edited manually
                */
                SELECT
                    x.stock_id,
                    x.stock_log_id_of_newest_edited_entry,

                    -- When an origin entry was edited, the new origin amount is the one of the newest 'stock-edit-new' + all
                    -- previous consume transactions (mind that consume transaction amounts are negative, hence here - instead of +)
                    (
                        SELECT amount
                        FROM stock_log sli
                        WHERE sli.id = x.stock_log_id_of_newest_edited_entry
                    )
                    -
                    COALESCE((
                        SELECT SUM(amount)
                        FROM stock_log sli_consumed
                        WHERE sli_consumed.stock_id = x.stock_id
                            AND sli_consumed.transaction_type IN ('consume', 'inventory-correction')
                            AND sli_consumed.id < x.stock_log_id_of_newest_edited_entry
                            AND sli_consumed.amount < 0
                            AND sli_consumed.undone = false), 0) AS edited_origin_amount
                FROM (
                    SELECT
                        sl_add.stock_id,
                        MAX(sl_edit.id) AS stock_log_id_of_newest_edited_entry
                    FROM stock_log sl_add
                    JOIN stock_log sl_edit
                        ON sl_add.stock_id = sl_edit.stock_id
                        AND sl_edit.transaction_type = 'stock-edit-new'
                    WHERE sl_add.transaction_type IN ('purchase', 'inventory-correction', 'self-production')
                        AND sl_add.amount > 0
                    GROUP BY sl_add.stock_id
                ) x
                JOIN stock_log sl_edit
                    ON x.stock_log_id_of_newest_edited_entry = sl_edit.id;
            ");

            // -------------------------------------------------------------------------
            // VIEW 2: stock_average_product_shelf_life
            // -------------------------------------------------------------------------
            // Purpose: Calculate average shelf life per product (purchase → best before)
            // Returns -1 if no data available
            // Complexity: MEDIUM - Date calculations with aggregation
            // Migration Source: Grocy 0194.sql
            // SQLite Change: JULIANDAY() → EXTRACT(DAY FROM timestamp difference)
            // -------------------------------------------------------------------------
            migrationBuilder.Sql(@"
                CREATE VIEW stock_average_product_shelf_life AS
                SELECT
                    p.id,
                    CASE WHEN x.product_id IS NULL THEN -1 ELSE AVG(x.shelf_life_days) END AS average_shelf_life_days
                FROM products p
                LEFT JOIN (
                    SELECT
                        sl_p.product_id,
                        EXTRACT(DAY FROM (sl_p.best_before_date::timestamp - sl_p.purchased_date::timestamp)) AS shelf_life_days
                    FROM stock_log sl_p
                    WHERE sl_p.undone = false
                        AND (
                            (sl_p.transaction_type IN ('purchase', 'inventory-correction', 'self-production') AND sl_p.stock_id NOT IN (SELECT stock_id FROM stock_edited_entries))
                            OR (sl_p.transaction_type = 'stock-edit-new' AND sl_p.stock_id IN (SELECT stock_id FROM stock_edited_entries))
                        )
                ) x
                    ON p.id = x.product_id
                GROUP BY p.id;
            ");

            // -------------------------------------------------------------------------
            // VIEW 3: stock_current
            // -------------------------------------------------------------------------
            // Purpose: Current stock levels per product with parent/sub-product aggregation
            // Includes separate amounts for parent vs aggregated sub-products
            // Complexity: VERY COMPLEX - Multiple UNIONs with quantity unit conversions
            // Migration Source: Grocy 0233.sql
            // -------------------------------------------------------------------------
            migrationBuilder.Sql(@"
                CREATE VIEW stock_current AS
                SELECT
                    pr.parent_product_id AS product_id,
                    COALESCE((SELECT SUM(amount) FROM stock WHERE product_id = pr.parent_product_id), 0) AS amount,
                    SUM(s.amount * COALESCE(qucr.factor, 1.0)) AS amount_aggregated,
                    COALESCE(ROUND(CAST((SELECT SUM(COALESCE(price, 0) * amount) FROM stock WHERE product_id = pr.parent_product_id) AS NUMERIC), 2), 0) AS value,
                    MIN(s.best_before_date) AS best_before_date,
                    COALESCE((SELECT SUM(amount) FROM stock WHERE product_id = pr.parent_product_id AND open = true), 0) AS amount_opened,
                    COALESCE((SELECT SUM(amount) FROM stock WHERE product_id IN (SELECT sub_product_id FROM products_resolved WHERE parent_product_id = pr.parent_product_id) AND open = true), 0) * COALESCE(qucr.factor, 1) AS amount_opened_aggregated,
                    CASE WHEN COUNT(p_sub.parent_product_id) > 0 THEN 1 ELSE 0 END AS is_aggregated_amount,
                    MAX(p_parent.due_type) AS due_type
                FROM products_resolved pr
                JOIN stock s
                    ON pr.sub_product_id = s.product_id
                JOIN products p_parent
                    ON pr.parent_product_id = p_parent.id
                    AND p_parent.active = true
                JOIN products p_sub
                    ON pr.sub_product_id = p_sub.id
                    AND p_sub.active = true
                LEFT JOIN quantity_unit_conversions_resolved qucr
                    ON pr.sub_product_id = qucr.product_id
                    AND p_sub.qu_id_stock = qucr.from_qu_id
                    AND p_parent.qu_id_stock = qucr.to_qu_id
                GROUP BY pr.parent_product_id
                HAVING SUM(s.amount) > 0

                UNION

                -- This is the same as above but sub products not rolled up (no QU conversion and column is_aggregated_amount = 0 here)
                SELECT
                    pr.sub_product_id AS product_id,
                    SUM(s.amount) AS amount,
                    SUM(s.amount) AS amount_aggregated,
                    ROUND(CAST(SUM(COALESCE(s.price, 0) * s.amount) AS NUMERIC), 2) AS value,
                    MIN(s.best_before_date) AS best_before_date,
                    COALESCE((SELECT SUM(amount) FROM stock WHERE product_id = s.product_id AND open = true), 0) AS amount_opened,
                    COALESCE((SELECT SUM(amount) FROM stock WHERE product_id = s.product_id AND open = true), 0) AS amount_opened_aggregated,
                    0 AS is_aggregated_amount,
                    MAX(p_sub.due_type) AS due_type
                FROM products_resolved pr
                JOIN stock s
                    ON pr.sub_product_id = s.product_id
                JOIN products p_sub
                    ON pr.sub_product_id = p_sub.id
                    AND p_sub.active = true
                WHERE pr.parent_product_id != pr.sub_product_id
                GROUP BY pr.sub_product_id
                HAVING SUM(s.amount) > 0;
            ");

            // -------------------------------------------------------------------------
            // VIEW 4: stock_next_use
            // -------------------------------------------------------------------------
            // Purpose: Determine which stock entry to consume next (FIFO/FEFO logic)
            // Priority: default location → opened → earliest best before → FIFO
            // Complexity: MEDIUM - ROW_NUMBER() with complex ordering
            // Migration Source: Grocy 0200.sql
            // Note: Original SQLite INSTEAD OF triggers not migrated (handle in app code)
            // -------------------------------------------------------------------------
            migrationBuilder.Sql(@"
                CREATE VIEW stock_next_use AS
                /*
                    The default consume rule is:
                    Opened first, then first due first, then first in first out
                    Apart from that products at their default consume location should be consumed first

                    This orders the stock entries by that
                    => Highest 'priority' per product = the stock entry to use next
                    => ORDER BY clause = ORDER BY priority DESC, open DESC, best_before_date ASC, purchased_date ASC
                */
                SELECT
                    (ROW_NUMBER() OVER(PARTITION BY s.product_id ORDER BY CASE WHEN COALESCE(p.default_consume_location_id::text, '-1') = s.location_id::text THEN 0 ELSE 1 END ASC, s.open DESC, s.best_before_date ASC, s.purchased_date ASC)) * -1 AS priority,
                    s.*
                FROM stock s
                JOIN products p
                    ON p.id = s.product_id
                ORDER BY CASE WHEN COALESCE(p.default_consume_location_id::text, '-1') = s.location_id::text THEN 0 ELSE 1 END ASC, s.open DESC, s.best_before_date ASC, s.purchased_date ASC;
            ");

            // -------------------------------------------------------------------------
            // VIEW 5: stock_missing_products
            // -------------------------------------------------------------------------
            // Purpose: Products below minimum stock levels
            // Handles three scenarios: no subs, with subs (cumulated), with subs (not cumulated)
            // Complexity: VERY COMPLEX - Multiple UNIONs with different aggregation rules
            // Migration Source: Grocy 0215.sql
            // -------------------------------------------------------------------------
            migrationBuilder.Sql(@"
                CREATE VIEW stock_missing_products AS
                SELECT *
                FROM (
                    -- Products WITHOUT sub products where the amount of the sub products SHOULD NOT be cumulated
                    SELECT
                        p.id,
                        MAX(p.name) AS name,
                        p.min_stock_amount - COALESCE(SUM(s.amount), 0) + (CASE WHEN p.treat_opened_as_out_of_stock = true THEN COALESCE(SUM(s.amount_opened), 0) ELSE 0 END) AS amount_missing,
                        CASE WHEN COALESCE(SUM(s.amount), 0) > 0 THEN 1 ELSE 0 END AS is_partly_in_stock
                    FROM products_view p
                    LEFT JOIN stock_current s
                        ON p.id = s.product_id
                    WHERE p.min_stock_amount != 0
                        AND p.cumulate_min_stock_amount_of_sub_products = false
                        AND p.has_sub_products = 0
                        AND p.parent_product_id IS NULL
                        AND COALESCE(p.active, false) = true
                    GROUP BY p.id

                    UNION

                    -- Parent products WITH sub products where the amount of the sub products SHOULD be cumulated
                    SELECT
                        p.id,
                        MAX(p.name) AS name,
                        SUM(sub_p.min_stock_amount) - COALESCE(SUM(s.amount_aggregated), 0) + (CASE WHEN p.treat_opened_as_out_of_stock = true THEN COALESCE(SUM(s.amount_opened_aggregated), 0) ELSE 0 END) AS amount_missing,
                        CASE WHEN COALESCE(SUM(s.amount), 0) > 0 THEN 1 ELSE 0 END AS is_partly_in_stock
                    FROM products_view p
                    JOIN products_resolved pr
                        ON p.id = pr.parent_product_id
                    JOIN products sub_p
                        ON pr.sub_product_id = sub_p.id
                    LEFT JOIN stock_current s
                        ON pr.sub_product_id = s.product_id
                    WHERE sub_p.min_stock_amount != 0
                        AND p.cumulate_min_stock_amount_of_sub_products = true
                        AND COALESCE(p.active, false) = true
                    GROUP BY p.id

                    UNION

                    -- Sub products where the amount SHOULD NOT be cumulated into the parent product
                    SELECT
                        sub_p.id,
                        MAX(sub_p.name) AS name,
                        SUM(sub_p.min_stock_amount) - COALESCE(SUM(s.amount_aggregated), 0) + (CASE WHEN p.treat_opened_as_out_of_stock = true THEN COALESCE(SUM(s.amount_opened_aggregated), 0) ELSE 0 END) AS amount_missing,
                        CASE WHEN COALESCE(SUM(s.amount), 0) > 0 THEN 1 ELSE 0 END AS is_partly_in_stock
                    FROM products p
                    JOIN products_resolved pr
                        ON p.id = pr.parent_product_id
                    JOIN products sub_p
                        ON pr.sub_product_id = sub_p.id
                    LEFT JOIN stock_current s
                        ON pr.sub_product_id = s.product_id
                    WHERE sub_p.min_stock_amount != 0
                        AND p.cumulate_min_stock_amount_of_sub_products = false
                        AND COALESCE(p.active, false) = true
                    GROUP BY sub_p.id
                ) x
                WHERE x.amount_missing > 0;
            ");

            // =========================================================================
            // BATCH 2 STOCK MANAGEMENT VIEWS COMPLETE
            // =========================================================================
            //
            // ✅ stock_edited_entries - Edit history tracking
            // ✅ stock_average_product_shelf_life - Shelf life calculations
            // ✅ stock_current - Current stock with aggregation
            // ✅ stock_next_use - FIFO/FEFO consumption priority
            // ✅ stock_missing_products - Below min stock alerts
            //
            // These views enable core inventory management functionality:
            // - Real-time stock level tracking
            // - Parent/sub-product aggregation with quantity unit conversions
            // - Minimum stock alerts (3 different scenarios)
            // - FIFO/FEFO consumption logic (opened first, then earliest expiry, then FIFO)
            // - Average shelf life tracking for products
            // - Stock entry edit history
            //
            // Next Steps:
            // 1. Batch 3: Product management views (prices, history, substitutions)
            // 2. Batch 4: UI helper views (stock overview, journal, etc.)
            // 3. Test views return correct data
            // 4. Implement StockService business logic
            //
            // Note: PostgreSQL INSTEAD OF triggers (from stock_next_use in Grocy)
            // are not migrated - stock operations should be handled in application code
            // via StockService rather than through view triggers.
            //
            // =========================================================================
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop views in reverse dependency order
            migrationBuilder.Sql("DROP VIEW IF EXISTS stock_missing_products CASCADE;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS stock_next_use CASCADE;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS stock_current CASCADE;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS stock_average_product_shelf_life CASCADE;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS stock_edited_entries CASCADE;");
        }
    }
}
