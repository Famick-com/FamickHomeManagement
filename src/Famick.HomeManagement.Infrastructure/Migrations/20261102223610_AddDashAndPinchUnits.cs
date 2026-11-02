using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <summary>
    /// Adds the Dash and Pinch cooking measures to every household that already has quantity units.
    /// DataSeeder gives them to new households, but it only seeds a household with no units at all,
    /// so existing ones would never get them.
    /// </summary>
    /// <remarks>
    /// A household that already has a unit by either name, in any case ("dash"), is left alone: the
    /// unique index on ("TenantId", "Name") is case-sensitive, so it wouldn't stop a second one.
    /// Households come from quantity_units itself, so this doesn't depend on how tenants are stored;
    /// one with no units at all gets the full set, Dash and Pinch included, from the seeder.
    /// </remarks>
    public partial class AddDashAndPinchUnits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO quantity_units ("Id", "TenantId", "Name", "NamePlural", "Description", "IsActive", "CreatedAt")
                SELECT gen_random_uuid(), households."TenantId", unit.name, unit.plural, 'cooking measurement', true, now()
                FROM (SELECT DISTINCT "TenantId" FROM quantity_units) AS households
                CROSS JOIN (VALUES ('Dash', 'Dashes'), ('Pinch', 'Pinches')) AS unit(name, plural)
                WHERE NOT EXISTS (
                    SELECT 1 FROM quantity_units existing
                    WHERE existing."TenantId" = households."TenantId"
                      AND lower(existing."Name") = lower(unit.name));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing: by then recipes and products may use these units, and the households that had
            // their own Dash or Pinch before can't be told apart from the ones this added.
        }
    }
}
