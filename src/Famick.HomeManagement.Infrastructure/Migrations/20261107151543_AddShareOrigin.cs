using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddShareOrigin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OriginKey",
                table: "recipes",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SharedByHouseholdName",
                table: "recipes",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginKey",
                table: "products",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SharedByHouseholdName",
                table: "products",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "origin_key",
                table: "meals",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "shared_by_household_name",
                table: "meals",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginKey",
                table: "contacts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SharedByHouseholdName",
                table: "contacts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_recipes_tenant_origin",
                table: "recipes",
                columns: new[] { "TenantId", "OriginKey" },
                unique: true,
                filter: "\"OriginKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_products_tenant_origin",
                table: "products",
                columns: new[] { "TenantId", "OriginKey" },
                unique: true,
                filter: "\"OriginKey\" IS NOT NULL AND \"IsActive\" = true");

            migrationBuilder.CreateIndex(
                name: "ux_meals_tenant_origin",
                table: "meals",
                columns: new[] { "tenant_id", "origin_key" },
                unique: true,
                filter: "origin_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_contacts_tenant_origin",
                table: "contacts",
                columns: new[] { "TenantId", "OriginKey" },
                unique: true,
                filter: "\"OriginKey\" IS NOT NULL AND \"IsActive\" = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_recipes_tenant_origin",
                table: "recipes");

            migrationBuilder.DropIndex(
                name: "ux_products_tenant_origin",
                table: "products");

            migrationBuilder.DropIndex(
                name: "ux_meals_tenant_origin",
                table: "meals");

            migrationBuilder.DropIndex(
                name: "ux_contacts_tenant_origin",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "OriginKey",
                table: "recipes");

            migrationBuilder.DropColumn(
                name: "SharedByHouseholdName",
                table: "recipes");

            migrationBuilder.DropColumn(
                name: "OriginKey",
                table: "products");

            migrationBuilder.DropColumn(
                name: "SharedByHouseholdName",
                table: "products");

            migrationBuilder.DropColumn(
                name: "origin_key",
                table: "meals");

            migrationBuilder.DropColumn(
                name: "shared_by_household_name",
                table: "meals");

            migrationBuilder.DropColumn(
                name: "OriginKey",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "SharedByHouseholdName",
                table: "contacts");
        }
    }
}
