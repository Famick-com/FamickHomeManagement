using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddContactEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_contacts_TenantId_Email",
                table: "contacts");

            migrationBuilder.RenameColumn(
                name: "Email",
                table: "contacts",
                newName: "ProfileImageFileName");

            migrationBuilder.AlterColumn<string>(
                name: "LastName",
                table: "contacts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "FirstName",
                table: "contacts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<string>(
                name: "CompanyName",
                table: "contacts",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "contacts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "contact_email_addresses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Tag = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Personal"),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contact_email_addresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contact_email_addresses_contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_contacts_TenantId_CompanyName",
                table: "contacts",
                columns: new[] { "TenantId", "CompanyName" });

            migrationBuilder.CreateIndex(
                name: "IX_contact_email_addresses_ContactId_IsPrimary",
                table: "contact_email_addresses",
                columns: new[] { "ContactId", "IsPrimary" });

            migrationBuilder.CreateIndex(
                name: "IX_contact_email_addresses_TenantId",
                table: "contact_email_addresses",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_contact_email_addresses_TenantId_NormalizedEmail",
                table: "contact_email_addresses",
                columns: new[] { "TenantId", "NormalizedEmail" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "contact_email_addresses");

            migrationBuilder.DropIndex(
                name: "IX_contacts_TenantId_CompanyName",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "CompanyName",
                table: "contacts");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "contacts");

            migrationBuilder.RenameColumn(
                name: "ProfileImageFileName",
                table: "contacts",
                newName: "Email");

            migrationBuilder.AlterColumn<string>(
                name: "LastName",
                table: "contacts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FirstName",
                table: "contacts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_contacts_TenantId_Email",
                table: "contacts",
                columns: new[] { "TenantId", "Email" });
        }
    }
}
