using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountDeletionNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletionCancelledNoticeAt",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletionCancelledNoticeRequestedAt",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DeletionCancelledNoticeWasHousehold",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletionReminderSentAt",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletionReminderSentAt",
                table: "tenants",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletionCancelledNoticeAt",
                table: "users");

            migrationBuilder.DropColumn(
                name: "DeletionCancelledNoticeRequestedAt",
                table: "users");

            migrationBuilder.DropColumn(
                name: "DeletionCancelledNoticeWasHousehold",
                table: "users");

            migrationBuilder.DropColumn(
                name: "DeletionReminderSentAt",
                table: "users");

            migrationBuilder.DropColumn(
                name: "DeletionReminderSentAt",
                table: "tenants");
        }
    }
}
