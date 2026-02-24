using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantBillingColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "kms_key_id",
                table: "tenants",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "max_users",
                table: "tenants",
                type: "integer",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<string>(
                name: "revenuecat_user_id",
                table: "tenants",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "storage_blocks_purchased",
                table: "tenants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "storage_quota_mb",
                table: "tenants",
                type: "integer",
                nullable: false,
                defaultValue: 1000);

            migrationBuilder.AddColumn<long>(
                name: "storage_used_bytes",
                table: "tenants",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "stripe_customer_id",
                table: "tenants",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "stripe_subscription_id",
                table: "tenants",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "subscription_expires_at",
                table: "tenants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "subscription_tier",
                table: "tenants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "trial_ends_at",
                table: "tenants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "trial_started_at",
                table: "tenants",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "kms_key_id",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "max_users",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "revenuecat_user_id",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "storage_blocks_purchased",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "storage_quota_mb",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "storage_used_bytes",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "stripe_customer_id",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "stripe_subscription_id",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "subscription_expires_at",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "subscription_tier",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "trial_ends_at",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "trial_started_at",
                table: "tenants");
        }
    }
}
