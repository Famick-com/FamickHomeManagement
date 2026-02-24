using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVehiclesAndPropertyLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "property_links",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HomeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_property_links", x => x.Id);
                    table.ForeignKey(
                        name: "FK_property_links_homes_HomeId",
                        column: x => x.HomeId,
                        principalTable: "homes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vehicles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Make = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Trim = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Vin = table.Column<string>(type: "character varying(17)", maxLength: 17, nullable: true),
                    LicensePlate = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Color = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CurrentMileage = table.Column<int>(type: "integer", nullable: true),
                    MileageAsOfDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PrimaryDriverContactId = table.Column<Guid>(type: "uuid", nullable: true),
                    PurchaseDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PurchasePrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    PurchaseLocation = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_vehicles_contacts_PrimaryDriverContactId",
                        column: x => x.PrimaryDriverContactId,
                        principalTable: "contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "vehicle_documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DocumentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_vehicle_documents_vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vehicle_maintenance_schedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IntervalMonths = table.Column<int>(type: "integer", nullable: true),
                    IntervalMiles = table.Column<int>(type: "integer", nullable: true),
                    LastCompletedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastCompletedMileage = table.Column<int>(type: "integer", nullable: true),
                    NextDueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextDueMileage = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_maintenance_schedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_vehicle_maintenance_schedules_vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vehicle_mileage_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Mileage = table.Column<int>(type: "integer", nullable: false),
                    ReadingDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_mileage_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_vehicle_mileage_logs_vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vehicle_maintenance_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CompletedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MileageAtCompletion = table.Column<int>(type: "integer", nullable: true),
                    Cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ServiceProvider = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    MaintenanceScheduleId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_maintenance_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_vehicle_maintenance_records_vehicle_maintenance_schedules_M~",
                        column: x => x.MaintenanceScheduleId,
                        principalTable: "vehicle_maintenance_schedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_vehicle_maintenance_records_vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_property_links_HomeId",
                table: "property_links",
                column: "HomeId");

            migrationBuilder.CreateIndex(
                name: "IX_property_links_TenantId",
                table: "property_links",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_documents_TenantId",
                table: "vehicle_documents",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_documents_VehicleId",
                table: "vehicle_documents",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_maintenance_records_MaintenanceScheduleId",
                table: "vehicle_maintenance_records",
                column: "MaintenanceScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_maintenance_records_TenantId",
                table: "vehicle_maintenance_records",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_maintenance_records_VehicleId",
                table: "vehicle_maintenance_records",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_maintenance_records_VehicleId_CompletedDate",
                table: "vehicle_maintenance_records",
                columns: new[] { "VehicleId", "CompletedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_maintenance_schedules_TenantId",
                table: "vehicle_maintenance_schedules",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_maintenance_schedules_VehicleId",
                table: "vehicle_maintenance_schedules",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_maintenance_schedules_VehicleId_Name",
                table: "vehicle_maintenance_schedules",
                columns: new[] { "VehicleId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_maintenance_schedules_VehicleId_NextDueDate",
                table: "vehicle_maintenance_schedules",
                columns: new[] { "VehicleId", "NextDueDate" },
                filter: "\"IsActive\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_maintenance_schedules_VehicleId_NextDueMileage",
                table: "vehicle_maintenance_schedules",
                columns: new[] { "VehicleId", "NextDueMileage" },
                filter: "\"IsActive\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_mileage_logs_TenantId",
                table: "vehicle_mileage_logs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_mileage_logs_VehicleId",
                table: "vehicle_mileage_logs",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_mileage_logs_VehicleId_ReadingDate",
                table: "vehicle_mileage_logs",
                columns: new[] { "VehicleId", "ReadingDate" });

            migrationBuilder.CreateIndex(
                name: "IX_vehicles_PrimaryDriverContactId",
                table: "vehicles",
                column: "PrimaryDriverContactId");

            migrationBuilder.CreateIndex(
                name: "IX_vehicles_TenantId",
                table: "vehicles",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_vehicles_TenantId_Vin",
                table: "vehicles",
                columns: new[] { "TenantId", "Vin" },
                unique: true,
                filter: "\"Vin\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "property_links");

            migrationBuilder.DropTable(
                name: "vehicle_documents");

            migrationBuilder.DropTable(
                name: "vehicle_maintenance_records");

            migrationBuilder.DropTable(
                name: "vehicle_mileage_logs");

            migrationBuilder.DropTable(
                name: "vehicle_maintenance_schedules");

            migrationBuilder.DropTable(
                name: "vehicles");
        }
    }
}
