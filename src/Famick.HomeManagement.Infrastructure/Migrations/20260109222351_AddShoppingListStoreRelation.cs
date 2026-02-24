using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddShoppingListStoreRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_shopping_lists_tenant_name",
                table: "shopping_lists");

            migrationBuilder.AddColumn<Guid>(
                name: "shopping_location_id",
                table: "shopping_lists",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "aisle",
                table: "shopping_list",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "department",
                table: "shopping_list",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "external_product_id",
                table: "shopping_list",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_purchased",
                table: "shopping_list",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "purchased_at",
                table: "shopping_list",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "shelf",
                table: "shopping_list",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_shopping_lists_shopping_location_id",
                table: "shopping_lists",
                column: "shopping_location_id");

            migrationBuilder.CreateIndex(
                name: "ux_shopping_lists_tenant_location_name",
                table: "shopping_lists",
                columns: new[] { "tenant_id", "shopping_location_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_shopping_list_purchased",
                table: "shopping_list",
                columns: new[] { "shopping_list_id", "is_purchased" });

            migrationBuilder.AddForeignKey(
                name: "fk_shopping_lists_shopping_locations",
                table: "shopping_lists",
                column: "shopping_location_id",
                principalTable: "shopping_locations",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_shopping_lists_shopping_locations",
                table: "shopping_lists");

            migrationBuilder.DropIndex(
                name: "ix_shopping_lists_shopping_location_id",
                table: "shopping_lists");

            migrationBuilder.DropIndex(
                name: "ux_shopping_lists_tenant_location_name",
                table: "shopping_lists");

            migrationBuilder.DropIndex(
                name: "ix_shopping_list_purchased",
                table: "shopping_list");

            migrationBuilder.DropColumn(
                name: "shopping_location_id",
                table: "shopping_lists");

            migrationBuilder.DropColumn(
                name: "aisle",
                table: "shopping_list");

            migrationBuilder.DropColumn(
                name: "department",
                table: "shopping_list");

            migrationBuilder.DropColumn(
                name: "external_product_id",
                table: "shopping_list");

            migrationBuilder.DropColumn(
                name: "is_purchased",
                table: "shopping_list");

            migrationBuilder.DropColumn(
                name: "purchased_at",
                table: "shopping_list");

            migrationBuilder.DropColumn(
                name: "shelf",
                table: "shopping_list");

            migrationBuilder.CreateIndex(
                name: "ux_shopping_lists_tenant_name",
                table: "shopping_lists",
                columns: new[] { "tenant_id", "name" },
                unique: true);
        }
    }
}
