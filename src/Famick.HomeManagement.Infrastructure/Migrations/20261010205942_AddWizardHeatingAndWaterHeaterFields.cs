using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWizardHeatingAndWaterHeaterFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ac_type",
                table: "homes",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "heating_type",
                table: "homes",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "water_heater_size",
                table: "homes",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "water_heater_type",
                table: "homes",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ac_type",
                table: "homes");

            migrationBuilder.DropColumn(
                name: "heating_type",
                table: "homes");

            migrationBuilder.DropColumn(
                name: "water_heater_size",
                table: "homes");

            migrationBuilder.DropColumn(
                name: "water_heater_type",
                table: "homes");
        }
    }
}
