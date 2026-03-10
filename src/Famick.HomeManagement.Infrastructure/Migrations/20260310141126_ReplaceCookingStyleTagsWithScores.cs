using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceCookingStyleTagsWithScores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cooking_style_tags",
                table: "master_products");

            migrationBuilder.AddColumn<int>(
                name: "convenience_score",
                table: "master_products",
                type: "integer",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<int>(
                name: "health_score",
                table: "master_products",
                type: "integer",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<int>(
                name: "organic_score",
                table: "master_products",
                type: "integer",
                nullable: false,
                defaultValue: 3);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "convenience_score",
                table: "master_products");

            migrationBuilder.DropColumn(
                name: "health_score",
                table: "master_products");

            migrationBuilder.DropColumn(
                name: "organic_score",
                table: "master_products");

            migrationBuilder.AddColumn<string>(
                name: "cooking_style_tags",
                table: "master_products",
                type: "text",
                nullable: false,
                defaultValue: "[]");
        }
    }
}
