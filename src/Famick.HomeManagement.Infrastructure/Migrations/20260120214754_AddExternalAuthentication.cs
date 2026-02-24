using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalAuthentication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_external_logins",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProviderUserId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ProviderEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_external_logins", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_external_logins_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_passkey_credentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CredentialId = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    PublicKey = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    SignatureCounter = table.Column<long>(type: "bigint", nullable: false),
                    DeviceName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    AaGuid = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CredentialType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "public-key"),
                    UserVerification = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_passkey_credentials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_passkey_credentials_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_external_logins_TenantId",
                table: "user_external_logins",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_user_external_logins_TenantId_Provider_ProviderUserId",
                table: "user_external_logins",
                columns: new[] { "TenantId", "Provider", "ProviderUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_external_logins_TenantId_UserId_Provider",
                table: "user_external_logins",
                columns: new[] { "TenantId", "UserId", "Provider" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_external_logins_UserId",
                table: "user_external_logins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_passkey_credentials_TenantId",
                table: "user_passkey_credentials",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_user_passkey_credentials_TenantId_CredentialId",
                table: "user_passkey_credentials",
                columns: new[] { "TenantId", "CredentialId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_passkey_credentials_UserId",
                table: "user_passkey_credentials",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_external_logins");

            migrationBuilder.DropTable(
                name: "user_passkey_credentials");
        }
    }
}
