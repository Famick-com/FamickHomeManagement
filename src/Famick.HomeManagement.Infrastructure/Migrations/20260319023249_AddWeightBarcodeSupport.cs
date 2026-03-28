using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWeightBarcodeSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "type2_item_number_start",
                table: "shopping_locations",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "SaleType",
                table: "products",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Type2Prefix",
                table: "product_barcodes",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "sale_type",
                table: "master_products",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "type2_prefix",
                table: "master_product_barcodes",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "type2_item_number_start",
                table: "shopping_locations");

            migrationBuilder.DropColumn(
                name: "SaleType",
                table: "products");

            migrationBuilder.DropColumn(
                name: "Type2Prefix",
                table: "product_barcodes");

            migrationBuilder.DropColumn(
                name: "sale_type",
                table: "master_products");

            migrationBuilder.DropColumn(
                name: "type2_prefix",
                table: "master_product_barcodes");
        }
    }
}
