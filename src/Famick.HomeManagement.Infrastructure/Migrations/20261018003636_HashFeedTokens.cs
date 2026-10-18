using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <summary>
    /// Feed tokens are stored as a SHA-256 hash rather than in the clear. Existing tokens are
    /// hashed in place, so feed URLs already in people's calendar and contacts apps keep working.
    /// The hash must match <c>FeedTokens.Hash</c>: SHA-256 over UTF-8, as lowercase hex.
    /// </summary>
    public partial class HashFeedTokens : Migration
    {
        private static readonly string[] Tables = ["user_calendar_ics_tokens", "user_contact_vcf_tokens"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in Tables)
            {
                migrationBuilder.AddColumn<string>(
                    name: "token_hash",
                    table: table,
                    type: "character varying(64)",
                    maxLength: 64,
                    nullable: true);

                migrationBuilder.Sql(
                    $"UPDATE {table} SET token_hash = encode(sha256(convert_to(token, 'UTF8')), 'hex');");

                migrationBuilder.AlterColumn<string>(
                    name: "token_hash",
                    table: table,
                    type: "character varying(64)",
                    maxLength: 64,
                    nullable: false,
                    oldClrType: typeof(string),
                    oldType: "character varying(64)",
                    oldMaxLength: 64,
                    oldNullable: true);

                migrationBuilder.DropIndex(
                    name: $"ix_{table}_token",
                    table: table);

                migrationBuilder.DropColumn(
                    name: "token",
                    table: table);

                migrationBuilder.CreateIndex(
                    name: $"ix_{table}_token_hash",
                    table: table,
                    column: "token_hash",
                    unique: true);
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// A hash can't be turned back into a token. Going down keeps the rows, but every token is
        /// revoked and carries its hash as a placeholder, so the column stays unique and nothing
        /// that worked before can be used again.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in Tables)
            {
                migrationBuilder.AddColumn<string>(
                    name: "token",
                    table: table,
                    type: "character varying(128)",
                    maxLength: 128,
                    nullable: true);

                migrationBuilder.Sql($"UPDATE {table} SET token = token_hash, is_revoked = true;");

                migrationBuilder.AlterColumn<string>(
                    name: "token",
                    table: table,
                    type: "character varying(128)",
                    maxLength: 128,
                    nullable: false,
                    oldClrType: typeof(string),
                    oldType: "character varying(128)",
                    oldMaxLength: 128,
                    oldNullable: true);

                migrationBuilder.DropIndex(
                    name: $"ix_{table}_token_hash",
                    table: table);

                migrationBuilder.DropColumn(
                    name: "token_hash",
                    table: table);

                migrationBuilder.CreateIndex(
                    name: $"ix_{table}_token",
                    table: table,
                    column: "token",
                    unique: true);
            }
        }
    }
}
