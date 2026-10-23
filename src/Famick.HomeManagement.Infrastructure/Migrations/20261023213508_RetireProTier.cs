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
            // Nothing to undo: which Home households were moved from tier 3 is not recorded.
        }
    }
}
