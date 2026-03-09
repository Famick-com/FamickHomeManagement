using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMasterProductsAndOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "brand",
                table: "products",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "master_product_id",
                table: "products",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "overridden_fields",
                table: "products",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "master_products",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    brand = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    container_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    grams_per_tbsp = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    icon_svg = table.Column<string>(type: "text", nullable: true),
                    serving_size = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    serving_unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    servings_per_container = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    default_best_before_days = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    tracks_best_before_date = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_staple = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    popularity = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    lifestyle_tags = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    allergen_flags = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    dietary_conflict_flags = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    cooking_style_tags = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    default_location_hint = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    default_quantity_unit_hint = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    data_source_attribution = table.Column<string>(type: "text", nullable: true),
                    parent_master_product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_master_products", x => x.id);
                    table.ForeignKey(
                        name: "FK_master_products_master_products_parent_master_product_id",
                        column: x => x.parent_master_product_id,
                        principalTable: "master_products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "tenant_product_onboarding_states",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    has_completed_onboarding = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    questionnaire_answers_json = table.Column<string>(type: "jsonb", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    products_created_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_product_onboarding_states", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "master_product_barcodes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    master_product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    barcode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_master_product_barcodes", x => x.id);
                    table.ForeignKey(
                        name: "FK_master_product_barcodes_master_products_master_product_id",
                        column: x => x.master_product_id,
                        principalTable: "master_products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "master_product_images",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    master_product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    original_file_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    file_size = table.Column<long>(type: "bigint", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    external_url = table.Column<string>(type: "text", nullable: true),
                    external_thumbnail_url = table.Column<string>(type: "text", nullable: true),
                    external_source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_master_product_images", x => x.id);
                    table.ForeignKey(
                        name: "FK_master_product_images_master_products_master_product_id",
                        column: x => x.master_product_id,
                        principalTable: "master_products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "master_product_nutrition",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    master_product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    data_source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    serving_size = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    serving_unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    servings_per_container = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    calories = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    total_fat = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    saturated_fat = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    trans_fat = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    cholesterol = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    sodium = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    total_carbohydrates = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    dietary_fiber = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    total_sugars = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    added_sugars = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    protein = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    vitamin_a = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    vitamin_c = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    vitamin_d = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    vitamin_e = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    vitamin_k = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    thiamin = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    riboflavin = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    niacin = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    vitamin_b6 = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    folate = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    vitamin_b12 = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    calcium = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    iron = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    magnesium = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    phosphorus = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    potassium = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    zinc = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    brand_owner = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    brand_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ingredients = table.Column<string>(type: "text", nullable: true),
                    serving_size_description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    last_updated_from_source = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_master_product_nutrition", x => x.id);
                    table.ForeignKey(
                        name: "FK_master_product_nutrition_master_products_master_product_id",
                        column: x => x.master_product_id,
                        principalTable: "master_products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_products_master_product_id",
                table: "products",
                column: "master_product_id");

            migrationBuilder.CreateIndex(
                name: "IX_master_product_barcodes_master_product_id",
                table: "master_product_barcodes",
                column: "master_product_id");

            migrationBuilder.CreateIndex(
                name: "ux_master_product_barcodes_barcode",
                table: "master_product_barcodes",
                column: "barcode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_master_product_images_master_product_id",
                table: "master_product_images",
                column: "master_product_id");

            migrationBuilder.CreateIndex(
                name: "ux_master_product_nutrition_master_product_id",
                table: "master_product_nutrition",
                column: "master_product_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_master_products_parent_master_product_id",
                table: "master_products",
                column: "parent_master_product_id");

            migrationBuilder.CreateIndex(
                name: "ux_master_products_name_category_brand",
                table: "master_products",
                columns: new[] { "name", "category", "brand" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_tenant_product_onboarding_states_tenant_id",
                table: "tenant_product_onboarding_states",
                column: "tenant_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_products_master_products_master_product_id",
                table: "products",
                column: "master_product_id",
                principalTable: "master_products",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_products_master_products_master_product_id",
                table: "products");

            migrationBuilder.DropTable(
                name: "master_product_barcodes");

            migrationBuilder.DropTable(
                name: "master_product_images");

            migrationBuilder.DropTable(
                name: "master_product_nutrition");

            migrationBuilder.DropTable(
                name: "tenant_product_onboarding_states");

            migrationBuilder.DropTable(
                name: "master_products");

            migrationBuilder.DropIndex(
                name: "ix_products_master_product_id",
                table: "products");

            migrationBuilder.DropColumn(
                name: "brand",
                table: "products");

            migrationBuilder.DropColumn(
                name: "master_product_id",
                table: "products");

            migrationBuilder.DropColumn(
                name: "overridden_fields",
                table: "products");
        }
    }
}
