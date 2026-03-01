using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMealPlanner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DietaryNotes",
                table: "contacts",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "contact_allergens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    contact_id = table.Column<Guid>(type: "uuid", nullable: false),
                    allergen_type = table.Column<int>(type: "integer", nullable: false),
                    severity = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contact_allergens", x => x.id);
                    table.ForeignKey(
                        name: "fk_contact_allergens_contact",
                        column: x => x.contact_id,
                        principalTable: "contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "contact_dietary_preferences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    contact_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dietary_preference = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contact_dietary_preferences", x => x.id);
                    table.ForeignKey(
                        name: "fk_contact_dietary_prefs_contact",
                        column: x => x.contact_id,
                        principalTable: "contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "meal_plans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    week_start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meal_plans", x => x.id);
                    table.ForeignKey(
                        name: "fk_meal_plans_updated_by_user",
                        column: x => x.updated_by_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "meal_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_default = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    color = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meal_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "meals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    is_favorite = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meals", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "product_allergens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    allergen_type = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_allergens", x => x.id);
                    table.ForeignKey(
                        name: "fk_product_allergens_product",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_dietary_conflicts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dietary_preference = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_dietary_conflicts", x => x.id);
                    table.ForeignKey(
                        name: "fk_product_dietary_conflicts_product",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_meal_planner_preferences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    has_completed_onboarding = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    planning_style = table.Column<int>(type: "integer", nullable: true),
                    collapsed_meal_type_ids = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_meal_planner_preferences", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_meal_planner_prefs_user",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_meal_planner_tips",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tip_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    dismissed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_meal_planner_tips", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_meal_planner_tips_user",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "meal_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    meal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_type = table.Column<int>(type: "integer", nullable: false),
                    recipe_id = table.Column<Guid>(type: "uuid", nullable: true),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    product_quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    product_quantity_unit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    freetext_description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meal_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_meal_items_meal",
                        column: x => x.meal_id,
                        principalTable: "meals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_meal_items_product",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_meal_items_quantity_unit",
                        column: x => x.product_quantity_unit_id,
                        principalTable: "quantity_units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_meal_items_recipe",
                        column: x => x.recipe_id,
                        principalTable: "recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "meal_plan_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    meal_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    meal_id = table.Column<Guid>(type: "uuid", nullable: true),
                    inline_note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    meal_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day_of_week = table.Column<int>(type: "integer", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_batch_source = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    batch_source_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meal_plan_entries", x => x.id);
                    table.CheckConstraint("ck_meal_plan_entries_batch_exclusive", "NOT (is_batch_source = true AND batch_source_entry_id IS NOT NULL)");
                    table.CheckConstraint("ck_meal_plan_entries_batch_requires_meal", "(is_batch_source = false AND batch_source_entry_id IS NULL) OR meal_id IS NOT NULL");
                    table.CheckConstraint("ck_meal_plan_entries_meal_or_note", "(meal_id IS NOT NULL AND inline_note IS NULL) OR (meal_id IS NULL AND inline_note IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_meal_plan_entries_batch_source",
                        column: x => x.batch_source_entry_id,
                        principalTable: "meal_plan_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_meal_plan_entries_meal",
                        column: x => x.meal_id,
                        principalTable: "meals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_meal_plan_entries_meal_type",
                        column: x => x.meal_type_id,
                        principalTable: "meal_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_meal_plan_entries_plan",
                        column: x => x.meal_plan_id,
                        principalTable: "meal_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_contact_allergens_contact_type",
                table: "contact_allergens",
                columns: new[] { "contact_id", "allergen_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_contact_dietary_prefs_contact_pref",
                table: "contact_dietary_preferences",
                columns: new[] { "contact_id", "dietary_preference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_meal_items_meal_id",
                table: "meal_items",
                column: "meal_id");

            migrationBuilder.CreateIndex(
                name: "IX_meal_items_product_id",
                table: "meal_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_meal_items_product_quantity_unit_id",
                table: "meal_items",
                column: "product_quantity_unit_id");

            migrationBuilder.CreateIndex(
                name: "IX_meal_items_recipe_id",
                table: "meal_items",
                column: "recipe_id");

            migrationBuilder.CreateIndex(
                name: "IX_meal_plan_entries_batch_source_entry_id",
                table: "meal_plan_entries",
                column: "batch_source_entry_id");

            migrationBuilder.CreateIndex(
                name: "IX_meal_plan_entries_meal_id",
                table: "meal_plan_entries",
                column: "meal_id");

            migrationBuilder.CreateIndex(
                name: "IX_meal_plan_entries_meal_type_id",
                table: "meal_plan_entries",
                column: "meal_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_meal_plan_entries_plan_day_type",
                table: "meal_plan_entries",
                columns: new[] { "meal_plan_id", "day_of_week", "meal_type_id" });

            migrationBuilder.CreateIndex(
                name: "ix_meal_plan_entries_plan_id",
                table: "meal_plan_entries",
                column: "meal_plan_id");

            migrationBuilder.CreateIndex(
                name: "ix_meal_plans_tenant_id",
                table: "meal_plans",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_meal_plans_updated_by_user_id",
                table: "meal_plans",
                column: "updated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_meal_plans_tenant_week",
                table: "meal_plans",
                columns: new[] { "tenant_id", "week_start_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_meal_types_tenant_id",
                table: "meal_types",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_meal_types_tenant_name",
                table: "meal_types",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_meals_tenant_favorite",
                table: "meals",
                columns: new[] { "tenant_id", "is_favorite" });

            migrationBuilder.CreateIndex(
                name: "ix_meals_tenant_id",
                table: "meals",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_meals_tenant_name",
                table: "meals",
                columns: new[] { "tenant_id", "name" });

            migrationBuilder.CreateIndex(
                name: "ux_product_allergens_product_type",
                table: "product_allergens",
                columns: new[] { "product_id", "allergen_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_product_dietary_conflicts_product_pref",
                table: "product_dietary_conflicts",
                columns: new[] { "product_id", "dietary_preference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_user_meal_planner_prefs_user",
                table: "user_meal_planner_preferences",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_user_meal_planner_tips_user_key",
                table: "user_meal_planner_tips",
                columns: new[] { "user_id", "tip_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "contact_allergens");

            migrationBuilder.DropTable(
                name: "contact_dietary_preferences");

            migrationBuilder.DropTable(
                name: "meal_items");

            migrationBuilder.DropTable(
                name: "meal_plan_entries");

            migrationBuilder.DropTable(
                name: "product_allergens");

            migrationBuilder.DropTable(
                name: "product_dietary_conflicts");

            migrationBuilder.DropTable(
                name: "user_meal_planner_preferences");

            migrationBuilder.DropTable(
                name: "user_meal_planner_tips");

            migrationBuilder.DropTable(
                name: "meals");

            migrationBuilder.DropTable(
                name: "meal_types");

            migrationBuilder.DropTable(
                name: "meal_plans");

            migrationBuilder.DropColumn(
                name: "DietaryNotes",
                table: "contacts");
        }
    }
}
