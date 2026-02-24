using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddContactEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ContactId",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "contact_tags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Icon = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contact_tags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "contacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MiddleName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PreferredName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Gender = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Unknown"),
                    BirthYear = table.Column<int>(type: "integer", nullable: true),
                    BirthMonth = table.Column<int>(type: "integer", nullable: true),
                    BirthDay = table.Column<int>(type: "integer", nullable: true),
                    BirthDatePrecision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Unknown"),
                    DeathYear = table.Column<int>(type: "integer", nullable: true),
                    DeathMonth = table.Column<int>(type: "integer", nullable: true),
                    DeathDay = table.Column<int>(type: "integer", nullable: true),
                    DeathDatePrecision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Unknown"),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    HouseholdTenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    LinkedUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UsesTenantAddress = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Visibility = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "TenantShared"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contacts_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "contact_addresses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: false),
                    AddressId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tag = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Home"),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contact_addresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contact_addresses_addresses_AddressId",
                        column: x => x.AddressId,
                        principalTable: "addresses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_contact_addresses_contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "contact_audit_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OldValues = table.Column<string>(type: "jsonb", nullable: true),
                    NewValues = table.Column<string>(type: "jsonb", nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contact_audit_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contact_audit_logs_contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_contact_audit_logs_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "contact_phone_numbers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    NormalizedNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Tag = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Mobile"),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contact_phone_numbers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contact_phone_numbers_contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "contact_relationships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceContactId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetContactId = table.Column<Guid>(type: "uuid", nullable: false),
                    RelationshipType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CustomLabel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contact_relationships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contact_relationships_contacts_SourceContactId",
                        column: x => x.SourceContactId,
                        principalTable: "contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_contact_relationships_contacts_TargetContactId",
                        column: x => x.TargetContactId,
                        principalTable: "contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "contact_social_media",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: false),
                    Service = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Username = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ProfileUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contact_social_media", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contact_social_media_contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "contact_tag_links",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: false),
                    TagId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contact_tag_links", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contact_tag_links_contact_tags_TagId",
                        column: x => x.TagId,
                        principalTable: "contact_tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_contact_tag_links_contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "contact_user_shares",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: false),
                    SharedWithUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CanEdit = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contact_user_shares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contact_user_shares_contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_contact_user_shares_users_SharedWithUserId",
                        column: x => x.SharedWithUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_users_ContactId",
                table: "users",
                column: "ContactId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_contact_addresses_AddressId",
                table: "contact_addresses",
                column: "AddressId");

            migrationBuilder.CreateIndex(
                name: "IX_contact_addresses_ContactId_AddressId",
                table: "contact_addresses",
                columns: new[] { "ContactId", "AddressId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_contact_addresses_ContactId_IsPrimary",
                table: "contact_addresses",
                columns: new[] { "ContactId", "IsPrimary" });

            migrationBuilder.CreateIndex(
                name: "IX_contact_addresses_TenantId",
                table: "contact_addresses",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_contact_audit_logs_ContactId_CreatedAt",
                table: "contact_audit_logs",
                columns: new[] { "ContactId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_contact_audit_logs_TenantId",
                table: "contact_audit_logs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_contact_audit_logs_TenantId_CreatedAt",
                table: "contact_audit_logs",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_contact_audit_logs_UserId",
                table: "contact_audit_logs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_contact_phone_numbers_ContactId_IsPrimary",
                table: "contact_phone_numbers",
                columns: new[] { "ContactId", "IsPrimary" });

            migrationBuilder.CreateIndex(
                name: "IX_contact_phone_numbers_TenantId",
                table: "contact_phone_numbers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_contact_phone_numbers_TenantId_NormalizedNumber",
                table: "contact_phone_numbers",
                columns: new[] { "TenantId", "NormalizedNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_contact_relationships_SourceContactId",
                table: "contact_relationships",
                column: "SourceContactId");

            migrationBuilder.CreateIndex(
                name: "IX_contact_relationships_SourceContactId_TargetContactId_Relat~",
                table: "contact_relationships",
                columns: new[] { "SourceContactId", "TargetContactId", "RelationshipType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_contact_relationships_TargetContactId",
                table: "contact_relationships",
                column: "TargetContactId");

            migrationBuilder.CreateIndex(
                name: "IX_contact_relationships_TenantId",
                table: "contact_relationships",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_contact_social_media_ContactId_Service",
                table: "contact_social_media",
                columns: new[] { "ContactId", "Service" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_contact_social_media_TenantId",
                table: "contact_social_media",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_contact_tag_links_ContactId_TagId",
                table: "contact_tag_links",
                columns: new[] { "ContactId", "TagId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_contact_tag_links_TagId",
                table: "contact_tag_links",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_contact_tag_links_TenantId",
                table: "contact_tag_links",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_contact_tags_TenantId",
                table: "contact_tags",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_contact_tags_TenantId_Name",
                table: "contact_tags",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_contact_user_shares_ContactId_SharedWithUserId",
                table: "contact_user_shares",
                columns: new[] { "ContactId", "SharedWithUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_contact_user_shares_SharedWithUserId",
                table: "contact_user_shares",
                column: "SharedWithUserId");

            migrationBuilder.CreateIndex(
                name: "IX_contact_user_shares_TenantId",
                table: "contact_user_shares",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_contacts_CreatedByUserId",
                table: "contacts",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_contacts_TenantId",
                table: "contacts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_contacts_TenantId_Email",
                table: "contacts",
                columns: new[] { "TenantId", "Email" });

            migrationBuilder.CreateIndex(
                name: "IX_contacts_TenantId_FirstName_LastName",
                table: "contacts",
                columns: new[] { "TenantId", "FirstName", "LastName" });

            migrationBuilder.CreateIndex(
                name: "IX_contacts_TenantId_IsActive",
                table: "contacts",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_contacts_TenantId_LinkedUserId",
                table: "contacts",
                columns: new[] { "TenantId", "LinkedUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_contacts_TenantId_Visibility",
                table: "contacts",
                columns: new[] { "TenantId", "Visibility" });

            migrationBuilder.AddForeignKey(
                name: "FK_users_contacts_ContactId",
                table: "users",
                column: "ContactId",
                principalTable: "contacts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_users_contacts_ContactId",
                table: "users");

            migrationBuilder.DropTable(
                name: "contact_addresses");

            migrationBuilder.DropTable(
                name: "contact_audit_logs");

            migrationBuilder.DropTable(
                name: "contact_phone_numbers");

            migrationBuilder.DropTable(
                name: "contact_relationships");

            migrationBuilder.DropTable(
                name: "contact_social_media");

            migrationBuilder.DropTable(
                name: "contact_tag_links");

            migrationBuilder.DropTable(
                name: "contact_user_shares");

            migrationBuilder.DropTable(
                name: "contact_tags");

            migrationBuilder.DropTable(
                name: "contacts");

            migrationBuilder.DropIndex(
                name: "IX_users_ContactId",
                table: "users");

            migrationBuilder.DropColumn(
                name: "ContactId",
                table: "users");
        }
    }
}
