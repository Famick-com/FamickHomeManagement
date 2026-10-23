using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RetireProTier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Tier 3 is no longer sold. Households that held it move to Home, the highest tier
            // that remains; their seat and storage limits are left as they were.
            migrationBuilder.Sql("UPDATE tenants SET subscription_tier = 2 WHERE subscription_tier = 3;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // One-way. Which Home households came from tier 3 is not recorded, so this cannot put
            // them back; only a database restore can. An older build still runs against the result,
            // because it reads those households as Home, a tier it also knows.
        }
    }
}
