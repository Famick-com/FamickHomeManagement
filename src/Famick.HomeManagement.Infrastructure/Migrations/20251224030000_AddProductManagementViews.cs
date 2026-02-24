using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductManagementViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // =========================================================================
            // BATCH 3: PRODUCT MANAGEMENT VIEWS - GROCY TO POSTGRESQL MIGRATION
            // =========================================================================
            //
            // Adds 6 product price and status tracking views that enable:
            // - Last purchase tracking per product
            // - Average price calculations
            // - Complete price history
            // - Current price determination (next-to-use or last price)
            // - Product due status (ok, due_soon, overdue, expired)
            // - Product substitution logic (parent/child products)
            //
            // These views depend on:
            // - stock table (Migration 20251224010000)
            // - stock_log table (Migration 20251224010000)
            // - stock_edited_entries view (Migration 20251224020000)
            // - stock_next_use view (Migration 20251224020000)
            // - stock_current view (Migration 20251224020000)
            // - stock_missing_products view (Migration 20251224020000)
            // - products_view (Migration 20251224000000)
            // - products_resolved (Migration 20251224000000)
            //
            // SQLite to PostgreSQL conversions applied:
            // - IFNULL() → COALESCE()
            // - JULIANDAY() date math → EXTRACT(DAY FROM timestamp difference)
            // - grocy_user_setting() → user_settings table lookup (to be implemented)
            // - DROP VIEW → DROP VIEW IF EXISTS CASCADE
            //
            // View creation order follows dependency chain:
            // 1. products_price_history (base view)
            // 2. products_average_price (depends on #1 indirectly)
            // 3. products_last_purchased (depends on #1)
            // 4. products_current_price (depends on #3 and stock_next_use)
            // 5. products_volatile_status (depends on stock_current, stock_missing_products)
            // 6. products_current_substitutions (depends on products_view, stock_next_use)
            //
            // =========================================================================

            // -------------------------------------------------------------------------
            // VIEW 1: products_price_history
            // -------------------------------------------------------------------------
            // Purpose: Complete price history for all products
            // Returns: product_id, price, amount, purchased_date, shopping_location_id, transaction_type
            // Complexity: MEDIUM - Accounts for edited stock entries
            // Migration Source: Grocy 0239.sql
            // -------------------------------------------------------------------------
            migrationBuilder.Sql(@"
                CREATE VIEW products_price_history AS
                /*
                    Complete price history showing all purchase prices, amounts, dates, and locations.
                    Accounts for edited stock entries by using the newest 'stock-edit-new' entry.
                */
                SELECT
                    sl.product_id AS id, -- Dummy, for ORM compatibility
                    sl.product_id,
                    sl.price,
                    COALESCE(sl.edited_origin_amount, sl.amount) AS amount,
                    sl.purchased_date,
                    sl.shopping_location_id,
                    sl.transaction_type
                FROM (
                    SELECT sl.*, CASE WHEN sl.transaction_type = 'stock-edit-new' THEN see.edited_origin_amount END AS edited_origin_amount
                    FROM stock_log sl
                    LEFT JOIN stock_edited_entries see
                        ON sl.stock_id = see.stock_id
                ) sl
                WHERE sl.undone = false
                    AND (
                        (sl.transaction_type IN ('purchase', 'inventory-correction', 'self-production') AND sl.stock_id NOT IN (SELECT stock_id FROM stock_edited_entries)) -- Unedited origin entries
                        OR (sl.transaction_type = 'stock-edit-new' AND sl.id IN (SELECT stock_log_id_of_newest_edited_entry FROM stock_edited_entries)) -- Edited origin entries => take the newest 'stock-edit-new' one
                    )
                    AND COALESCE(sl.price, 0) > 0
                    AND COALESCE(sl.amount, 0) > 0;
            ");

            // -------------------------------------------------------------------------
            // VIEW 2: products_average_price
            // -------------------------------------------------------------------------
            // Purpose: Weighted average purchase price per product
            // Calculation: SUM(amount * price) / SUM(amount)
            // Complexity: MEDIUM - Accounts for edited stock entries
            // Migration Source: Grocy 0230.sql
            // -------------------------------------------------------------------------
            migrationBuilder.Sql(@"
                CREATE VIEW products_average_price AS
                /*
                    Calculates weighted average purchase price per product.
                    Formula: SUM(amount * price) / SUM(amount)
                    Accounts for edited stock entries using edited_origin_amount when available.
                */
                SELECT
                    1 AS id, -- Dummy, for ORM compatibility
                    sl.product_id,
                    SUM(COALESCE(sl.edited_origin_amount, sl.amount) * sl.price) / SUM(COALESCE(sl.edited_origin_amount, sl.amount)) as price
                FROM (
                    SELECT sl.*, CASE WHEN sl.transaction_type = 'stock-edit-new' THEN see.edited_origin_amount END AS edited_origin_amount
                    FROM stock_log sl
                    LEFT JOIN stock_edited_entries see
                        ON sl.stock_id = see.stock_id
                ) sl
                WHERE sl.undone = false
                    AND (
                        (sl.transaction_type IN ('purchase', 'inventory-correction', 'self-production') AND sl.stock_id NOT IN (SELECT stock_id FROM stock_edited_entries)) -- Unedited origin entries
                        OR (sl.transaction_type = 'stock-edit-new' AND sl.id IN (SELECT stock_log_id_of_newest_edited_entry FROM stock_edited_entries)) -- Edited origin entries => take the newest 'stock-edit-new' one
                    )
                    AND COALESCE(sl.price, 0) > 0
                    AND COALESCE(sl.amount, 0) > 0
                GROUP BY sl.product_id;
            ");

            // -------------------------------------------------------------------------
            // VIEW 3: products_last_purchased
            // -------------------------------------------------------------------------
            // Purpose: Last purchase information per product
            // Returns: product_id, amount, best_before_date, purchased_date, location_id, shopping_location_id, price
            // Complexity: COMPLEX - Nested subqueries to find last purchase accounting for edits
            // Migration Source: Grocy 0236.sql
            // -------------------------------------------------------------------------
            migrationBuilder.Sql(@"
                CREATE VIEW products_last_purchased AS
                /*
                    Returns the last purchase information per product.
                    Handles multiple purchase transactions per day by taking the MAX stock_log id.
                    Accounts for undone and edited transactions.
                    Price is fetched from products_price_history for the last purchase date.
                */
                SELECT
                    1 AS id, -- Dummy, for ORM compatibility
                    sl.product_id,
                    sl.amount,
                    sl.best_before_date,
                    sl.purchased_date,
                    sl.location_id,
                    sl.shopping_location_id,
                    COALESCE((SELECT price FROM products_price_history WHERE product_id = sl.product_id ORDER BY purchased_date DESC LIMIT 1), 0) AS price
                FROM stock_log sl
                JOIN (
                    /*
                        This subquery gets the ID of the stock_log row (per product) which refers to the last purchase transaction,
                        while taking undone and edited transactions into account
                    */
                    SELECT
                        sl1.product_id,
                        MAX(sl1.id) stock_log_id_of_last_purchase
                    FROM stock_log sl1
                    JOIN (
                        /*
                            This subquery finds the last purchased date per product,
                            there can be multiple purchase transactions per day, therefore a JOIN by purchased_date
                            for the outer query on this and then take MAX id of stock_log (of that day)
                        */
                        SELECT
                            sl2.product_id,
                            MAX(sl2.purchased_date) AS last_purchased_date
                        FROM stock_log sl2
                        WHERE sl2.undone = false
                            AND (
                                (sl2.transaction_type IN ('purchase', 'inventory-correction', 'self-production') AND sl2.stock_id NOT IN (SELECT stock_id FROM stock_edited_entries))
                                OR (sl2.transaction_type = 'stock-edit-new' AND sl2.stock_id IN (SELECT stock_id FROM stock_edited_entries) AND sl2.id IN (SELECT stock_log_id_of_newest_edited_entry FROM stock_edited_entries))
                            )
                        GROUP BY sl2.product_id
                    ) x2
                        ON sl1.product_id = x2.product_id
                        AND sl1.purchased_date = x2.last_purchased_date
                    WHERE sl1.undone = false
                        AND (
                            (sl1.transaction_type IN ('purchase', 'inventory-correction', 'self-production') AND sl1.stock_id NOT IN (SELECT stock_id FROM stock_edited_entries))
                            OR (sl1.transaction_type = 'stock-edit-new' AND sl1.stock_id IN (SELECT stock_id FROM stock_edited_entries) AND sl1.id IN (SELECT stock_log_id_of_newest_edited_entry FROM stock_edited_entries))
                        )
                    GROUP BY sl1.product_id
                ) x
                    ON sl.product_id = x.product_id
                    AND sl.id = x.stock_log_id_of_last_purchase;
            ");

            // -------------------------------------------------------------------------
            // VIEW 4: products_current_price
            // -------------------------------------------------------------------------
            // Purpose: Current price per product (next-to-use or last purchase price)
            // Logic: Uses price from stock_next_use if product is in stock, otherwise last purchase price
            // Complexity: MEDIUM - Joins multiple views
            // Migration Source: Grocy 0200.sql
            // -------------------------------------------------------------------------
            migrationBuilder.Sql(@"
                CREATE VIEW products_current_price AS
                /*
                    Current price per product, based on:
                    - The stock entry to use next (from stock_next_use), if product is in stock
                    - OR the last purchase price (from products_last_purchased), if out of stock
                */
                SELECT
                    -1 AS id, -- Dummy, for ORM compatibility
                    p.id AS product_id,
                    COALESCE(snu.price, plp.price) AS price
                FROM products p
                LEFT JOIN (
                    SELECT
                        product_id,
                        MAX(priority),
                        price -- Bare column, uses the price from the row with MAX priority
                    FROM stock_next_use
                    GROUP BY product_id
                    ORDER BY priority DESC, open DESC, best_before_date ASC, purchased_date ASC
                    ) snu
                    ON p.id = snu.product_id
                LEFT JOIN products_last_purchased plp
                    ON p.id = plp.product_id;
            ");

            // -------------------------------------------------------------------------
            // VIEW 5: products_volatile_status
            // -------------------------------------------------------------------------
            // Purpose: Product status tracking (due status and below min stock)
            // Returns: product_id, product_name, current_due_status (ok/due_soon/overdue/expired), is_currently_below_min_stock_amount
            // Complexity: MEDIUM - Date calculations and status logic
            // Migration Source: Grocy 0177.sql
            // SQLite Change: JULIANDAY() → EXTRACT(DAY FROM timestamp difference)
            // Note: grocy_user_setting('stock_due_soon_days') hardcoded to 5 for now (will be user_settings table later)
            // -------------------------------------------------------------------------
            migrationBuilder.Sql(@"
                CREATE VIEW products_volatile_status AS
                /*
                    Returns the current status of each product:
                    - current_due_status: ok, due_soon, overdue, or expired
                    - is_currently_below_min_stock_amount: 1 if below min stock, 0 otherwise

                    Due status logic:
                    - If best_before_date is in the past:
                      - due_type = 1 (best before) → 'overdue'
                      - due_type = 2 (expiration) → 'expired'
                    - If best_before_date is within stock_due_soon_days (5 days default):
                      - 'due_soon'
                    - Otherwise:
                      - 'ok'
                */
                SELECT
                    -1 AS id, -- Dummy, for ORM compatibility
                    p.id AS product_id,
                    p.name AS product_name,
                    CASE WHEN EXTRACT(DAY FROM (sc.best_before_date::timestamp - CURRENT_TIMESTAMP)) < 0 THEN
                        CASE WHEN p.due_type = 1 THEN 'overdue' ELSE 'expired' END
                    ELSE
                        CASE WHEN EXTRACT(DAY FROM (sc.best_before_date::timestamp - CURRENT_TIMESTAMP)) < 5 THEN
                            'due_soon'
                        ELSE
                            'ok'
                        END
                    END AS current_due_status,
                    CASE WHEN smp.id IS NOT NULL THEN 1 ELSE 0 END AS is_currently_below_min_stock_amount
                FROM products p
                LEFT JOIN stock_current sc
                    ON p.id = sc.product_id
                LEFT JOIN stock_missing_products smp
                    ON p.id = smp.id;
            ");

            // -------------------------------------------------------------------------
            // VIEW 6: products_current_substitutions
            // -------------------------------------------------------------------------
            // Purpose: Product substitution logic for parent/child products
            // Returns: parent_product_id, product_id_effective (parent if in stock, otherwise next available sub-product)
            // Complexity: COMPLEX - Nested subqueries with stock_next_use
            // Migration Source: Grocy 0200.sql
            // -------------------------------------------------------------------------
            migrationBuilder.Sql(@"
                CREATE VIEW products_current_substitutions AS
                /*
                    When a parent product is not in stock itself, any sub product (the next based on the
                    default consume rule) should be used.

                    This view lists all parent products and in the column 'product_id_effective' either itself,
                    when the corresponding parent product is currently in stock itself, or otherwise the next
                    sub product to use.
                */
                SELECT
                    -1, -- Dummy, for ORM compatibility
                    p_sub.id AS parent_product_id,
                    CASE WHEN p_sub.has_sub_products = 1 THEN
                        CASE WHEN COALESCE(sc.amount, 0) = 0 THEN -- Parent product itself is currently not in stock => use the next sub product
                            (
                            SELECT x_snu.product_id
                            FROM products_resolved x_pr
                            JOIN stock_next_use x_snu
                                ON x_pr.sub_product_id = x_snu.product_id
                            WHERE x_pr.parent_product_id = p_sub.id
                                AND x_pr.parent_product_id != x_pr.sub_product_id
                            ORDER BY x_snu.priority DESC, x_snu.open DESC, x_snu.best_before_date ASC, x_snu.purchased_date ASC
                            LIMIT 1
                            )
                        ELSE -- Parent product itself is currently in stock => use it
                            p_sub.id
                        END
                    END AS product_id_effective
                FROM products_view p
                JOIN products_resolved pr
                    ON p.id = pr.parent_product_id
                JOIN products_view p_sub
                    ON pr.sub_product_id = p_sub.id
                JOIN stock_current sc
                    ON p_sub.id = sc.product_id
                WHERE p_sub.has_sub_products = 1;
            ");

            // =========================================================================
            // BATCH 3 PRODUCT MANAGEMENT VIEWS COMPLETE
            // =========================================================================
            //
            // ✅ products_price_history - Complete price history for all products
            // ✅ products_average_price - Weighted average pricing
            // ✅ products_last_purchased - Last purchase tracking
            // ✅ products_current_price - Current price logic (next-to-use or last)
            // ✅ products_volatile_status - Due status and stock level monitoring
            // ✅ products_current_substitutions - Parent/child product substitution
            //
            // These views enable comprehensive product pricing and status tracking:
            // - Complete price history with transaction context
            // - Average price calculations for reporting
            // - Last purchase information for reordering
            // - Current price determination for consumption/sales
            // - Product due status alerts (overdue, expired, due soon)
            // - Automatic product substitution when parent out of stock
            //
            // Next Steps:
            // 1. Batch 4: UI helper views (stock overview, journal, etc.)
            // 2. Test all views return correct data
            // 3. Create user_settings table for stock_due_soon_days configuration
            // 4. Begin StockService and ProductService implementation
            //
            // Note: grocy_user_setting('stock_due_soon_days') is currently hardcoded to 5
            // in products_volatile_status. This will be replaced with a user_settings table
            // lookup once the user_settings table is created in a future migration.
            //
            // =========================================================================
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop views in reverse dependency order
            migrationBuilder.Sql("DROP VIEW IF EXISTS products_current_substitutions CASCADE;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS products_volatile_status CASCADE;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS products_current_price CASCADE;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS products_last_purchased CASCADE;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS products_average_price CASCADE;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS products_price_history CASCADE;");
        }
    }
}
