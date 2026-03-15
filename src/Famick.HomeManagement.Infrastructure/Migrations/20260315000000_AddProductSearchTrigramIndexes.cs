using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductSearchTrigramIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Enable pg_trgm extension for trigram-based similarity/LIKE searches
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");

            // GIN trigram indexes for fast case-insensitive LIKE/ILIKE searches on product text fields.
            // These support the '%term%' pattern used by ProductSearchService.ApplyTextSearch.
            migrationBuilder.Sql(
                """CREATE INDEX ix_products_name_trgm ON "Products" USING gin (lower("Name") gin_trgm_ops);""");

            migrationBuilder.Sql(
                """CREATE INDEX ix_products_description_trgm ON "Products" USING gin (lower("Description") gin_trgm_ops);""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP INDEX IF EXISTS ix_products_description_trgm;""");
            migrationBuilder.Sql("""DROP INDEX IF EXISTS ix_products_name_trgm;""");
        }
    }
}
