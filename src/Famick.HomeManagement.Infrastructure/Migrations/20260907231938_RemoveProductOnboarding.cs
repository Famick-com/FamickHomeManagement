using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveProductOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tenant_product_onboarding_states");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenant_product_onboarding_states",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    has_completed_onboarding = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    products_created_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    questionnaire_answers_json = table.Column<string>(type: "jsonb", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_product_onboarding_states", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_tenant_product_onboarding_states_tenant_id",
                table: "tenant_product_onboarding_states",
                column: "tenant_id",
                unique: true);
        }
    }
}
