using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStorageBins : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "storage_bins",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    short_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_storage_bins", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "storage_bin_photos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    storage_bin_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    original_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    file_size = table.Column<long>(type: "bigint", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_storage_bin_photos", x => x.id);
                    table.ForeignKey(
                        name: "fk_storage_bin_photos_storage_bin",
                        column: x => x.storage_bin_id,
                        principalTable: "storage_bins",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_storage_bin_photos_storage_bin_id",
                table: "storage_bin_photos",
                column: "storage_bin_id");

            migrationBuilder.CreateIndex(
                name: "ix_storage_bin_photos_tenant_id",
                table: "storage_bin_photos",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_storage_bin_photos_tenant_storage_bin",
                table: "storage_bin_photos",
                columns: new[] { "tenant_id", "storage_bin_id" });

            migrationBuilder.CreateIndex(
                name: "ix_storage_bins_tenant_id",
                table: "storage_bins",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_storage_bins_tenant_short_code",
                table: "storage_bins",
                columns: new[] { "tenant_id", "short_code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "storage_bin_photos");

            migrationBuilder.DropTable(
                name: "storage_bins");
        }
    }
}
