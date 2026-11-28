using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogChildScans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "master_product_id",
                table: "shopping_list",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCatalogChild",
                table: "product_barcodes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_shopping_list_master_product_id",
                table: "shopping_list",
                column: "master_product_id");

            migrationBuilder.AddForeignKey(
                name: "fk_shopping_list_master_products",
                table: "shopping_list",
                column: "master_product_id",
                principalTable: "master_products",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_shopping_list_master_products",
                table: "shopping_list");

            migrationBuilder.DropIndex(
                name: "ix_shopping_list_master_product_id",
                table: "shopping_list");

            migrationBuilder.DropColumn(
                name: "master_product_id",
                table: "shopping_list");

            migrationBuilder.DropColumn(
                name: "IsCatalogChild",
                table: "product_barcodes");
        }
    }
}
