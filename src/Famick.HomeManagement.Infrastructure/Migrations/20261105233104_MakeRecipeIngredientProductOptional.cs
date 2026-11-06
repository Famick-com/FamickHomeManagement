using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MakeRecipeIngredientProductOptional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "ProductId",
                table: "recipe_positions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "OriginalText",
                table: "recipe_positions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The older schema can't hold a line without a product, and the empty GUID below would fail the
            // product foreign key, so lines kept as text are removed rather than restored.
            migrationBuilder.Sql("DELETE FROM recipe_positions WHERE \"ProductId\" IS NULL;");

            migrationBuilder.DropColumn(
                name: "OriginalText",
                table: "recipe_positions");

            migrationBuilder.AlterColumn<Guid>(
                name: "ProductId",
                table: "recipe_positions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
