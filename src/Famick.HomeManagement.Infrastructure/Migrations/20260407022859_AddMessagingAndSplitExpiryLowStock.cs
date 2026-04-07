using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMessagingAndSplitExpiryLowStock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                table: "users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SmsEnabled",
                table: "notification_preferences",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Rename ExpiryLowStock → Expiry in notification_preferences
            migrationBuilder.Sql(
                """
                UPDATE notification_preferences
                SET "NotificationType" = 'Expiry'
                WHERE "NotificationType" = 'ExpiryLowStock';
                """);

            // Create LowStock preference rows for each user that had ExpiryLowStock,
            // copying their enabled/disabled settings
            migrationBuilder.Sql(
                """
                INSERT INTO notification_preferences ("Id", "TenantId", "UserId", "NotificationType", "EmailEnabled", "PushEnabled", "InAppEnabled", "SmsEnabled", "CreatedAt")
                SELECT gen_random_uuid(), "TenantId", "UserId", 'LowStock', "EmailEnabled", "PushEnabled", "InAppEnabled", "SmsEnabled", CURRENT_TIMESTAMP
                FROM notification_preferences
                WHERE "NotificationType" = 'Expiry'
                AND NOT EXISTS (
                    SELECT 1 FROM notification_preferences p2
                    WHERE p2."UserId" = notification_preferences."UserId"
                    AND p2."TenantId" = notification_preferences."TenantId"
                    AND p2."NotificationType" = 'LowStock'
                );
                """);

            // Rename ExpiryLowStock → Expiry in notifications table
            migrationBuilder.Sql(
                """
                UPDATE notifications
                SET "Type" = 'Expiry'
                WHERE "Type" = 'ExpiryLowStock';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                table: "users");

            migrationBuilder.DropColumn(
                name: "SmsEnabled",
                table: "notification_preferences");
        }
    }
}
