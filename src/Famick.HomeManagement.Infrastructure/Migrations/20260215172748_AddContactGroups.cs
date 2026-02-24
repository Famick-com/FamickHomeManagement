using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddContactGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BusinessCategory",
                table: "contacts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactType",
                table: "contacts",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsTenantHousehold",
                table: "contacts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentContactId",
                table: "contacts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UsesGroupAddress",
                table: "contacts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Website",
                table: "contacts",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_contacts_ParentContactId",
                table: "contacts",
                column: "ParentContactId");

            migrationBuilder.CreateIndex(
                name: "IX_contacts_TenantId_ContactType",
                table: "contacts",
                columns: new[] { "TenantId", "ContactType" });

            migrationBuilder.CreateIndex(
                name: "IX_contacts_TenantId_IsTenantHousehold",
                table: "contacts",
                columns: new[] { "TenantId", "IsTenantHousehold" },
                unique: true,
                filter: "\"IsTenantHousehold\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_contacts_TenantId_ParentContactId",
                table: "contacts",
                columns: new[] { "TenantId", "ParentContactId" });

            migrationBuilder.AddForeignKey(
                name: "FK_contacts_contacts_ParentContactId",
                table: "contacts",
                column: "ParentContactId",
                principalTable: "contacts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Data migration: Create a Household group contact for each tenant
            migrationBuilder.Sql("""
                INSERT INTO contacts ("Id", "TenantId", "CompanyName", "ContactType", "IsTenantHousehold",
                                      "Visibility", "IsActive", "CreatedByUserId", "CreatedAt", "UpdatedAt",
                                      "UsesTenantAddress", "UseGravatar", "Gender", "BirthDatePrecision",
                                      "DeathDatePrecision", "UsesGroupAddress")
                SELECT
                    gen_random_uuid(),
                    t."id",
                    t."name" || ' Household',
                    'Household',
                    true,
                    'TenantShared',
                    true,
                    (SELECT u."Id" FROM users u WHERE u."TenantId" = t."id" ORDER BY u."CreatedAt" LIMIT 1),
                    CURRENT_TIMESTAMP,
                    CURRENT_TIMESTAMP,
                    true,
                    false,
                    'Unknown',
                    'Unknown',
                    'Unknown',
                    false
                FROM tenants t
                WHERE EXISTS (SELECT 1 FROM users u WHERE u."TenantId" = t."id");
                """);

            // Data migration: Link all existing contacts as members of their tenant's household group
            migrationBuilder.Sql("""
                UPDATE contacts c
                SET "ParentContactId" = th."Id"
                FROM contacts th
                WHERE th."TenantId" = c."TenantId"
                  AND th."IsTenantHousehold" = true
                  AND c."IsTenantHousehold" = false
                  AND c."ParentContactId" IS NULL;
                """);

            // Data migration: Copy tenant address to household group contact
            migrationBuilder.Sql("""
                INSERT INTO contact_addresses ("Id", "ContactId", "AddressId", "Tag", "IsPrimary", "CreatedAt", "TenantId")
                SELECT
                    gen_random_uuid(),
                    c."Id",
                    t."address_id",
                    'Home',
                    true,
                    CURRENT_TIMESTAMP,
                    c."TenantId"
                FROM contacts c
                JOIN tenants t ON t."id" = c."TenantId"
                WHERE c."IsTenantHousehold" = true
                  AND t."address_id" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove address links for tenant household groups
            migrationBuilder.Sql("""
                DELETE FROM contact_addresses
                WHERE "ContactId" IN (SELECT "Id" FROM contacts WHERE "IsTenantHousehold" = true);
                """);

            // Remove parent links from all contacts
            migrationBuilder.Sql("""
                UPDATE contacts SET "ParentContactId" = NULL;
                """);

            // Delete tenant household group contacts
            migrationBuilder.Sql("""
                DELETE FROM contacts WHERE "IsTenantHousehold" = true;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_contacts_contacts_ParentContactId",
                table: "contacts");

            migrationBuilder.DropIndex(
                name: "IX_contacts_ParentContactId",
                table: "contacts");

            migrationBuilder.DropIndex(
                name: "IX_contacts_TenantId_ContactType",
                table: "contacts");

            migrationBuilder.DropIndex(
                name: "IX_contacts_TenantId_IsTenantHousehold",
                table: "contacts");

            migrationBuilder.DropIndex(
                name: "IX_contacts_TenantId_ParentContactId",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "BusinessCategory",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "ContactType",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "IsTenantHousehold",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "ParentContactId",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "UsesGroupAddress",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "Website",
                table: "contacts");
        }
    }
}
