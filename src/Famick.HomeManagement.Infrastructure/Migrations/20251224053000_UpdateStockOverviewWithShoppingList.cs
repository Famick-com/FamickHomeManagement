using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateStockOverviewWithShoppingList : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // =========================================================================
            // UPDATE PHASE 1 VIEW: uihelper_stock_current_overview (FINAL UPDATE)
            // =========================================================================
            //
            // Updates the stock overview view to use actual shopping_list table
            // instead of returning false for on_shopping_list.
            //
            // Changes:
            // - on_shopping_list: false → EXISTS(SELECT 1 FROM shopping_list WHERE product_id = sc.product_id)
            //
            // This completes the Phase 1 view - all columns now return actual data!
            //
            // =========================================================================

            // Drop the existing view
            migrationBuilder.Sql("DROP VIEW IF EXISTS uihelper_stock_current_overview CASCADE;");

            // Recreate with updated shopping list check
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

                    Updated: Now uses actual shopping_list table (PHASE 1 VIEW COMPLETE!)
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
                    pg.name AS product_group_name,
                    sl.name AS default_store_name,
                    EXISTS(SELECT 1 FROM shopping_list WHERE shopping_list.product_id = sc.product_id) AS on_shopping_list, -- ✅ Now uses actual shopping_list table
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
                LEFT JOIN product_groups pg
                    ON p.product_group_id = pg.id
                LEFT JOIN shopping_locations sl
                    ON p.shopping_location_id = sl.id
                WHERE p.hide_on_stock_overview = 0;
            ");

            // =========================================================================
            // PHASE 1 VIEW FULLY COMPLETE! 🎉
            // =========================================================================
            //
            // ✅ uihelper_stock_current_overview now returns actual data for ALL columns:
            //    - product_group_name (from product_groups table) ✅
            //    - default_store_name (from shopping_locations table) ✅
            //    - on_shopping_list (from shopping_list table) ✅
            //
            // Phase 1 + Phase 2 Batch 1-2 integration is now complete!
            //
            // All 21 Phase 1 views are fully functional with Phase 2 tables.
            //
            // =========================================================================
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop the updated view
            migrationBuilder.Sql("DROP VIEW IF EXISTS uihelper_stock_current_overview CASCADE;");

            // Recreate previous version (with product_groups and shopping_locations but not shopping_list)
            migrationBuilder.Sql(@"
                CREATE VIEW uihelper_stock_current_overview AS
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
                    pg.name AS product_group_name,
                    sl.name AS default_store_name,
                    false AS on_shopping_list,
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
                    SELECT *, 1 AS is_in_stock_or_below_min_stock
                    FROM stock_current
                    WHERE best_before_date IS NOT NULL
                    UNION
                    SELECT m.id, 0, 0, 0, null::timestamp with time zone, 0, 0, 0, p.due_type, 1 AS is_in_stock_or_below_min_stock
                    FROM stock_missing_products m
                    JOIN products p ON m.id = p.id
                    WHERE m.id NOT IN (SELECT product_id FROM stock_current)
                    UNION
                    SELECT p2.id, 0, 0, 0, null::timestamp with time zone, 0, 0, 0, p2.due_type, 0 AS is_in_stock_or_below_min_stock
                    FROM products p2
                    WHERE active = true
                        AND p2.id NOT IN (SELECT product_id FROM stock_current UNION SELECT id FROM stock_missing_products)
                    ) sc
                JOIN products_view p ON sc.product_id = p.id
                JOIN locations l ON p.location_id = l.id
                JOIN quantity_units qu_stock ON p.qu_id_stock = qu_stock.id
                JOIN quantity_units qu_purchase ON p.qu_id_purchase = qu_purchase.id
                JOIN quantity_units qu_consume ON p.qu_id_consume = qu_consume.id
                JOIN quantity_units qu_price ON p.qu_id_price = qu_price.id
                LEFT JOIN products_last_purchased plp ON sc.product_id = plp.product_id
                LEFT JOIN products_average_price pap ON sc.product_id = pap.product_id
                LEFT JOIN product_barcodes_comma_separated pbcs ON sc.product_id = pbcs.product_id
                LEFT JOIN products p_parent ON p.parent_product_id = p_parent.id
                LEFT JOIN product_groups pg ON p.product_group_id = pg.id
                LEFT JOIN shopping_locations sl ON p.shopping_location_id = sl.id
                WHERE p.hide_on_stock_overview = 0;
            ");
        }
    }
}
