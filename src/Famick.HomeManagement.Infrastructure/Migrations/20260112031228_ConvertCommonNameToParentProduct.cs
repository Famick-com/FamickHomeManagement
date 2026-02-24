using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConvertCommonNameToParentProduct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_products_product_common_names_ProductCommonNameId",
                table: "products");

            // Clear existing ProductCommonNameId values since they reference the old table
            // (starting fresh - no data migration from CommonName to ParentProduct)
            migrationBuilder.Sql("UPDATE products SET \"ProductCommonNameId\" = NULL WHERE \"ProductCommonNameId\" IS NOT NULL");

            migrationBuilder.DropTable(
                name: "product_common_names");

            migrationBuilder.RenameColumn(
                name: "ProductCommonNameId",
                table: "products",
                newName: "ParentProductId");

            migrationBuilder.RenameIndex(
                name: "IX_products_ProductCommonNameId",
                table: "products",
                newName: "ix_products_parent_product_id");

            migrationBuilder.AddForeignKey(
                name: "fk_products_parent_product",
                table: "products",
                column: "ParentProductId",
                principalTable: "products",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_products_parent_product",
                table: "products");

            migrationBuilder.RenameColumn(
                name: "ParentProductId",
                table: "products",
                newName: "ProductCommonNameId");

            migrationBuilder.RenameIndex(
                name: "ix_products_parent_product_id",
                table: "products",
                newName: "IX_products_ProductCommonNameId");

            migrationBuilder.CreateTable(
                name: "product_common_names",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    description = table.Column<string>(type: "text", nullable: true),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_common_names", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_product_common_names_tenant_id",
                table: "product_common_names",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_product_common_names_tenant_name",
                table: "product_common_names",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_products_product_common_names_ProductCommonNameId",
                table: "products",
                column: "ProductCommonNameId",
                principalTable: "product_common_names",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
