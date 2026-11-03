using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <summary>
    /// Lets a quantity unit say which standard measure it is, so amounts in it convert to others of the
    /// same kind, and tags the units every household was seeded with.
    /// </summary>
    /// <remarks>
    /// Existing units are tagged only when their name is one of the seeded defaults, in any case
    /// ("cup", "CUP"). Anything else, such as "Tbsp" or "c", is left for the household to tag in unit
    /// settings rather than guessed at. The seeded gallon, quart and pint were described as imperial;
    /// they are converted as US measures, so their descriptions change too, where nobody edited them.
    /// </remarks>
    public partial class AddStandardUnitToQuantityUnits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StandardUnit",
                table: "quantity_units",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE quantity_units AS unit
                SET "StandardUnit" = standard.tag
                FROM (VALUES
                    ('teaspoon', 'Teaspoon'), ('tablespoon', 'Tablespoon'), ('fluid ounce', 'FluidOunce'),
                    ('cup', 'Cup'), ('pint', 'Pint'), ('quart', 'Quart'), ('gallon', 'Gallon'),
                    ('milliliter', 'Milliliter'), ('liter', 'Liter'),
                    ('gram', 'Gram'), ('kilogram', 'Kilogram'), ('ounce', 'Ounce'), ('pound', 'Pound')
                ) AS standard(name, tag)
                WHERE lower(unit."Name") = standard.name;
                """);

            migrationBuilder.Sql("""
                UPDATE quantity_units AS unit
                SET "Description" = seeded.now
                FROM (VALUES
                    ('gal - imperial volume', 'gal - US volume'),
                    ('qt - imperial volume', 'qt - US volume'),
                    ('pt - imperial volume', 'pt - US volume')
                ) AS seeded(was, now)
                WHERE unit."Description" = seeded.was;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The descriptions stay: "US volume" is what those units are.
            migrationBuilder.DropColumn(
                name: "StandardUnit",
                table: "quantity_units");
        }
    }
}
