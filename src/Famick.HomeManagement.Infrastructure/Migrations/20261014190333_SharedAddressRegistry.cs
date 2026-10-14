using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <summary>
    /// Issue #119: one shared, never-edited <c>addresses</c> row per address.
    /// <list type="number">
    /// <item>Lines 2-4 move off the shared row onto whoever references it: the contact link
    /// and, for the household's own address, the tenant.</item>
    /// <item>Rows with the same hash merge into one, preferring a provider-verified row, then
    /// the oldest. References move to the survivor and the duplicates are deleted.</item>
    /// <item>The hash index becomes unique, so the table cannot drift back.</item>
    /// </list>
    /// </summary>
    public partial class SharedAddressRegistry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "address_line_2",
                table: "tenants",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "address_line_3",
                table: "tenants",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "address_line_4",
                table: "tenants",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressLine3",
                table: "contact_addresses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressLine4",
                table: "contact_addresses",
                type: "text",
                nullable: true);

            // 1. Copy the per-person lines down before the rows they sit on merge or lose them.
            //    A contact's own line 2 already wins over the row's (AddContactAddressLine2).
            migrationBuilder.Sql(@"
                UPDATE contact_addresses ca
                SET ""AddressLine2"" = COALESCE(NULLIF(ca.""AddressLine2"", ''), a.address_line_2),
                    ""AddressLine3"" = a.address_line_3,
                    ""AddressLine4"" = a.address_line_4
                FROM addresses a
                WHERE a.id = ca.""AddressId"";

                UPDATE tenants t
                SET address_line_2 = a.address_line_2,
                    address_line_3 = a.address_line_3,
                    address_line_4 = a.address_line_4
                FROM addresses a
                WHERE a.id = t.address_id;
            ");

            // 2. Merge rows that share a hash.
            migrationBuilder.Sql(@"
                CREATE TEMP TABLE address_merge AS
                SELECT id AS duplicate_id, survivor_id
                FROM (
                    SELECT id,
                           first_value(id) OVER (
                               PARTITION BY normalized_hash
                               ORDER BY (provider_source IS NOT NULL AND provider_source <> 'Unknown') DESC,
                                        created_at, id) AS survivor_id
                    FROM addresses
                    WHERE normalized_hash IS NOT NULL
                ) ranked
                WHERE id <> survivor_id;

                -- A contact links each address once. Where it would end up linking the survivor
                -- twice, keep one link: its existing link to the survivor, else the oldest link.
                CREATE TEMP TABLE address_merge_dropped_links AS
                SELECT ca.""Id"" AS id, ca.""ContactId"" AS contact_id, ca.""IsPrimary"" AS is_primary
                FROM contact_addresses ca
                JOIN address_merge m ON m.duplicate_id = ca.""AddressId""
                WHERE EXISTS (
                    SELECT 1 FROM contact_addresses o
                    WHERE o.""ContactId"" = ca.""ContactId"" AND o.""AddressId"" = m.survivor_id)
                   OR EXISTS (
                    SELECT 1 FROM contact_addresses o
                    JOIN address_merge om ON om.duplicate_id = o.""AddressId""
                    WHERE o.""ContactId"" = ca.""ContactId""
                      AND om.survivor_id = m.survivor_id
                      AND (o.""CreatedAt"", o.""Id"") < (ca.""CreatedAt"", ca.""Id""));

                DELETE FROM contact_addresses ca
                USING address_merge_dropped_links d
                WHERE ca.""Id"" = d.id;

                UPDATE contact_addresses ca
                SET ""AddressId"" = m.survivor_id
                FROM address_merge m
                WHERE ca.""AddressId"" = m.duplicate_id;

                -- A contact whose primary link was the one dropped gets its oldest remaining
                -- link as primary.
                UPDATE contact_addresses ca
                SET ""IsPrimary"" = true
                WHERE ca.""Id"" IN (
                    SELECT DISTINCT ON (c.""ContactId"") c.""Id""
                    FROM contact_addresses c
                    WHERE c.""ContactId"" IN (SELECT contact_id FROM address_merge_dropped_links WHERE is_primary)
                      AND NOT EXISTS (
                          SELECT 1 FROM contact_addresses p
                          WHERE p.""ContactId"" = c.""ContactId"" AND p.""IsPrimary"")
                    ORDER BY c.""ContactId"", c.""CreatedAt"", c.""Id"");

                UPDATE tenants t
                SET address_id = m.survivor_id
                FROM address_merge m
                WHERE t.address_id = m.duplicate_id;

                DELETE FROM addresses a
                USING address_merge m
                WHERE a.id = m.duplicate_id;

                DROP TABLE address_merge_dropped_links;
                DROP TABLE address_merge;
            ");

            migrationBuilder.DropIndex(
                name: "ix_addresses_normalized_hash",
                table: "addresses");

            migrationBuilder.DropColumn(
                name: "address_line_2",
                table: "addresses");

            migrationBuilder.DropColumn(
                name: "address_line_3",
                table: "addresses");

            migrationBuilder.DropColumn(
                name: "address_line_4",
                table: "addresses");

            migrationBuilder.CreateIndex(
                name: "ix_addresses_normalized_hash",
                table: "addresses",
                column: "normalized_hash",
                unique: true,
                filter: "normalized_hash IS NOT NULL");
        }

        /// <inheritdoc />
        /// <remarks>
        /// Restores the columns and the non-unique index. Merged rows stay merged, and the
        /// lines moved onto contacts and tenants stay there.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_addresses_normalized_hash",
                table: "addresses");

            migrationBuilder.DropColumn(
                name: "address_line_2",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "address_line_3",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "address_line_4",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "AddressLine3",
                table: "contact_addresses");

            migrationBuilder.DropColumn(
                name: "AddressLine4",
                table: "contact_addresses");

            migrationBuilder.AddColumn<string>(
                name: "address_line_2",
                table: "addresses",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "address_line_3",
                table: "addresses",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "address_line_4",
                table: "addresses",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_addresses_normalized_hash",
                table: "addresses",
                column: "normalized_hash");
        }
    }
}
