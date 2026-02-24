using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCalendarEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "time_zone_id",
                table: "tenants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "America/New_York");

            migrationBuilder.CreateTable(
                name: "calendar_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    location = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    start_time_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_time_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_all_day = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    recurrence_rule = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    recurrence_end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reminder_minutes_before = table.Column<int>(type: "integer", nullable: true),
                    color = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_calendar_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_calendar_events_created_by_user",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "external_calendar_subscriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ics_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    color = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    sync_interval_minutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 60),
                    last_synced_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_sync_status = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_external_calendar_subscriptions", x => x.id);
                    table.ForeignKey(
                        name: "fk_external_calendar_subscriptions_user",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_calendar_ics_tokens",
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
                    table.PrimaryKey("PK_user_calendar_ics_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_calendar_ics_tokens_user",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "calendar_event_exceptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    calendar_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_start_time_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    override_title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    override_description = table.Column<string>(type: "text", nullable: true),
                    override_location = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    override_start_time_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    override_end_time_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    override_is_all_day = table.Column<bool>(type: "boolean", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_calendar_event_exceptions", x => x.id);
                    table.ForeignKey(
                        name: "fk_calendar_event_exceptions_event",
                        column: x => x.calendar_event_id,
                        principalTable: "calendar_events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "calendar_event_members",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    calendar_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    participation_type = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_calendar_event_members", x => x.id);
                    table.ForeignKey(
                        name: "fk_calendar_event_members_event",
                        column: x => x.calendar_event_id,
                        principalTable: "calendar_events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_calendar_event_members_user",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "external_calendar_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscription_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_uid = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    start_time_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_time_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_all_day = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_external_calendar_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_external_calendar_events_subscription",
                        column: x => x.subscription_id,
                        principalTable: "external_calendar_subscriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_calendar_event_exceptions_event_id",
                table: "calendar_event_exceptions",
                column: "calendar_event_id");

            migrationBuilder.CreateIndex(
                name: "ix_calendar_event_exceptions_event_occurrence",
                table: "calendar_event_exceptions",
                columns: new[] { "calendar_event_id", "original_start_time_utc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_calendar_event_members_event_user",
                table: "calendar_event_members",
                columns: new[] { "calendar_event_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_calendar_event_members_user_id",
                table: "calendar_event_members",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_calendar_events_created_by",
                table: "calendar_events",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_calendar_events_tenant_id",
                table: "calendar_events",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_calendar_events_tenant_start",
                table: "calendar_events",
                columns: new[] { "tenant_id", "start_time_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_external_calendar_events_subscription_start",
                table: "external_calendar_events",
                columns: new[] { "subscription_id", "start_time_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_external_calendar_events_subscription_uid",
                table: "external_calendar_events",
                columns: new[] { "subscription_id", "external_uid" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_external_calendar_subscriptions_tenant_id",
                table: "external_calendar_subscriptions",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_external_calendar_subscriptions_tenant_user",
                table: "external_calendar_subscriptions",
                columns: new[] { "tenant_id", "user_id" });

            migrationBuilder.CreateIndex(
                name: "IX_external_calendar_subscriptions_user_id",
                table: "external_calendar_subscriptions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_calendar_ics_tokens_tenant_id",
                table: "user_calendar_ics_tokens",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_calendar_ics_tokens_tenant_user",
                table: "user_calendar_ics_tokens",
                columns: new[] { "tenant_id", "user_id" });

            migrationBuilder.CreateIndex(
                name: "ix_user_calendar_ics_tokens_token",
                table: "user_calendar_ics_tokens",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_calendar_ics_tokens_user_id",
                table: "user_calendar_ics_tokens",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "calendar_event_exceptions");

            migrationBuilder.DropTable(
                name: "calendar_event_members");

            migrationBuilder.DropTable(
                name: "external_calendar_events");

            migrationBuilder.DropTable(
                name: "user_calendar_ics_tokens");

            migrationBuilder.DropTable(
                name: "calendar_events");

            migrationBuilder.DropTable(
                name: "external_calendar_subscriptions");

            migrationBuilder.DropColumn(
                name: "time_zone_id",
                table: "tenants");
        }
    }
}
