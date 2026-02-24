using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductCommonName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProductCommonNameId",
                table: "products",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "product_common_names",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_common_names", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_products_ProductCommonNameId",
                table: "products",
                column: "ProductCommonNameId");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_products_product_common_names_ProductCommonNameId",
                table: "products");

            migrationBuilder.DropTable(
                name: "product_common_names");

            migrationBuilder.DropIndex(
                name: "IX_products_ProductCommonNameId",
                table: "products");

            migrationBuilder.DropColumn(
                name: "ProductCommonNameId",
                table: "products");
        }
    }
}
