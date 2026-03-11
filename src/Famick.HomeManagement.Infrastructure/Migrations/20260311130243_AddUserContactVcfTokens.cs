using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserContactVcfTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_contact_vcf_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    is_revoked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    label = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_contact_vcf_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_contact_vcf_tokens_user",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_user_contact_vcf_tokens_tenant_id",
                table: "user_contact_vcf_tokens",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_contact_vcf_tokens_tenant_user",
                table: "user_contact_vcf_tokens",
                columns: new[] { "tenant_id", "user_id" });

            migrationBuilder.CreateIndex(
                name: "ix_user_contact_vcf_tokens_token",
                table: "user_contact_vcf_tokens",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_contact_vcf_tokens_user_id",
                table: "user_contact_vcf_tokens",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_contact_vcf_tokens");
        }
    }
}
