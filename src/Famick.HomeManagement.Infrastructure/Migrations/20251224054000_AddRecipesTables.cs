using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipesTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // =========================================================================
            // PHASE 2 BATCH 3: RECIPES TABLES
            // =========================================================================
            //
            // Creates recipe management functionality with three tables:
            // 1. recipes - Recipe master table
            // 2. recipes_pos - Recipe ingredients/positions
            // 3. recipes_nestings - Hierarchical recipe relationships
            //
            // Features:
            // - Recipe creation with ingredients
            // - Quantity unit conversions for ingredients
            // - Stock fulfillment checking
            // - Hierarchical recipes (recipes that include other recipes)
            // - Ingredient grouping (Dry Ingredients, Wet Ingredients, etc.)
            //
            // Migration Source: Grocy 0025.sql, 0045.sql, 0043.sql
            // Complexity: Medium (includes trigger logic migration notes)
            // =========================================================================

            // -------------------------------------------------------------------------
            // TABLE 1: recipes
            // -------------------------------------------------------------------------
            // Purpose: Recipe master table
            // Examples: "Chocolate Chip Cookies", "Spaghetti Carbonara", "Green Smoothie"
            // -------------------------------------------------------------------------
            migrationBuilder.CreateTable(
                name: "recipes",
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
                    table.PrimaryKey("PK_recipes", x => x.id);
                });

            // Indexes for recipes
            migrationBuilder.CreateIndex(
                name: "ix_recipes_tenant_id",
                table: "recipes",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_recipes_tenant_name",
                table: "recipes",
                columns: new[] { "tenant_id", "name" });

            // -------------------------------------------------------------------------
            // TABLE 2: recipes_pos (Recipe Positions/Ingredients)
            // -------------------------------------------------------------------------
            // Purpose: Ingredients in recipes with amounts and quantity units
            // Features:
            // - Amount and quantity unit per ingredient
            // - Ingredient grouping (Dry Ingredients, Wet Ingredients, etc.)
            // - Stock fulfillment options (only check single unit, don't check)
            // - Optional notes (preparation instructions, substitutions)
            //
            // Trigger Migration Notes:
            // - recipes_pos_qu_id_default (Grocy 0045.sql):
            //   Sets qu_id to product's qu_id_stock if NULL on INSERT
            //   Migration Strategy: Handle in RecipesService.AddIngredient() method
            //
            // - cascade_change_qu_id_stock (Grocy 0045.sql):
            //   Updates qu_id when product's qu_id_stock changes (if only_check_single_unit_in_stock = 0)
            //   Migration Strategy: Handle in ProductsService.UpdateProduct() method
            // -------------------------------------------------------------------------
            migrationBuilder.CreateTable(
                name: "recipes_pos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false, defaultValue: 0),
                    note = table.Column<string>(type: "text", nullable: true),
                    qu_id = table.Column<Guid>(type: "uuid", nullable: true),
                    only_check_single_unit_in_stock = table.Column<short>(type: "smallint", nullable: false, defaultValue: 0),
                    ingredient_group = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    not_check_stock_fulfillment = table.Column<short>(type: "smallint", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recipes_pos", x => x.id);
                    table.ForeignKey(
                        name: "fk_recipes_pos_recipes",
                        column: x => x.recipe_id,
                        principalTable: "recipes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_recipes_pos_products",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_recipes_pos_quantity_units",
                        column: x => x.qu_id,
                        principalTable: "quantity_units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Indexes for recipes_pos
            migrationBuilder.CreateIndex(
                name: "ix_recipes_pos_tenant_id",
                table: "recipes_pos",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_recipes_pos_recipe_id",
                table: "recipes_pos",
                column: "recipe_id");

            migrationBuilder.CreateIndex(
                name: "ix_recipes_pos_product_id",
                table: "recipes_pos",
                column: "product_id");

            // -------------------------------------------------------------------------
            // TABLE 3: recipes_nestings (Hierarchical Recipes)
            // -------------------------------------------------------------------------
            // Purpose: Links recipes that include other recipes
            // Example: "Wedding Cake" includes "Vanilla Buttercream Frosting" recipe
            // Features:
            // - Self-referential relationship (recipe → recipe)
            // - Enables complex recipes built from simpler ones
            // - Prevents duplicate nestings (unique constraint)
            // -------------------------------------------------------------------------
            migrationBuilder.CreateTable(
                name: "recipes_nestings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    includes_recipe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recipes_nestings", x => x.id);
                    table.ForeignKey(
                        name: "fk_recipes_nestings_recipe_id",
                        column: x => x.recipe_id,
                        principalTable: "recipes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_recipes_nestings_includes_recipe_id",
                        column: x => x.includes_recipe_id,
                        principalTable: "recipes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Indexes for recipes_nestings
            migrationBuilder.CreateIndex(
                name: "ix_recipes_nestings_tenant_id",
                table: "recipes_nestings",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_recipes_nestings_recipe_includes",
                table: "recipes_nestings",
                columns: new[] { "recipe_id", "includes_recipe_id" },
                unique: true);

            // =========================================================================
            // PHASE 2 BATCH 3 COMPLETE
            // =========================================================================
            //
            // ✅ recipes table created
            // ✅ recipes_pos table created
            // ✅ recipes_nestings table created
            // ✅ All indexes and constraints created
            //
            // Features Enabled:
            // - Recipe creation with ingredients
            // - Quantity unit per ingredient
            // - Ingredient grouping
            // - Stock fulfillment checking options
            // - Hierarchical recipes (recipes including other recipes)
            //
            // Trigger Migration Strategy:
            // 1. recipes_pos_qu_id_default:
            //    - Original: Auto-sets qu_id to product's qu_id_stock on INSERT
            //    - Migration: Implement in RecipesService.AddIngredient()
            //    - Code: if (position.QuantityUnitId == null)
            //              position.QuantityUnitId = product.QuantityUnitIdStock;
            //
            // 2. cascade_change_qu_id_stock:
            //    - Original: Updates recipes_pos.qu_id when product.qu_id_stock changes
            //    - Migration: Implement in ProductsService.UpdateProduct()
            //    - Code: if (product.QuantityUnitIdStock changed)
            //              Update recipes_pos WHERE product_id = product.Id AND only_check_single_unit_in_stock = 0
            //
            // Next Steps:
            // 1. Implement RecipesService with trigger logic
            // 2. Create recipe fulfillment views (if needed from Grocy)
            // 3. Batch 4: Chores tables
            //
            // =========================================================================
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop tables in reverse dependency order
            migrationBuilder.DropTable(name: "recipes_nestings");
            migrationBuilder.DropTable(name: "recipes_pos");
            migrationBuilder.DropTable(name: "recipes");
        }
    }
}
