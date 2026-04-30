using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameAddressProviderColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rename the misnamed `geoapify_place_id` column. The column has
            // long held provider-issued opaque place IDs from any provider
            // (Smarty as well as Geoapify) — the old name was misleading.
            migrationBuilder.RenameColumn(
                name: "geoapify_place_id",
                table: "addresses",
                newName: "provider_place_id");

            // New column tracking which provider verified the address.
            // Mirrors `IAddressAutocompleteProvider.ProviderName` ("Smarty"
            // / "Geoapify") for verified rows; null for hand-entered.
            migrationBuilder.AddColumn<string>(
                name: "provider_source",
                table: "addresses",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            // Backfill: rows that already had a place ID came from some
            // provider, but historically the system didn't track which one,
            // so we mark them "Unknown". Going forward writes set this
            // column explicitly. The Unverified-vs-Verified provenance check
            // in AddressHasher treats any non-null ProviderSource as
            // verified, matching today's behavior for these legacy rows.
            migrationBuilder.Sql(@"
                UPDATE addresses
                SET provider_source = 'Unknown'
                WHERE provider_place_id IS NOT NULL
                  AND provider_source IS NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "provider_source",
                table: "addresses");

            migrationBuilder.RenameColumn(
                name: "provider_place_id",
                table: "addresses",
                newName: "geoapify_place_id");
        }
    }
}
