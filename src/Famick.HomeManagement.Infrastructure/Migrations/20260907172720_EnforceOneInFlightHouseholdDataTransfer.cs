using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnforceOneInFlightHouseholdDataTransfer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_household_data_transfers_tenant_id_kind_status",
                table: "household_data_transfers");

            migrationBuilder.CreateIndex(
                name: "ix_household_data_transfers_one_in_flight",
                table: "household_data_transfers",
                columns: new[] { "tenant_id", "kind" },
                unique: true,
                filter: "\"status\" IN ('Queued', 'Running', 'Applying')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_household_data_transfers_one_in_flight",
                table: "household_data_transfers");

            migrationBuilder.CreateIndex(
                name: "IX_household_data_transfers_tenant_id_kind_status",
                table: "household_data_transfers",
                columns: new[] { "tenant_id", "kind", "status" });
        }
    }
}
