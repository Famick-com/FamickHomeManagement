using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FoldVehiclesIntoEquipment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ORDER MATTERS THROUGHOUT. The scaffolded version of this migration dropped the
            // vehicle tables before moving anything out of them, and it guessed that category_id
            // had been *renamed* to primary_driver_contact_id — which would have reinterpreted
            // every equipment row's category as a contact id. Both are corrected by hand here.
            //
            // Note the two column-naming conventions in play: the equipment_* tables use
            // snake_case columns, the vehicle_* tables use quoted PascalCase. Every statement
            // below has to use the right one for the table it touches.

            // ── 1. New shape on the equipment side ────────────────────────────────────────────
            migrationBuilder.AddColumn<decimal>(
                name: "cost",
                table: "equipment_maintenance_records",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "maintenance_schedule_id",
                table: "equipment_maintenance_records",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "service_provider",
                table: "equipment_maintenance_records",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "attributes",
                table: "equipment",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "equipment",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "kind",
                table: "equipment",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // A new column, NOT a rename of category_id.
            migrationBuilder.AddColumn<Guid>(
                name: "primary_driver_contact_id",
                table: "equipment",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "equipment_maintenance_schedules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    equipment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    interval_months = table.Column<int>(type: "integer", nullable: true),
                    interval_usage = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    last_completed_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_completed_usage = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    next_due_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    next_due_usage = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_equipment_maintenance_schedules", x => x.id);
                    table.ForeignKey(
                        name: "fk_equipment_maintenance_schedules_equipment",
                        column: x => x.equipment_id,
                        principalTable: "equipment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ── 2. Derive a kind for equipment that already exists ────────────────────────────
            // Runs while category_id is still here, because the category name is the fallback
            // signal. The icon is tried first: icons come from a fixed picker whose groups map 1:1
            // onto EquipmentKind, so they classify far more reliably than free-text category names.
            // Kinds: 0 Other, 1 Appliance, 2 Electronics, 3 Vehicle, 4 Outdoor, 5 Tool, 6 Furniture.
            migrationBuilder.Sql("""
                UPDATE equipment e SET kind = CASE
                    WHEN e.icon IN ('Kitchen','Microwave','Blender','CoffeeMaker','LocalLaundryService',
                                    'Iron','AcUnit','Hvac','WaterDrop','Fireplace') THEN 1
                    WHEN e.icon IN ('Tv','Computer','Laptop','Router','SpeakerGroup','Videocam',
                                    'Print','PhoneAndroid') THEN 2
                    WHEN e.icon IN ('DirectionsCar','DirectionsBike','TwoWheeler','DirectionsBoat',
                                    'Agriculture','LocalShipping') THEN 3
                    WHEN e.icon IN ('Grass','Pool','OutdoorGrill') THEN 4
                    WHEN e.icon IN ('Construction','Handyman','Power') THEN 5
                    WHEN e.icon IN ('Chair','Bed','TableBar','Weekend','Light','Garage','Door',
                                    'Window','Roofing') THEN 6
                    ELSE COALESCE((
                        SELECT CASE
                            WHEN lower(c.name) LIKE '%appliance%' THEN 1
                            WHEN lower(c.name) LIKE '%electronic%' THEN 2
                            WHEN lower(c.name) LIKE '%vehicle%'
                              OR lower(c.name) LIKE '%car%'
                              OR lower(c.name) LIKE '%auto%' THEN 3
                            WHEN lower(c.name) LIKE '%outdoor%'
                              OR lower(c.name) LIKE '%yard%'
                              OR lower(c.name) LIKE '%garden%'
                              OR lower(c.name) LIKE '%lawn%' THEN 4
                            WHEN lower(c.name) LIKE '%tool%' THEN 5
                            WHEN lower(c.name) LIKE '%furniture%' THEN 6
                            ELSE NULL
                        END
                        FROM equipment_categories c WHERE c.id = e.category_id
                    ), 0)
                END;
                """);

            // Categories are about to be dropped, and one whose name did not map to a kind carries
            // information the new model has nowhere to put. Rather than lose what the user typed,
            // append it to the notes. This is the only part of this migration whose input cannot be
            // reconstructed afterwards, so it errs toward keeping the text.
            migrationBuilder.Sql("""
                UPDATE equipment e
                SET notes = COALESCE(NULLIF(e.notes, '') || E'\n\n', '')
                            || 'Former category: ' || c.name
                FROM equipment_categories c
                WHERE c.id = e.category_id
                  AND e.kind = 0;
                """);

            // ── 3. Move the vehicles in ───────────────────────────────────────────────────────
            // The vehicle's Id becomes the equipment's Id. That is what lets every child table
            // below move with a plain INSERT…SELECT, and it keeps anything that already referenced
            // a vehicle id — deep links, notification dedupe keys — pointing at the same thing.
            migrationBuilder.Sql("""
                INSERT INTO equipment (
                    id, tenant_id, name, icon, manufacturer, model_number, usage_unit,
                    purchase_date, purchase_location, notes,
                    kind, is_active, primary_driver_contact_id, attributes,
                    created_at, updated_at)
                SELECT
                    v."Id",
                    v."TenantId",
                    NULLIF(btrim(concat_ws(' ', v."Year"::text, v."Make", v."Model", v."Trim")), ''),
                    'DirectionsCar',
                    NULLIF(v."Make", ''),
                    NULLIF(v."Model", ''),
                    'miles',
                    v."PurchaseDate",
                    v."PurchaseLocation",
                    v."Notes",
                    3,
                    v."IsActive",
                    v."PrimaryDriverContactId",
                    NULLIF(jsonb_strip_nulls(jsonb_build_object(
                        'Year',          v."Year",
                        'Trim',          NULLIF(v."Trim", ''),
                        'Vin',           NULLIF(v."Vin", ''),
                        'LicensePlate',  NULLIF(v."LicensePlate", ''),
                        'Color',         NULLIF(v."Color", ''),
                        'PurchasePrice', v."PurchasePrice"
                    )), '{}'::jsonb),
                    v."CreatedAt",
                    COALESCE(v."UpdatedAt", v."CreatedAt", CURRENT_TIMESTAMP)
                FROM vehicles v;
                """);

            // A vehicle with no year, make or model would produce an empty name, which the column
            // forbids. Give it something rather than failing the whole migration on one bad row.
            migrationBuilder.Sql("""
                UPDATE equipment SET name = 'Vehicle' WHERE kind = 3 AND name IS NULL;
                """);

            // Mileage becomes usage readings: the odometer is no longer a column on the asset, it
            // is the latest entry in its usage log.
            migrationBuilder.Sql("""
                INSERT INTO equipment_usage_logs (
                    id, tenant_id, equipment_id, date, reading, notes, created_at, updated_at)
                SELECT m."Id", m."TenantId", m."VehicleId", m."ReadingDate", m."Mileage", m."Notes",
                       m."CreatedAt", COALESCE(m."UpdatedAt", m."CreatedAt", CURRENT_TIMESTAMP)
                FROM vehicle_mileage_logs m;
                """);

            // For a vehicle that never logged a reading, the odometer existed only on the vehicle
            // row. Without this it is simply lost, and any usage-based schedule it had stops making
            // sense.
            migrationBuilder.Sql("""
                INSERT INTO equipment_usage_logs (
                    id, tenant_id, equipment_id, date, reading, notes, created_at, updated_at)
                SELECT gen_random_uuid(), v."TenantId", v."Id",
                       COALESCE(v."MileageAsOfDate", v."CreatedAt", CURRENT_TIMESTAMP),
                       v."CurrentMileage",
                       'Odometer reading carried over from the vehicle record',
                       CURRENT_TIMESTAMP, CURRENT_TIMESTAMP
                FROM vehicles v
                WHERE v."CurrentMileage" IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM vehicle_mileage_logs m WHERE m."VehicleId" = v."Id");
                """);

            migrationBuilder.Sql("""
                INSERT INTO equipment_maintenance_schedules (
                    id, tenant_id, equipment_id, name, description,
                    interval_months, interval_usage,
                    last_completed_date, last_completed_usage,
                    next_due_date, next_due_usage,
                    is_active, notes, created_at, updated_at)
                SELECT s."Id", s."TenantId", s."VehicleId", s."Name", s."Description",
                       s."IntervalMonths", s."IntervalMiles",
                       s."LastCompletedDate", s."LastCompletedMileage",
                       s."NextDueDate", s."NextDueMileage",
                       s."IsActive", s."Notes",
                       s."CreatedAt", COALESCE(s."UpdatedAt", s."CreatedAt", CURRENT_TIMESTAMP)
                FROM vehicle_maintenance_schedules s;
                """);

            migrationBuilder.Sql("""
                INSERT INTO equipment_maintenance_records (
                    id, tenant_id, equipment_id, description, completed_date,
                    usage_at_completion, cost, service_provider, notes,
                    maintenance_schedule_id, created_at, updated_at)
                SELECT r."Id", r."TenantId", r."VehicleId", r."Description", r."CompletedDate",
                       r."MileageAtCompletion", r."Cost", r."ServiceProvider", r."Notes",
                       r."MaintenanceScheduleId",
                       r."CreatedAt", COALESCE(r."UpdatedAt", r."CreatedAt", CURRENT_TIMESTAMP)
                FROM vehicle_maintenance_records r;
                """);

            // ── 4. Indexes and keys ───────────────────────────────────────────────────────────
            migrationBuilder.CreateIndex(
                name: "IX_equipment_maintenance_records_maintenance_schedule_id",
                table: "equipment_maintenance_records",
                column: "maintenance_schedule_id");

            migrationBuilder.CreateIndex(
                name: "ix_equipment_tenant_kind",
                table: "equipment",
                columns: new[] { "tenant_id", "kind" });

            migrationBuilder.CreateIndex(
                name: "ix_equipment_maintenance_schedules_equipment_id",
                table: "equipment_maintenance_schedules",
                column: "equipment_id");

            migrationBuilder.CreateIndex(
                name: "ix_equipment_maintenance_schedules_equipment_next_due_date",
                table: "equipment_maintenance_schedules",
                columns: new[] { "equipment_id", "next_due_date" },
                filter: "is_active = true");

            migrationBuilder.CreateIndex(
                name: "ix_equipment_maintenance_schedules_equipment_next_due_usage",
                table: "equipment_maintenance_schedules",
                columns: new[] { "equipment_id", "next_due_usage" },
                filter: "is_active = true");

            migrationBuilder.CreateIndex(
                name: "ix_equipment_maintenance_schedules_tenant_id",
                table: "equipment_maintenance_schedules",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_equipment_maintenance_schedules_equipment_name",
                table: "equipment_maintenance_schedules",
                columns: new[] { "equipment_id", "name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_equipment_primary_driver",
                table: "equipment",
                column: "primary_driver_contact_id",
                principalTable: "contacts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_equipment_maintenance_records_schedule",
                table: "equipment_maintenance_records",
                column: "maintenance_schedule_id",
                principalTable: "equipment_maintenance_schedules",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // Unique VIN per tenant, carried over from the vehicles table. EF cannot express an
            // index over a JSON expression, so it is created here and will never appear in the
            // model snapshot — if EquipmentAttributes.Vin ever moves or is renamed, change this too.
            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX ux_equipment_tenant_vin
                    ON equipment (tenant_id, ((attributes ->> 'Vin')))
                    WHERE (attributes ->> 'Vin') IS NOT NULL;
                """);

            // ── 5. Only now drop what has been copied out ─────────────────────────────────────
            // vehicle_documents is dropped without being moved on purpose: nothing could ever write
            // to it. The entity had no API surface and IFileStorageService had no vehicle methods,
            // so a row naming a file that does not exist was the best it could have held.
            migrationBuilder.DropTable(name: "vehicle_documents");
            migrationBuilder.DropTable(name: "vehicle_maintenance_records");
            migrationBuilder.DropTable(name: "vehicle_mileage_logs");
            migrationBuilder.DropTable(name: "vehicle_maintenance_schedules");
            migrationBuilder.DropTable(name: "vehicles");

            migrationBuilder.DropForeignKey(
                name: "fk_equipment_category",
                table: "equipment");

            migrationBuilder.DropIndex(
                name: "ix_equipment_category_id",
                table: "equipment");

            migrationBuilder.DropColumn(
                name: "category_id",
                table: "equipment");

            migrationBuilder.DropTable(
                name: "equipment_categories");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reordered from the scaffolded version: tables are recreated and the data is
            // moved back BEFORE the new columns are dropped, because the move needs them.
            // The scaffolded rename pair is gone too: category_id and
            // primary_driver_contact_id never held the same kind of value.

            // Bring category_id back as its own column. The scaffolded Down() did this with a
            // RenameColumn from primary_driver_contact_id, which is wrong in both directions: the
            // two columns never held the same kind of value, and renaming would hand every
            // equipment row a category id that is really a contact id.
            migrationBuilder.AddColumn<Guid>(
                name: "category_id",
                table: "equipment",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_equipment_category_id",
                table: "equipment",
                column: "category_id");

            migrationBuilder.CreateTable(
                name: "equipment_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    description = table.Column<string>(type: "text", nullable: true),
                    icon_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_equipment_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "vehicles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PrimaryDriverContactId = table.Column<Guid>(type: "uuid", nullable: true),
                    Color = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CurrentMileage = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    LicensePlate = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Make = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MileageAsOfDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PurchaseDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PurchaseLocation = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PurchasePrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Trim = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Vin = table.Column<string>(type: "character varying(17)", maxLength: 17, nullable: true),
                    Year = table.Column<int>(type: "integer", nullable: false)
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
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DocumentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IntervalMiles = table.Column<int>(type: "integer", nullable: true),
                    IntervalMonths = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    LastCompletedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastCompletedMileage = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NextDueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextDueMileage = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Mileage = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReadingDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
                    MaintenanceScheduleId = table.Column<Guid>(type: "uuid", nullable: true),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompletedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    MileageAtCompletion = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ServiceProvider = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
                name: "ix_equipment_categories_tenant_id",
                table: "equipment_categories",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_equipment_categories_tenant_name",
                table: "equipment_categories",
                columns: new[] { "tenant_id", "name" },
                unique: true);

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

            migrationBuilder.AddForeignKey(
                name: "fk_equipment_category",
                table: "equipment",
                column: "category_id",
                principalTable: "equipment_categories",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // ── Move the vehicles back out, while kind and attributes still exist ─────────────
            // Has to run after the tables above are recreated and before the new columns are
            // dropped below. The scaffolded order did the opposite, which would have dropped the
            // kind column while it was still the only way to tell a vehicle from a toaster.
            //
            // Best-effort by nature: anything a vehicle gained while it was equipment and a
            // vehicle cannot hold — documents, a parent, a warranty, a non-mileage usage unit —
            // does not survive the trip back.
            migrationBuilder.Sql("""
                INSERT INTO vehicles (
                    "Id", "TenantId", "Year", "Make", "Model", "Trim", "Vin", "LicensePlate",
                    "Color", "CurrentMileage", "MileageAsOfDate", "PrimaryDriverContactId",
                    "PurchaseDate", "PurchasePrice", "PurchaseLocation", "Notes", "IsActive",
                    "CreatedAt", "UpdatedAt")
                SELECT
                    e.id, e.tenant_id,
                    COALESCE((e.attributes ->> 'Year')::int, 0),
                    COALESCE(e.manufacturer, ''),
                    COALESCE(e.model_number, ''),
                    e.attributes ->> 'Trim',
                    e.attributes ->> 'Vin',
                    e.attributes ->> 'LicensePlate',
                    e.attributes ->> 'Color',
                    (SELECT max(l.reading)::int FROM equipment_usage_logs l WHERE l.equipment_id = e.id),
                    (SELECT max(l.date) FROM equipment_usage_logs l WHERE l.equipment_id = e.id),
                    e.primary_driver_contact_id,
                    e.purchase_date,
                    (e.attributes ->> 'PurchasePrice')::numeric,
                    e.purchase_location,
                    e.notes,
                    e.is_active,
                    e.created_at, e.updated_at
                FROM equipment e
                WHERE e.kind = 3;
                """);

            migrationBuilder.Sql("""
                INSERT INTO vehicle_mileage_logs (
                    "Id", "TenantId", "VehicleId", "Mileage", "ReadingDate", "Notes",
                    "CreatedAt", "UpdatedAt")
                SELECT l.id, l.tenant_id, l.equipment_id, l.reading::int, l.date, l.notes,
                       l.created_at, l.updated_at
                FROM equipment_usage_logs l
                JOIN equipment e ON e.id = l.equipment_id
                WHERE e.kind = 3;
                """);

            migrationBuilder.Sql("""
                INSERT INTO vehicle_maintenance_schedules (
                    "Id", "TenantId", "VehicleId", "Name", "Description", "IntervalMonths",
                    "IntervalMiles", "LastCompletedDate", "LastCompletedMileage", "NextDueDate",
                    "NextDueMileage", "IsActive", "Notes", "CreatedAt", "UpdatedAt")
                SELECT s.id, s.tenant_id, s.equipment_id, s.name, s.description, s.interval_months,
                       s.interval_usage::int, s.last_completed_date, s.last_completed_usage::int,
                       s.next_due_date, s.next_due_usage::int, s.is_active, s.notes,
                       s.created_at, s.updated_at
                FROM equipment_maintenance_schedules s
                JOIN equipment e ON e.id = s.equipment_id
                WHERE e.kind = 3;
                """);

            migrationBuilder.Sql("""
                INSERT INTO vehicle_maintenance_records (
                    "Id", "TenantId", "VehicleId", "Description", "CompletedDate",
                    "MileageAtCompletion", "Cost", "ServiceProvider", "Notes",
                    "MaintenanceScheduleId", "CreatedAt", "UpdatedAt")
                SELECT r.id, r.tenant_id, r.equipment_id, r.description, r.completed_date,
                       r.usage_at_completion::int, r.cost, r.service_provider, r.notes,
                       r.maintenance_schedule_id, r.created_at, r.updated_at
                FROM equipment_maintenance_records r
                JOIN equipment e ON e.id = r.equipment_id
                WHERE e.kind = 3;
                """);

            // The equipment rows that were vehicles go away; their children cascade.
            migrationBuilder.Sql("DELETE FROM equipment WHERE kind = 3;");

            migrationBuilder.DropForeignKey(
                name: "fk_equipment_primary_driver",
                table: "equipment");

            migrationBuilder.DropForeignKey(
                name: "fk_equipment_maintenance_records_schedule",
                table: "equipment_maintenance_records");

            migrationBuilder.DropTable(
                name: "equipment_maintenance_schedules");

            migrationBuilder.DropIndex(
                name: "IX_equipment_maintenance_records_maintenance_schedule_id",
                table: "equipment_maintenance_records");

            migrationBuilder.DropIndex(
                name: "ix_equipment_tenant_kind",
                table: "equipment");

            migrationBuilder.DropColumn(
                name: "cost",
                table: "equipment_maintenance_records");

            migrationBuilder.DropColumn(
                name: "maintenance_schedule_id",
                table: "equipment_maintenance_records");

            migrationBuilder.DropColumn(
                name: "service_provider",
                table: "equipment_maintenance_records");

            migrationBuilder.DropColumn(
                name: "attributes",
                table: "equipment");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "equipment");

            migrationBuilder.DropColumn(
                name: "kind",
                table: "equipment");

            migrationBuilder.DropColumn(
                name: "primary_driver_contact_id",
                table: "equipment");

            migrationBuilder.Sql("DROP INDEX IF EXISTS ux_equipment_tenant_vin;");
        }
    }
}
