using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEquipmentUsageAndMaintenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "manufacturer",
                table: "equipment",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "manufacturer_link",
                table: "equipment",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "usage_unit",
                table: "equipment",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "equipment_maintenance_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    equipment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    completed_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    usage_at_completion = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    reminder_chore_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_equipment_maintenance_records", x => x.id);
                    table.ForeignKey(
                        name: "fk_equipment_maintenance_records_chore",
                        column: x => x.reminder_chore_id,
                        principalTable: "chores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_equipment_maintenance_records_equipment",
                        column: x => x.equipment_id,
                        principalTable: "equipment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "equipment_usage_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    equipment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reading = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_equipment_usage_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_equipment_usage_logs_equipment",
                        column: x => x.equipment_id,
                        principalTable: "equipment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_equipment_tenant_manufacturer",
                table: "equipment",
                columns: new[] { "tenant_id", "manufacturer" });

            migrationBuilder.CreateIndex(
                name: "ix_equipment_maintenance_records_equipment_date",
                table: "equipment_maintenance_records",
                columns: new[] { "equipment_id", "completed_date" });

            migrationBuilder.CreateIndex(
                name: "ix_equipment_maintenance_records_equipment_id",
                table: "equipment_maintenance_records",
                column: "equipment_id");

            migrationBuilder.CreateIndex(
                name: "IX_equipment_maintenance_records_reminder_chore_id",
                table: "equipment_maintenance_records",
                column: "reminder_chore_id");

            migrationBuilder.CreateIndex(
                name: "ix_equipment_maintenance_records_tenant_id",
                table: "equipment_maintenance_records",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_equipment_usage_logs_equipment_date",
                table: "equipment_usage_logs",
                columns: new[] { "equipment_id", "date" });

            migrationBuilder.CreateIndex(
                name: "ix_equipment_usage_logs_equipment_id",
                table: "equipment_usage_logs",
                column: "equipment_id");

            migrationBuilder.CreateIndex(
                name: "ix_equipment_usage_logs_tenant_id",
                table: "equipment_usage_logs",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "equipment_maintenance_records");

            migrationBuilder.DropTable(
                name: "equipment_usage_logs");

            migrationBuilder.DropIndex(
                name: "ix_equipment_tenant_manufacturer",
                table: "equipment");

            migrationBuilder.DropColumn(
                name: "manufacturer",
                table: "equipment");

            migrationBuilder.DropColumn(
                name: "manufacturer_link",
                table: "equipment");

            migrationBuilder.DropColumn(
                name: "usage_unit",
                table: "equipment");
        }
    }
}
