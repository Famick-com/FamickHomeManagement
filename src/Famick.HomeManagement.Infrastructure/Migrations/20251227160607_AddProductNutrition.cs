using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductNutrition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "product_nutrition",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    data_source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    serving_size = table.Column<decimal>(type: "numeric(10,3)", nullable: true),
                    serving_unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    calories = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    total_fat = table.Column<decimal>(type: "numeric(10,3)", nullable: true),
                    saturated_fat = table.Column<decimal>(type: "numeric(10,3)", nullable: true),
                    trans_fat = table.Column<decimal>(type: "numeric(10,3)", nullable: true),
                    cholesterol = table.Column<decimal>(type: "numeric(10,3)", nullable: true),
                    sodium = table.Column<decimal>(type: "numeric(10,3)", nullable: true),
                    total_carbohydrates = table.Column<decimal>(type: "numeric(10,3)", nullable: true),
                    dietary_fiber = table.Column<decimal>(type: "numeric(10,3)", nullable: true),
                    total_sugars = table.Column<decimal>(type: "numeric(10,3)", nullable: true),
                    added_sugars = table.Column<decimal>(type: "numeric(10,3)", nullable: true),
                    protein = table.Column<decimal>(type: "numeric(10,3)", nullable: true),
                    vitamin_a = table.Column<decimal>(type: "numeric(10,3)", nullable: true),
                    vitamin_c = table.Column<decimal>(type: "numeric(10,3)", nullable: true),
                    vitamin_d = table.Column<decimal>(type: "numeric(10,3)", nullable: true),
                    vitamin_e = table.Column<decimal>(type: "numeric(10,3)", nullable: true),
                    vitamin_k = table.Column<decimal>(type: "numeric(10,3)", nullable: true),
                    thiamin = table.Column<decimal>(type: "numeric(10,3)", nullable: true),
                    riboflavin = table.Column<decimal>(type: "numeric(10,3)", nullable: true),
                    niacin = table.Column<decimal>(type: "numeric(10,3)", nullable: true),
                    vitamin_b6 = table.Column<decimal>(type: "numeric(10,3)", nullable: true),
                    folate = table.Column<decimal>(type: "numeric(10,3)", nullable: true),
                    vitamin_b12 = table.Column<decimal>(type: "numeric(10,3)", nullable: true),
                    calcium = table.Column<decimal>(type: "numeric(10,3)", nullable: true),
                    iron = table.Column<decimal>(type: "numeric(10,3)", nullable: true),
                    magnesium = table.Column<decimal>(type: "numeric(10,3)", nullable: true),
                    phosphorus = table.Column<decimal>(type: "numeric(10,3)", nullable: true),
                    potassium = table.Column<decimal>(type: "numeric(10,3)", nullable: true),
                    zinc = table.Column<decimal>(type: "numeric(10,3)", nullable: true),
                    brand_owner = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    brand_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ingredients = table.Column<string>(type: "text", nullable: true),
                    serving_size_description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    last_updated_from_source = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_nutrition", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_nutrition_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_product_nutrition_data_source_external_id",
                table: "product_nutrition",
                columns: new[] { "data_source", "external_id" });

            migrationBuilder.CreateIndex(
                name: "ix_product_nutrition_tenant_id",
                table: "product_nutrition",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_product_nutrition_product_id",
                table: "product_nutrition",
                column: "product_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_nutrition");
        }
    }
}
