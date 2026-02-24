using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductImageExternalUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalSource",
                table: "product_images",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalThumbnailUrl",
                table: "product_images",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalUrl",
                table: "product_images",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExternalSource",
                table: "product_images");

            migrationBuilder.DropColumn(
                name: "ExternalThumbnailUrl",
                table: "product_images");

            migrationBuilder.DropColumn(
                name: "ExternalUrl",
                table: "product_images");
        }
    }
}
