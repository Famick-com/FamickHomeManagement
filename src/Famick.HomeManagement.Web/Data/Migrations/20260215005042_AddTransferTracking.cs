using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTransferTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "transfer_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CloudUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CloudEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EncryptedRefreshToken = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IncludeHistory = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CurrentCategory = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transfer_sessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "transfer_item_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TransferSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CloudId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TransferredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transfer_item_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_transfer_item_logs_transfer_sessions_TransferSessionId",
                        column: x => x.TransferSessionId,
                        principalTable: "transfer_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_transfer_item_logs_TransferSessionId_Category_SourceId",
                table: "transfer_item_logs",
                columns: new[] { "TransferSessionId", "Category", "SourceId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "transfer_item_logs");

            migrationBuilder.DropTable(
                name: "transfer_sessions");
        }
    }
}
