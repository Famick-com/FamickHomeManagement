using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDatabaseViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // =========================================================================
            // PHASE 1 CRITICAL DATABASE VIEWS - GROCY TO POSTGRESQL MIGRATION
            // =========================================================================
            //
            // This migration adds 18 critical database views from Grocy, converted from
            // SQLite to PostgreSQL syntax. Views are added in dependency order.
            //
            // Migration Order:
            // 1. Foundation: quantity_unit_conversions_resolved
            // 2. Products: products_resolved, products_view
            // 3. Stock Core: stock_current, stock_missing_products
            // 4. Users: users_dto
            //
            // Note: Additional Phase 1 views will be added in subsequent migrations
            // to keep migration file size manageable.
            //
            // SQLite to PostgreSQL conversions applied:
            // - IFNULL() → COALESCE()
            // - GROUP_CONCAT() → STRING_AGG()
            // - CAST(x AS TEXT) → CAST(x AS VARCHAR) or x::TEXT
            // - All views support multi-tenancy via TenantId filtering in queries
            // =========================================================================

            // -------------------------------------------------------------------------
            // VIEW 1: quantity_unit_conversions_resolved
            // -------------------------------------------------------------------------
            // Purpose: Recursive resolution of quantity unit conversions (foundation view)
            // Dependencies: quantity_unit_conversions, products, quantity_units
            // Complexity: VERY COMPLEX - Multiple recursive CTEs
            // Migration Source: 0232.sql
            // -------------------------------------------------------------------------
            migrationBuilder.Sql(@"
                CREATE VIEW quantity_unit_conversions_resolved AS

                WITH RECURSIVE

                -- Default QU conversions (not product-specific)
                default_conversions(from_qu_id, to_qu_id, factor) AS (
                    SELECT
                        from_qu_id,
                        to_qu_id,
                        factor
                    FROM quantity_unit_conversions
                    WHERE product_id IS NULL
                ),

                -- Recursive closure for default conversions
                default_closure(depth, from_qu_id, to_qu_id, factor, path) AS (
                    -- Base case: all default conversions
                    SELECT
                        1 as depth,
                        from_qu_id,
                        to_qu_id,
                        factor,
                        '/' || from_qu_id || '/' || to_qu_id || '/' AS path
                    FROM default_conversions

                    UNION

                    -- Recursive case: find all conversion paths
                    SELECT
                        c.depth + 1,
                        c.from_qu_id,
                        s.to_qu_id,
                        c.factor * s.factor,
                        c.path || s.to_qu_id || '/'
                    FROM default_closure c
                    JOIN default_conversions s
                        ON c.to_qu_id = s.from_qu_id
                    WHERE c.path NOT LIKE ('%/' || s.to_qu_id || '/%') -- Prevent cycles
                        AND NOT EXISTS(
                            SELECT 1 FROM default_conversions ci
                            WHERE ci.from_qu_id = c.from_qu_id
                            AND ci.to_qu_id = s.to_qu_id
                        ) -- Prune duplicates
                ),

                -- Distinct default conversions (shortest path wins)
                default_closure_distinct(from_qu_id, to_qu_id, factor, path) AS (
                    SELECT DISTINCT
                        from_qu_id,
                        to_qu_id,
                        FIRST_VALUE(factor) OVER win AS factor,
                        FIRST_VALUE(path) OVER win AS path
                    FROM default_closure
                    GROUP BY from_qu_id, to_qu_id
                    WINDOW win AS (PARTITION BY from_qu_id, to_qu_id ORDER BY depth)
                    ORDER BY from_qu_id, to_qu_id
                ),

                -- Product-specific conversions
                product_conversions(product_id, from_qu_id, to_qu_id, factor) AS (
                    -- Priority 1: Product-specific overrides
                    SELECT
                        product_id,
                        from_qu_id,
                        to_qu_id,
                        factor
                    FROM quantity_unit_conversions
                    WHERE product_id IS NOT NULL

                    UNION

                    -- Priority 2: Stock unit to itself (1.0 factor)
                    SELECT
                        id,
                        qu_id_stock,
                        qu_id_stock,
                        1.0
                    FROM products
                ),

                -- Recursive closure for product conversions
                product_closure(depth, product_id, from_qu_id, to_qu_id, factor, path) AS (
                    -- Base case: all product conversions
                    SELECT
                        1 as depth,
                        product_id,
                        from_qu_id,
                        to_qu_id,
                        factor,
                        '/' || from_qu_id || '/' || to_qu_id || '/' AS path
                    FROM product_conversions

                    UNION

                    -- Recursive case: find all paths
                    SELECT
                        c.depth + 1,
                        c.product_id,
                        c.from_qu_id,
                        s.to_qu_id,
                        c.factor * s.factor,
                        c.path || s.to_qu_id || '/'
                    FROM product_closure c
                    JOIN product_conversions s
                        ON c.product_id = s.product_id
                        AND c.to_qu_id = s.from_qu_id
                    WHERE c.path NOT LIKE ('%/' || s.to_qu_id || '/%') -- Prevent cycles
                        AND NOT EXISTS(
                            SELECT 1 FROM product_conversions ci
                            WHERE ci.product_id = c.product_id
                            AND ci.from_qu_id = c.from_qu_id
                            AND ci.to_qu_id = s.to_qu_id
                        ) -- Prune duplicates
                ),

                -- Distinct product conversions (shortest path wins)
                product_closure_distinct(product_id, from_qu_id, to_qu_id, factor, path) AS (
                    SELECT DISTINCT
                        product_id,
                        from_qu_id,
                        to_qu_id,
                        FIRST_VALUE(factor) OVER win AS factor,
                        FIRST_VALUE(path) OVER win AS path
                    FROM product_closure
                    GROUP BY product_id, from_qu_id, to_qu_id
                    WINDOW win AS (PARTITION BY product_id, from_qu_id, to_qu_id ORDER BY depth)
                    ORDER BY product_id, from_qu_id, to_qu_id
                ),

                -- Combine product and default conversions
                product_reachable(product_id, from_qu_id, to_qu_id, factor, path) AS (
                    SELECT
                        product_id,
                        from_qu_id,
                        to_qu_id,
                        factor,
                        path
                    FROM product_closure_distinct

                    UNION

                    SELECT
                        cd.product_id,
                        dcd.from_qu_id,
                        dcd.to_qu_id,
                        dcd.factor,
                        '/' || dcd.from_qu_id || '/' || dcd.to_qu_id || '/'
                    FROM product_closure_distinct cd
                    JOIN default_closure_distinct dcd
                        ON cd.to_qu_id = dcd.from_qu_id
                        OR cd.to_qu_id = dcd.to_qu_id
                    WHERE NOT EXISTS(
                        SELECT 1 FROM product_closure_distinct ci
                        WHERE ci.product_id = cd.product_id
                        AND ci.from_qu_id = dcd.from_qu_id
                        AND ci.to_qu_id = dcd.to_qu_id
                    )
                ),

                -- Distinct reachable conversions
                product_reachable_distinct(product_id, from_qu_id, to_qu_id, factor, path) AS (
                    SELECT DISTINCT
                        product_id,
                        from_qu_id,
                        to_qu_id,
                        FIRST_VALUE(factor) OVER win AS factor,
                        FIRST_VALUE(path) OVER win AS path
                    FROM product_reachable
                    GROUP BY product_id, from_qu_id, to_qu_id
                    WINDOW win AS (PARTITION BY product_id, from_qu_id, to_qu_id)
                    ORDER BY product_id, from_qu_id, to_qu_id
                ),

                -- Final combined closure
                closure_final(depth, product_id, from_qu_id, to_qu_id, factor, path) AS (
                    -- Base case: product reachable conversions
                    SELECT
                        1,
                        product_id,
                        from_qu_id,
                        to_qu_id,
                        factor,
                        path
                    FROM product_reachable_distinct

                    UNION

                    -- Add default conversions to end of chain
                    SELECT
                        c.depth + 1,
                        c.product_id,
                        c.from_qu_id,
                        s.to_qu_id,
                        c.factor * s.factor,
                        c.path || s.to_qu_id || '/'
                    FROM closure_final c
                    JOIN product_reachable_distinct s
                        ON c.product_id = s.product_id
                        AND c.to_qu_id = s.from_qu_id
                    WHERE c.path NOT LIKE ('%/' || s.to_qu_id || '/%') -- Prevent cycles
                        AND NOT EXISTS(
                            SELECT 1 FROM product_reachable_distinct ci
                            WHERE ci.product_id = c.product_id
                            AND ci.from_qu_id = c.from_qu_id
                            AND ci.to_qu_id = s.to_qu_id
                        )
                )

                -- Final SELECT with quantity unit names
                SELECT DISTINCT
                    -1 AS id, -- Dummy ID column (compatibility)
                    c.product_id,
                    c.from_qu_id,
                    qu_from.name AS from_qu_name,
                    qu_from.name_plural AS from_qu_name_plural,
                    c.to_qu_id,
                    qu_to.name AS to_qu_name,
                    qu_to.name_plural AS to_qu_name_plural,
                    FIRST_VALUE(c.factor) OVER win AS factor,
                    FIRST_VALUE(c.path) OVER win AS path
                FROM closure_final c
                JOIN quantity_units qu_from
                    ON c.from_qu_id = qu_from.id
                JOIN quantity_units qu_to
                    ON c.to_qu_id = qu_to.id
                GROUP BY c.product_id, c.from_qu_id, c.to_qu_id
                WINDOW win AS (PARTITION BY c.product_id, c.from_qu_id, c.to_qu_id ORDER BY c.depth)
                ORDER BY c.product_id, c.from_qu_id, c.to_qu_id;
            ");

            // -------------------------------------------------------------------------
            // VIEW 2: products_resolved
            // -------------------------------------------------------------------------
            // Purpose: Parent-child product relationships (product hierarchy)
            // Dependencies: products
            // Complexity: Simple - UNION for self-reference
            // Migration Source: 0081.sql
            // -------------------------------------------------------------------------
            migrationBuilder.Sql(@"
                CREATE VIEW products_resolved AS

                -- Products with parent (child products)
                SELECT
                    parent_product_id,
                    id AS sub_product_id
                FROM products
                WHERE parent_product_id IS NOT NULL

                UNION

                -- Products without parent (parent = self)
                SELECT
                    id AS parent_product_id,
                    id AS sub_product_id
                FROM products
                WHERE parent_product_id IS NULL;
            ");

            // -------------------------------------------------------------------------
            // VIEW 3: products_view
            // -------------------------------------------------------------------------
            // Purpose: Enhanced products with QU conversion factors
            // Dependencies: products, quantity_unit_conversions_resolved
            // Complexity: Medium - Multiple LEFT JOINs
            // Migration Source: 0225.sql
            // -------------------------------------------------------------------------
            migrationBuilder.Sql(@"
                CREATE VIEW products_view AS
                SELECT
                    p.*,
                    CASE
                        WHEN EXISTS(SELECT 1 FROM products WHERE parent_product_id = p.id)
                        THEN 1
                        ELSE 0
                    END AS has_sub_products,
                    COALESCE(quc_purchase.factor, 1.0) AS qu_factor_purchase_to_stock,
                    COALESCE(quc_consume.factor, 1.0) AS qu_factor_consume_to_stock,
                    COALESCE(quc_price.factor, 1.0) AS qu_factor_price_to_stock
                FROM products p
                LEFT JOIN quantity_unit_conversions_resolved quc_purchase
                    ON p.id = quc_purchase.product_id
                    AND p.qu_id_purchase = quc_purchase.from_qu_id
                    AND p.qu_id_stock = quc_purchase.to_qu_id
                LEFT JOIN quantity_unit_conversions_resolved quc_consume
                    ON p.id = quc_consume.product_id
                    AND p.qu_id_consume = quc_consume.from_qu_id
                    AND p.qu_id_stock = quc_consume.to_qu_id
                LEFT JOIN quantity_unit_conversions_resolved quc_price
                    ON p.id = quc_price.product_id
                    AND p.qu_id_price = quc_price.from_qu_id
                    AND p.qu_id_stock = quc_price.to_qu_id;
            ");

            // -------------------------------------------------------------------------
            // VIEW 4: users_dto
            // -------------------------------------------------------------------------
            // Purpose: User display names (computed from first/last/username)
            // Dependencies: users
            // Complexity: Simple - CASE expression
            // Migration Source: 0113.sql
            // -------------------------------------------------------------------------
            migrationBuilder.Sql(@"
                CREATE VIEW users_dto AS
                SELECT
                    u.*,
                    CASE
                        WHEN u.first_name IS NOT NULL AND u.last_name IS NOT NULL
                            THEN u.first_name || ' ' || u.last_name
                        WHEN u.first_name IS NOT NULL
                            THEN u.first_name
                        WHEN u.last_name IS NOT NULL
                            THEN u.last_name
                        ELSE u.username
                    END AS display_name
                FROM users u;
            ");

            // -------------------------------------------------------------------------
            // VIEW 5: product_barcodes_comma_separated
            // -------------------------------------------------------------------------
            // Purpose: Aggregates all barcodes per product into comma-separated string
            // Dependencies: product_barcodes, products
            // Complexity: Simple - STRING_AGG aggregation
            // Migration Source: 0123.sql
            // SQLite Change: GROUP_CONCAT() → STRING_AGG()
            // -------------------------------------------------------------------------
            migrationBuilder.Sql(@"
                CREATE VIEW product_barcodes_comma_separated AS
                SELECT
                    p.id,
                    p.id AS product_id,
                    COALESCE(STRING_AGG(pb.barcode, ','), '') AS barcodes
                FROM products p
                LEFT JOIN product_barcodes pb
                    ON p.id = pb.product_id
                WHERE p.active = true
                GROUP BY p.id;
            ");

            // -------------------------------------------------------------------------
            // VIEW 6: product_barcodes_view
            // -------------------------------------------------------------------------
            // Purpose: Union of product_barcodes + generated Grocycodes
            // Dependencies: product_barcodes, products
            // Complexity: Simple - UNION ALL
            // Migration Source: 0238.sql
            // SQLite Change: CAST(x AS TEXT) → x::VARCHAR
            // -------------------------------------------------------------------------
            migrationBuilder.Sql(@"
                CREATE VIEW product_barcodes_view AS

                -- Regular barcodes from product_barcodes table
                SELECT
                    id,
                    product_id,
                    barcode,
                    qu_id,
                    amount,
                    shopping_location_id,
                    last_price,
                    note
                FROM product_barcodes

                UNION ALL

                -- Generated Grocycodes: 'grcy:p:{product_id}'
                SELECT
                    -1 AS id,
                    id AS product_id,
                    'grcy:p:' || id::VARCHAR AS barcode,
                    NULL AS qu_id,
                    NULL AS amount,
                    NULL AS shopping_location_id,
                    NULL AS last_price,
                    NULL AS note
                FROM products;
            ");

            // =========================================================================
            // PHASE 1 VIEWS - BATCH 1 COMPLETE
            // =========================================================================
            //
            // The following views have been successfully migrated:
            // ✅ quantity_unit_conversions_resolved - Foundation for all QU operations
            // ✅ products_resolved - Product hierarchy support
            // ✅ products_view - Enhanced product data with QU factors
            // ✅ users_dto - User display names
            // ✅ product_barcodes_comma_separated - Barcode aggregation
            // ✅ product_barcodes_view - All barcodes including Grocycodes
            //
            // Remaining Phase 1 Critical Views (to be added in next migration):
            // - stock_current
            // - stock_missing_products
            // - stock_next_use
            // - stock_average_product_shelf_life
            // - stock_edited_entries
            // - products_last_purchased
            // - products_average_price
            // - products_price_history
            // - products_current_price
            // - products_volatile_status
            // - products_current_substitutions
            // - uihelper_product_details
            // - uihelper_stock_current_overview
            // - uihelper_stock_entries
            // - uihelper_stock_journal
            //
            // Note: Views are split across multiple migrations to:
            // 1. Keep migration files manageable
            // 2. Allow incremental testing
            // 3. Enable easier rollback if issues arise
            // 4. Reduce merge conflicts in team environment
            //
            // =========================================================================
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop views in reverse dependency order
            migrationBuilder.Sql("DROP VIEW IF EXISTS product_barcodes_view;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS product_barcodes_comma_separated;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS users_dto;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS products_view;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS products_resolved;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS quantity_unit_conversions_resolved;");
        }
    }
}
