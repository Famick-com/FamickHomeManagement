using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBatchCookItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "batch_cook_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    total_quantity = table.Column<decimal>(type: "numeric", nullable: true),
                    quantity_unit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_batch_cook_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_batch_cook_items_source_entry",
                        column: x => x.source_entry_id,
                        principalTable: "meal_plan_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_batch_cook_items_product",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_batch_cook_items_quantity_unit",
                        column: x => x.quantity_unit_id,
                        principalTable: "quantity_units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "batch_cook_item_usages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_cook_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dependent_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity_used = table.Column<decimal>(type: "numeric", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_batch_cook_item_usages", x => x.id);
                    table.ForeignKey(
                        name: "fk_batch_cook_item_usages_batch_cook_item",
                        column: x => x.batch_cook_item_id,
                        principalTable: "batch_cook_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_batch_cook_item_usages_dependent_entry",
                        column: x => x.dependent_entry_id,
                        principalTable: "meal_plan_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_batch_cook_items_source_product",
                table: "batch_cook_items",
                columns: new[] { "source_entry_id", "product_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_batch_cook_items_product_id",
                table: "batch_cook_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_batch_cook_items_quantity_unit_id",
                table: "batch_cook_items",
                column: "quantity_unit_id");

            migrationBuilder.CreateIndex(
                name: "ux_batch_cook_item_usages_item_entry",
                table: "batch_cook_item_usages",
                columns: new[] { "batch_cook_item_id", "dependent_entry_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_batch_cook_item_usages_dependent_entry",
                table: "batch_cook_item_usages",
                column: "dependent_entry_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "batch_cook_item_usages");

            migrationBuilder.DropTable(
                name: "batch_cook_items");
        }
    }
}
