using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RefreshTokenFamilyNotEmpty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Passkey, social, registration and accept-terms sessions were issued with
            // FamilyId = Guid.Empty (issue #106). gen_random_uuid() runs per row, so each
            // one becomes its own family. A rotation chain is split into one family per
            // link, which only means a later replay revokes less of that chain; the
            // replayed user's access tokens still die through the jwt_min_iat bump.
            migrationBuilder.Sql(
                "UPDATE \"RefreshTokens\" SET \"FamilyId\" = gen_random_uuid() " +
                "WHERE \"FamilyId\" = '00000000-0000-0000-0000-000000000000';");

            migrationBuilder.AddCheckConstraint(
                name: "ck_refresh_tokens_family_id_not_empty",
                table: "RefreshTokens",
                sql: "\"FamilyId\" <> '00000000-0000-0000-0000-000000000000'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_refresh_tokens_family_id_not_empty",
                table: "RefreshTokens");
        }
    }
}
