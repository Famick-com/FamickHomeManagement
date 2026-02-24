using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServingFieldsToProduct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ServingSize",
                table: "products",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ServingUnit",
                table: "products",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ServingsPerContainer",
                table: "products",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ServingSize",
                table: "products");

            migrationBuilder.DropColumn(
                name: "ServingUnit",
                table: "products");

            migrationBuilder.DropColumn(
                name: "ServingsPerContainer",
                table: "products");
        }
    }
}
