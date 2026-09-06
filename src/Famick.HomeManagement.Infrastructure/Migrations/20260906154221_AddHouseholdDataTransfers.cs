using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHouseholdDataTransfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "household_data_transfers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    heartbeat_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    include_files = table.Column<bool>(type: "boolean", nullable: false),
                    archive_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    archive_bytes = table.Column<long>(type: "bigint", nullable: true),
                    archive_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    manifest_json = table.Column<string>(type: "jsonb", nullable: true),
                    upload_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    upload_bytes = table.Column<long>(type: "bigint", nullable: true),
                    changed_since_policy = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    progress_label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    progress_current = table.Column<long>(type: "bigint", nullable: false),
                    progress_total = table.Column<long>(type: "bigint", nullable: false),
                    error_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_household_data_transfers", x => x.id);
                    table.ForeignKey(
                        name: "FK_household_data_transfers_users_requested_by_user_id",
                        column: x => x.requested_by_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "household_data_transfer_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    transfer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    classification = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    source_updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    target_updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    decision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_household_data_transfer_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_household_data_transfer_items_household_data_transfers_tran~",
                        column: x => x.transfer_id,
                        principalTable: "household_data_transfers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_household_data_transfer_items_transfer_id_category_source_id",
                table: "household_data_transfer_items",
                columns: new[] { "transfer_id", "category", "source_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_household_data_transfer_items_transfer_id_classification",
                table: "household_data_transfer_items",
                columns: new[] { "transfer_id", "classification" });

            migrationBuilder.CreateIndex(
                name: "IX_household_data_transfers_requested_by_user_id",
                table: "household_data_transfers",
                column: "requested_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_household_data_transfers_tenant_id_expires_at",
                table: "household_data_transfers",
                columns: new[] { "tenant_id", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "IX_household_data_transfers_tenant_id_kind_status",
                table: "household_data_transfers",
                columns: new[] { "tenant_id", "kind", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "household_data_transfer_items");

            migrationBuilder.DropTable(
                name: "household_data_transfers");
        }
    }
}
