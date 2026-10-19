using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMasterProductReviewStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "review_status",
                table: "master_products",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "ix_master_products_review_status",
                table: "master_products",
                column: "review_status");

            // Earlier tenant contributions were published without review: hold them for it,
            // and drop the external image URLs they carried (source 1 = TenantContributed).
            migrationBuilder.Sql("""
                UPDATE master_products SET review_status = 1 WHERE source = 1;

                DELETE FROM master_product_images
                 WHERE external_url IS NOT NULL
                   AND master_product_id IN (SELECT id FROM master_products WHERE source = 1);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_master_products_review_status",
                table: "master_products");

            migrationBuilder.DropColumn(
                name: "review_status",
                table: "master_products");
        }
    }
}
