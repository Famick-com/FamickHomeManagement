using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHomeFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "homes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    year_built = table.Column<int>(type: "integer", nullable: true),
                    square_footage = table.Column<int>(type: "integer", nullable: true),
                    bedrooms = table.Column<int>(type: "integer", nullable: true),
                    bathrooms = table.Column<decimal>(type: "numeric(3,1)", nullable: true),
                    hoa_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    hoa_contact_info = table.Column<string>(type: "text", nullable: true),
                    hoa_rules_link = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ac_filter_sizes = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ac_filter_replacement_interval_days = table.Column<int>(type: "integer", nullable: true),
                    fridge_water_filter_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    under_sink_filter_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    whole_house_filter_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    smoke_co_detector_battery_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    hvac_service_schedule = table.Column<string>(type: "text", nullable: true),
                    pest_control_schedule = table.Column<string>(type: "text", nullable: true),
                    insurance_type = table.Column<int>(type: "integer", nullable: true),
                    insurance_policy_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    insurance_agent_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    insurance_agent_phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    insurance_agent_email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    mortgage_info = table.Column<string>(type: "text", nullable: true),
                    property_tax_account_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    escrow_details = table.Column<string>(type: "text", nullable: true),
                    appraisal_value = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    appraisal_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_setup_complete = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_homes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "home_utilities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    home_id = table.Column<Guid>(type: "uuid", nullable: false),
                    utility_type = table.Column<int>(type: "integer", nullable: false),
                    company_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    account_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    phone_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    website = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    login_email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_home_utilities", x => x.id);
                    table.ForeignKey(
                        name: "fk_home_utilities_homes",
                        column: x => x.home_id,
                        principalTable: "homes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_home_utilities_home_id",
                table: "home_utilities",
                column: "home_id");

            migrationBuilder.CreateIndex(
                name: "ix_home_utilities_tenant_id",
                table: "home_utilities",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_home_utilities_home_type",
                table: "home_utilities",
                columns: new[] { "home_id", "utility_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_homes_tenant_id",
                table: "homes",
                column: "tenant_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "home_utilities");

            migrationBuilder.DropTable(
                name: "homes");
        }
    }
}
