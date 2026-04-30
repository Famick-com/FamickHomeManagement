using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddContactAddressLine2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AddressLine2",
                table: "contact_addresses",
                type: "text",
                nullable: true);

            // Backfill: copy any existing apt/suite values off the shared
            // Addresses table onto each ContactAddress link so the per-contact
            // value isn't lost when ContactService stops writing to
            // Address.address_line_2 going forward.
            migrationBuilder.Sql(@"
                UPDATE contact_addresses ca
                SET ""AddressLine2"" = a.address_line_2
                FROM addresses a
                WHERE ca.""AddressId"" = a.id
                  AND a.address_line_2 IS NOT NULL
                  AND a.address_line_2 <> '';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AddressLine2",
                table: "contact_addresses");
        }
    }
}
