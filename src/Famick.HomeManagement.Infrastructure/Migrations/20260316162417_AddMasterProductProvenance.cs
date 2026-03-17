using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMasterProductProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "contributed_by_email",
                table: "master_products",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "contributed_by_tenant_id",
                table: "master_products",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "source",
                table: "master_products",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "ix_master_products_source",
                table: "master_products",
                column: "source");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_master_products_source",
                table: "master_products");

            migrationBuilder.DropColumn(
                name: "contributed_by_email",
                table: "master_products");

            migrationBuilder.DropColumn(
                name: "contributed_by_tenant_id",
                table: "master_products");

            migrationBuilder.DropColumn(
                name: "source",
                table: "master_products");
        }
    }
}
