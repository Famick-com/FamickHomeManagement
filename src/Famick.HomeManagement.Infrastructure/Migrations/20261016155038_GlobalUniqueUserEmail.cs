using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GlobalUniqueUserEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A sign-in email now names one person across every household. Two rows that share
            // an address once normalised cannot be merged safely here (each has its own password,
            // passkeys and household), so stop with a message saying which, rather than let the
            // index fail on its own.
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    clashes text;
                BEGIN
                    SELECT string_agg(email, ', ') INTO clashes
                    FROM (
                        SELECT lower(btrim("Email")) AS email
                        FROM users
                        GROUP BY 1
                        HAVING count(*) > 1
                    ) d;

                    IF clashes IS NOT NULL THEN
                        RAISE EXCEPTION 'More than one user has the email address(es): %. Give each a distinct address, or remove the extra users, then restart.', clashes;
                    END IF;
                END $$;
                """);

            migrationBuilder.DropIndex(
                name: "IX_users_TenantId_Email",
                table: "users");

            migrationBuilder.Sql("""
                UPDATE users SET "Email" = lower(btrim("Email")) WHERE "Email" <> lower(btrim("Email"));
                """);

            migrationBuilder.CreateIndex(
                name: "IX_users_Email",
                table: "users",
                column: "Email",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_users_Email_normalized",
                table: "users",
                sql: "\"Email\" = lower(btrim(\"Email\"))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_users_Email",
                table: "users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_users_Email_normalized",
                table: "users");

            migrationBuilder.CreateIndex(
                name: "IX_users_TenantId_Email",
                table: "users",
                columns: new[] { "TenantId", "Email" },
                unique: true);
        }
    }
}
