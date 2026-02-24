using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUIHelperViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // =========================================================================
            // BATCH 4: UI HELPER VIEWS - GROCY TO POSTGRESQL MIGRATION
            // =========================================================================
            //
            // Adds 4 UI helper views that aggregate data for application displays:
            // - Product detail aggregation (pricing, shelf life, spoil rates)
            // - Comprehensive stock overview (current levels + full product details)
            // - Stock entries with enriched product information
            // - Stock transaction journal (audit trail)
            //
            // These views depend on:
            // - All Batch 1, 2, and 3 views
            // - stock and stock_log tables
            // - products, locations, quantity_units tables
            //
            // SQLite to PostgreSQL conversions applied:
            // - IFNULL() → COALESCE()
            // - CAST(x AS REAL) → CAST(x AS NUMERIC)
            // - cache__ tables → actual view references
            // - EXISTS() returns boolean natively (no CASE needed)
            //
            // Note on dependencies:
            // - Some tables referenced (product_groups, shopping_locations, shopping_list)
            //   will be created in Phase 2
            // - Views will need LEFT JOIN to handle missing tables gracefully
            // - uihelper_stock_current_overview will be updated when Phase 2 tables exist
            //
            // View creation order (no interdependencies):
            // 1. uihelper_product_details
            // 2. uihelper_stock_entries
            // 3. uihelper_stock_journal
            // 4. uihelper_stock_current_overview
            //
            // =========================================================================

            // -------------------------------------------------------------------------
            // VIEW 1: uihelper_product_details
            // -------------------------------------------------------------------------
            // Purpose: Aggregates product detail information for detailed displays
            // Returns: Pricing history, shelf life, spoil rates, QU factors, hierarchy
            // Complexity: COMPLEX - Multiple JOINs and subqueries
            // Migration Source: Grocy 0234.sql
            // Note: Replaces cache__ tables with actual views
            // -------------------------------------------------------------------------
            migrationBuilder.Sql(@"
                CREATE VIEW uihelper_product_details AS
                /*
                    Aggregates comprehensive product detail information:
                    - Last purchase info (date, price, location)
                    - Average price
                    - Average shelf life
                    - Current price
                    - Last used date
                    - Next due date
                    - Spoil rate percentage
                    - Quantity unit conversion factors
                    - Has child products flag
                */
                SELECT
                    p.id,
                    plp.purchased_date AS last_purchased_date,
                    plp.price AS last_purchased_price,
                    plp.shopping_location_id AS last_purchased_shopping_location_id,
                    pap.price AS average_price,
                    sl.average_shelf_life_days,
                    pcp.price AS current_price,
                    last_used.used_date AS last_used_date,
                    next_due.best_before_date AS next_due_date,
                    COALESCE((spoil_count.amount * 100.0) / consume_count.amount, 0) AS spoil_rate,
                    CAST(COALESCE(quc_purchase2stock.factor, 1.0) AS NUMERIC) AS qu_factor_purchase_to_stock,
                    CAST(COALESCE(quc_price2stock.factor, 1.0) AS NUMERIC) AS qu_factor_price_to_stock,
                    CASE WHEN EXISTS(SELECT 1 FROM products px WHERE px.parent_product_id = p.id) THEN 1 ELSE 0 END AS has_childs
                FROM products p
                LEFT JOIN products_last_purchased plp
                    ON p.id = plp.product_id
                LEFT JOIN products_average_price pap
                    ON p.id = pap.product_id
                LEFT JOIN stock_average_product_shelf_life sl
                    ON p.id = sl.id
                LEFT JOIN products_current_price pcp
                    ON p.id = pcp.product_id
                LEFT JOIN quantity_unit_conversions_resolved quc_purchase2stock
                    ON p.id = quc_purchase2stock.product_id
                    AND p.qu_id_purchase = quc_purchase2stock.from_qu_id
                    AND p.qu_id_stock = quc_purchase2stock.to_qu_id
                LEFT JOIN quantity_unit_conversions_resolved quc_price2stock
                    ON p.id = quc_price2stock.product_id
                    AND p.qu_id_price = quc_price2stock.from_qu_id
                    AND p.qu_id_stock = quc_price2stock.to_qu_id
                LEFT JOIN (
                    SELECT product_id, MAX(used_date) AS used_date
                    FROM stock_log
                    WHERE transaction_type = 'consume'
                        AND undone = false
                    GROUP BY product_id
                ) last_used
                    ON p.id = last_used.product_id
                LEFT JOIN (
                    SELECT product_id, MIN(best_before_date) AS best_before_date
                    FROM stock
                    GROUP BY product_id
                ) next_due
                    ON p.id = next_due.product_id
                LEFT JOIN (
                    SELECT product_id, SUM(amount) AS amount
                    FROM stock_log
                    WHERE transaction_type = 'consume'
                        AND undone = false
                    GROUP BY product_id
                ) consume_count
                    ON p.id = consume_count.product_id
                LEFT JOIN (
                    SELECT product_id, SUM(amount) AS amount
                    FROM stock_log
                    WHERE transaction_type = 'consume'
                        AND undone = false
                        AND spoiled = 1
                    GROUP BY product_id
                ) spoil_count
                    ON p.id = spoil_count.product_id;
            ");

            // -------------------------------------------------------------------------
            // VIEW 2: uihelper_stock_entries
            // -------------------------------------------------------------------------
            // Purpose: Stock entries with enriched product information
            // Returns: All stock columns + products_view columns
            // Complexity: SIMPLE - Direct JOIN
            // Migration Source: Grocy 0219.sql
            // -------------------------------------------------------------------------
            migrationBuilder.Sql(@"
                CREATE VIEW uihelper_stock_entries AS
                /*
                    Simple join of stock entries with enriched product information.
                    Used for detailed stock entry displays showing all product metadata.
                */
                SELECT
                    s.*,
                    p.name AS product_name,
                    p.description AS product_description,
                    p.location_id AS product_location_id,
                    p.min_stock_amount,
                    p.qu_id_stock,
                    p.qu_id_purchase,
                    p.qu_id_consume,
                    p.qu_factor_purchase_to_stock,
                    p.qu_factor_consume_to_stock,
                    p.enable_tare_weight_handling,
                    p.tare_weight,
                    p.calories,
                    p.default_best_before_days,
                    p.picture_file_name,
                    p.allow_partial_units_in_stock,
                    p.active AS product_active,
                    p.has_sub_products,
                    p.parent_product_id,
                    p.cumulate_min_stock_amount_of_sub_products,
                    p.due_type,
                    p.quick_consume_amount,
                    p.quick_open_amount,
                    p.hide_on_stock_overview,
                    p.default_consume_location_id,
                    p.treat_opened_as_out_of_stock,
                    p.no_own_stock,
                    p.qu_id_price,
                    p.qu_factor_price_to_stock,
                    p.disable_open
                FROM stock s
                JOIN products_view p
                    ON s.product_id = p.id;
            ");

            // -------------------------------------------------------------------------
            // VIEW 3: uihelper_stock_journal
            // -------------------------------------------------------------------------
            // Purpose: Stock transaction audit trail with context
            // Returns: Transaction details + product, location, user, and QU info
            // Complexity: MEDIUM - Multiple JOINs
            // Migration Source: Grocy 0178.sql
            // -------------------------------------------------------------------------
            migrationBuilder.Sql(@"
                CREATE VIEW uihelper_stock_journal AS
                /*
                    Stock transaction journal combining stock_log entries with
                    related product, location, user, and quantity unit information
                    for comprehensive audit trail displays.
                */
                SELECT
                    sl.id,
                    sl.created_at AS row_created_timestamp,
                    sl.correlation_id,
                    sl.undone,
                    sl.undone_timestamp,
                    sl.transaction_type,
                    sl.spoiled,
                    sl.amount,
                    sl.location_id,
                    l.name AS location_name,
                    p.name AS product_name,
                    qu.name AS qu_name,
                    qu.name_plural AS qu_name_plural,
                    u.display_name AS user_display_name,
                    p.id AS product_id,
                    sl.note,
                    sl.stock_id
                FROM stock_log sl
                LEFT JOIN users_dto u
                    ON sl.user_id = u.id
                JOIN products p
                    ON sl.product_id = p.id
                LEFT JOIN locations l
                    ON sl.location_id = l.id
                JOIN quantity_units qu
                    ON p.qu_id_stock = qu.id;
            ");

            // -------------------------------------------------------------------------
            // VIEW 4: uihelper_stock_current_overview
            // -------------------------------------------------------------------------
            // Purpose: Comprehensive stock overview with all product details
            // Returns: Current stock + product info + calories + shopping status
            // Complexity: VERY COMPLEX - Multiple UNIONs and extensive JOINs
            // Migration Source: Grocy 0252.sql
            // Note: References to product_groups, shopping_locations, shopping_list
            //       are handled with LEFT JOINs - these tables will be created in Phase 2
            // -------------------------------------------------------------------------
            migrationBuilder.Sql(@"
                CREATE VIEW uihelper_stock_current_overview AS
                /*
                    Comprehensive stock overview view combining:
                    - Current stock levels (in stock, missing, no stock)
                    - Full product details and hierarchy
                    - Nutritional information (calories)
                    - Pricing information (last price, average price)
                    - Shopping list status
                    - Quantity unit conversions
                    - Product location and barcode information

                    Includes three categories via UNION:
                    1. Products currently in stock
                    2. Products below minimum stock (missing products)
                    3. Products with no stock at all
                */
                SELECT
                    p.id,
                    sc.amount_opened AS amount_opened,
                    p.tare_weight AS tare_weight,
                    p.enable_tare_weight_handling AS enable_tare_weight_handling,
                    sc.amount AS amount,
                    sc.value as value,
                    sc.product_id AS product_id,
                    COALESCE(sc.best_before_date, '2888-12-31') AS best_before_date,
                    EXISTS(SELECT id FROM stock_missing_products WHERE id = sc.product_id) AS product_missing,
                    p.name AS product_name,
                    NULL AS product_group_name, -- product_groups table created in Phase 2
                    NULL AS default_store_name, -- shopping_locations table created in Phase 2
                    false AS on_shopping_list, -- shopping_list table created in Phase 2
                    qu_stock.name AS qu_stock_name,
                    qu_stock.name_plural AS qu_stock_name_plural,
                    qu_purchase.name AS qu_purchase_name,
                    qu_purchase.name_plural AS qu_purchase_name_plural,
                    qu_consume.name AS qu_consume_name,
                    qu_consume.name_plural AS qu_consume_name_plural,
                    qu_price.name AS qu_price_name,
                    qu_price.name_plural AS qu_price_name_plural,
                    sc.is_aggregated_amount,
                    sc.amount_opened_aggregated,
                    sc.amount_aggregated,
                    p.calories AS product_calories,
                    sc.amount * p.calories AS calories,
                    sc.amount_aggregated * p.calories AS calories_aggregated,
                    p.quick_consume_amount,
                    p.quick_consume_amount / p.qu_factor_consume_to_stock AS quick_consume_amount_qu_consume,
                    p.quick_open_amount,
                    p.quick_open_amount / p.qu_factor_consume_to_stock AS quick_open_amount_qu_consume,
                    p.due_type,
                    plp.purchased_date AS last_purchased,
                    plp.price AS last_price,
                    pap.price as average_price,
                    p.min_stock_amount,
                    pbcs.barcodes AS product_barcodes,
                    p.description AS product_description,
                    l.name AS product_default_location_name,
                    p_parent.id AS parent_product_id,
                    p_parent.name AS parent_product_name,
                    p.picture_file_name AS product_picture_file_name,
                    p.no_own_stock AS product_no_own_stock,
                    p.qu_factor_purchase_to_stock AS product_qu_factor_purchase_to_stock,
                    p.qu_factor_price_to_stock AS product_qu_factor_price_to_stock,
                    sc.is_in_stock_or_below_min_stock,
                    p.disable_open
                FROM (
                    -- Products currently in stock
                    SELECT *, 1 AS is_in_stock_or_below_min_stock
                    FROM stock_current
                    WHERE best_before_date IS NOT NULL

                    UNION

                    -- Products below minimum stock (missing)
                    SELECT m.id, 0, 0, 0, null::timestamp with time zone, 0, 0, 0, p.due_type, 1 AS is_in_stock_or_below_min_stock
                    FROM stock_missing_products m
                    JOIN products p
                        ON m.id = p.id
                    WHERE m.id NOT IN (SELECT product_id FROM stock_current)

                    UNION

                    -- Active products with no stock at all
                    SELECT p2.id, 0, 0, 0, null::timestamp with time zone, 0, 0, 0, p2.due_type, 0 AS is_in_stock_or_below_min_stock
                    FROM products p2
                    WHERE active = true
                        AND p2.id NOT IN (SELECT product_id FROM stock_current UNION SELECT id FROM stock_missing_products)
                    ) sc
                JOIN products_view p
                    ON sc.product_id = p.id
                JOIN locations l
                    ON p.location_id = l.id
                JOIN quantity_units qu_stock
                    ON p.qu_id_stock = qu_stock.id
                JOIN quantity_units qu_purchase
                    ON p.qu_id_purchase = qu_purchase.id
                JOIN quantity_units qu_consume
                    ON p.qu_id_consume = qu_consume.id
                JOIN quantity_units qu_price
                    ON p.qu_id_price = qu_price.id
                LEFT JOIN products_last_purchased plp
                    ON sc.product_id = plp.product_id
                LEFT JOIN products_average_price pap
                    ON sc.product_id = pap.product_id
                LEFT JOIN product_barcodes_comma_separated pbcs
                    ON sc.product_id = pbcs.product_id
                LEFT JOIN products p_parent
                    ON p.parent_product_id = p_parent.id
                WHERE p.hide_on_stock_overview = 0;
            ");

            // =========================================================================
            // BATCH 4 UI HELPER VIEWS COMPLETE
            // =========================================================================
            //
            // ✅ uihelper_product_details - Product detail aggregation
            // ✅ uihelper_stock_entries - Stock entries with product info
            // ✅ uihelper_stock_journal - Transaction audit trail
            // ✅ uihelper_stock_current_overview - Comprehensive stock overview
            //
            // These views enable UI displays for:
            // - Detailed product information screens (pricing, shelf life, spoil rates)
            // - Stock entry management screens
            // - Stock transaction history and audit trails
            // - Stock overview dashboard (current levels, missing products, calories)
            //
            // Phase 1 Database Views: COMPLETE! 🎉
            // - Batch 1: 6 foundation views ✅
            // - Batch 2: 5 stock management views ✅
            // - Batch 3: 6 product management views ✅
            // - Batch 4: 4 UI helper views ✅
            // Total: 21 views migrated
            //
            // Next Steps:
            // 1. Test all views return correct data
            // 2. Create Phase 2 tables (product_groups, shopping_locations, shopping_list)
            // 3. Update uihelper_stock_current_overview when Phase 2 tables exist
            // 4. Begin service layer implementation (StockService, ProductService)
            //
            // Note: Some columns in uihelper_stock_current_overview return NULL
            // for Phase 2 table references (product_group_name, default_store_name,
            // on_shopping_list). These will be updated in a future migration when
            // the Phase 2 tables are created.
            //
            // =========================================================================
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop views (no dependencies between them)
            migrationBuilder.Sql("DROP VIEW IF EXISTS uihelper_stock_current_overview CASCADE;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS uihelper_stock_journal CASCADE;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS uihelper_stock_entries CASCADE;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS uihelper_product_details CASCADE;");
        }
    }
}
