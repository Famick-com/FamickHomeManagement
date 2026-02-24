using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantIntegrationTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TenantIntegrationTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PluginId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AccessToken = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    RefreshToken = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastRefreshedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RequiresReauth = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantIntegrationTokens", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantIntegrationTokens_PluginId",
                table: "TenantIntegrationTokens",
                column: "PluginId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantIntegrationTokens_TenantId",
                table: "TenantIntegrationTokens",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantIntegrationTokens_TenantId_PluginId",
                table: "TenantIntegrationTokens",
                columns: new[] { "TenantId", "PluginId" },
                unique: true);

            // Migrate existing tokens from shopping_locations to TenantIntegrationTokens
            // Uses DISTINCT ON to select one token per tenant/integration, preferring most recent
            // Wrapped in DO block to handle fresh databases or databases without integration columns
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.tables
                        WHERE table_schema = 'public'
                        AND table_name = 'shopping_locations'
                    ) AND EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'public'
                        AND table_name = 'shopping_locations'
                        AND column_name = 'integration_type'
                    ) THEN
                        INSERT INTO ""TenantIntegrationTokens"" (""Id"", ""TenantId"", ""PluginId"", ""AccessToken"", ""RefreshToken"", ""ExpiresAt"", ""RequiresReauth"", ""CreatedAt"", ""UpdatedAt"")
                        SELECT DISTINCT ON (sl.tenant_id, sl.integration_type)
                            gen_random_uuid(),
                            sl.tenant_id,
                            sl.integration_type,
                            sl.oauth_access_token,
                            sl.oauth_refresh_token,
                            sl.oauth_token_expires_at,
                            false,
                            CURRENT_TIMESTAMP,
                            CURRENT_TIMESTAMP
                        FROM shopping_locations sl
                        WHERE sl.integration_type IS NOT NULL
                          AND sl.oauth_access_token IS NOT NULL
                        ORDER BY sl.tenant_id, sl.integration_type, sl.updated_at DESC;
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantIntegrationTokens");
        }
    }
}
