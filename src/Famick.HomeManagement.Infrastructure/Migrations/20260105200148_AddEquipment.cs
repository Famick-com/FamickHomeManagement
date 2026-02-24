using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEquipment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "equipment_id",
                table: "chores",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "equipment_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    icon_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_equipment_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "equipment_document_tags",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_equipment_document_tags", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "equipment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    location = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    model_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    serial_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    purchase_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    purchase_location = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    warranty_expiration_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    warranty_contact_info = table.Column<string>(type: "text", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    parent_equipment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_equipment", x => x.id);
                    table.ForeignKey(
                        name: "fk_equipment_category",
                        column: x => x.category_id,
                        principalTable: "equipment_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_equipment_parent",
                        column: x => x.parent_equipment_id,
                        principalTable: "equipment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "equipment_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    equipment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    original_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    file_size = table.Column<long>(type: "bigint", nullable: false),
                    display_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    tag_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_equipment_documents", x => x.id);
                    table.ForeignKey(
                        name: "fk_equipment_documents_equipment",
                        column: x => x.equipment_id,
                        principalTable: "equipment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_equipment_documents_tags",
                        column: x => x.tag_id,
                        principalTable: "equipment_document_tags",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_chores_equipment_id",
                table: "chores",
                column: "equipment_id");

            migrationBuilder.CreateIndex(
                name: "ix_equipment_category_id",
                table: "equipment",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_equipment_parent_id",
                table: "equipment",
                column: "parent_equipment_id");

            migrationBuilder.CreateIndex(
                name: "ix_equipment_tenant_id",
                table: "equipment",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_equipment_tenant_name",
                table: "equipment",
                columns: new[] { "tenant_id", "name" });

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
                name: "ix_equipment_document_tags_tenant_id",
                table: "equipment_document_tags",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_equipment_document_tags_tenant_name",
                table: "equipment_document_tags",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_equipment_documents_equipment_id",
                table: "equipment_documents",
                column: "equipment_id");

            migrationBuilder.CreateIndex(
                name: "IX_equipment_documents_tag_id",
                table: "equipment_documents",
                column: "tag_id");

            migrationBuilder.CreateIndex(
                name: "ix_equipment_documents_tenant_equipment",
                table: "equipment_documents",
                columns: new[] { "tenant_id", "equipment_id" });

            migrationBuilder.CreateIndex(
                name: "ix_equipment_documents_tenant_id",
                table: "equipment_documents",
                column: "tenant_id");

            migrationBuilder.AddForeignKey(
                name: "fk_chores_equipment",
                table: "chores",
                column: "equipment_id",
                principalTable: "equipment",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_chores_equipment",
                table: "chores");

            migrationBuilder.DropTable(
                name: "equipment_documents");

            migrationBuilder.DropTable(
                name: "equipment");

            migrationBuilder.DropTable(
                name: "equipment_document_tags");

            migrationBuilder.DropTable(
                name: "equipment_categories");

            migrationBuilder.DropIndex(
                name: "IX_chores_equipment_id",
                table: "chores");

            migrationBuilder.DropColumn(
                name: "equipment_id",
                table: "chores");
        }
    }
}
