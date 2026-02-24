using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RedesignRecipeSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_recipes_nestings_includes_recipe_id",
                table: "recipes_nestings");

            migrationBuilder.DropForeignKey(
                name: "fk_recipes_nestings_recipe_id",
                table: "recipes_nestings");

            migrationBuilder.DropForeignKey(
                name: "fk_recipes_pos_products",
                table: "recipes_pos");

            migrationBuilder.DropForeignKey(
                name: "fk_recipes_pos_quantity_units",
                table: "recipes_pos");

            migrationBuilder.DropForeignKey(
                name: "fk_recipes_pos_recipes",
                table: "recipes_pos");

            migrationBuilder.DropPrimaryKey(
                name: "PK_recipes_pos",
                table: "recipes_pos");

            migrationBuilder.DropPrimaryKey(
                name: "PK_recipes_nestings",
                table: "recipes_nestings");

            migrationBuilder.RenameTable(
                name: "recipes_pos",
                newName: "recipe_positions");

            migrationBuilder.RenameTable(
                name: "recipes_nestings",
                newName: "recipe_nestings");

            migrationBuilder.RenameColumn(
                name: "name",
                table: "recipes",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "recipes",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "recipes",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "tenant_id",
                table: "recipes",
                newName: "TenantId");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "recipes",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "description",
                table: "recipes",
                newName: "Notes");

            migrationBuilder.RenameIndex(
                name: "ix_recipes_tenant_name",
                table: "recipes",
                newName: "IX_recipes_TenantId_Name");

            migrationBuilder.RenameIndex(
                name: "ix_recipes_tenant_id",
                table: "recipes",
                newName: "IX_recipes_TenantId");

            migrationBuilder.RenameColumn(
                name: "note",
                table: "recipe_positions",
                newName: "Note");

            migrationBuilder.RenameColumn(
                name: "amount",
                table: "recipe_positions",
                newName: "Amount");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "recipe_positions",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "recipe_positions",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "tenant_id",
                table: "recipe_positions",
                newName: "TenantId");

            migrationBuilder.RenameColumn(
                name: "qu_id",
                table: "recipe_positions",
                newName: "QuantityUnitId");

            migrationBuilder.RenameColumn(
                name: "product_id",
                table: "recipe_positions",
                newName: "ProductId");

            migrationBuilder.RenameColumn(
                name: "only_check_single_unit_in_stock",
                table: "recipe_positions",
                newName: "OnlyCheckSingleUnitInStock");

            migrationBuilder.RenameColumn(
                name: "not_check_stock_fulfillment",
                table: "recipe_positions",
                newName: "NotCheckStockFulfillment");

            migrationBuilder.RenameColumn(
                name: "ingredient_group",
                table: "recipe_positions",
                newName: "IngredientGroup");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "recipe_positions",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "recipe_id",
                table: "recipe_positions",
                newName: "RecipeStepId");

            migrationBuilder.RenameIndex(
                name: "ix_recipes_pos_tenant_id",
                table: "recipe_positions",
                newName: "IX_recipe_positions_TenantId");

            migrationBuilder.RenameIndex(
                name: "ix_recipes_pos_recipe_id",
                table: "recipe_positions",
                newName: "IX_recipe_positions_RecipeStepId");

            migrationBuilder.RenameIndex(
                name: "IX_recipes_pos_qu_id",
                table: "recipe_positions",
                newName: "IX_recipe_positions_QuantityUnitId");

            migrationBuilder.RenameIndex(
                name: "ix_recipes_pos_product_id",
                table: "recipe_positions",
                newName: "IX_recipe_positions_ProductId");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "recipe_nestings",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "recipe_nestings",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "tenant_id",
                table: "recipe_nestings",
                newName: "TenantId");

            migrationBuilder.RenameColumn(
                name: "recipe_id",
                table: "recipe_nestings",
                newName: "RecipeId");

            migrationBuilder.RenameColumn(
                name: "includes_recipe_id",
                table: "recipe_nestings",
                newName: "IncludesRecipeId");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "recipe_nestings",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "ux_recipes_nestings_recipe_includes",
                table: "recipe_nestings",
                newName: "IX_recipe_nestings_RecipeId_IncludesRecipeId");

            migrationBuilder.RenameIndex(
                name: "ix_recipes_nestings_tenant_id",
                table: "recipe_nestings",
                newName: "IX_recipe_nestings_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_recipes_nestings_includes_recipe_id",
                table: "recipe_nestings",
                newName: "IX_recipe_nestings_IncludesRecipeId");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "recipes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "recipes",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<string>(
                name: "Attribution",
                table: "recipes",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByContactId",
                table: "recipes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsMeal",
                table: "recipes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Servings",
                table: "recipes",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "recipes",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Note",
                table: "recipe_positions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "recipe_positions",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)",
                oldDefaultValue: 0m);

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "recipe_positions",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            // PostgreSQL cannot automatically cast smallint to boolean; use explicit USING clause
            migrationBuilder.Sql(
                """
                ALTER TABLE recipe_positions
                    ALTER COLUMN "OnlyCheckSingleUnitInStock" DROP DEFAULT,
                    ALTER COLUMN "OnlyCheckSingleUnitInStock" TYPE boolean USING "OnlyCheckSingleUnitInStock"::int::boolean,
                    ALTER COLUMN "OnlyCheckSingleUnitInStock" SET DEFAULT false;
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE recipe_positions
                    ALTER COLUMN "NotCheckStockFulfillment" DROP DEFAULT,
                    ALTER COLUMN "NotCheckStockFulfillment" TYPE boolean USING "NotCheckStockFulfillment"::int::boolean,
                    ALTER COLUMN "NotCheckStockFulfillment" SET DEFAULT false;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "IngredientGroup",
                table: "recipe_positions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AmountInGrams",
                table: "recipe_positions",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "recipe_positions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "recipe_nestings",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddPrimaryKey(
                name: "PK_recipe_positions",
                table: "recipe_positions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_recipe_nestings",
                table: "recipe_nestings",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "recipe_images",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ExternalUrl = table.Column<string>(type: "text", nullable: true),
                    ExternalThumbnailUrl = table.Column<string>(type: "text", nullable: true),
                    ExternalSource = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recipe_images", x => x.Id);
                    table.ForeignKey(
                        name: "FK_recipe_images_recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recipe_share_tokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsRevoked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recipe_share_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_recipe_share_tokens_recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recipe_steps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipeId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepOrder = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Instructions = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    ImageFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ImageOriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ImageContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ImageFileSize = table.Column<long>(type: "bigint", nullable: true),
                    ImageExternalUrl = table.Column<string>(type: "text", nullable: true),
                    VideoUrl = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recipe_steps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_recipe_steps_recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_recipes_CreatedByContactId",
                table: "recipes",
                column: "CreatedByContactId");

            migrationBuilder.CreateIndex(
                name: "IX_recipe_images_RecipeId",
                table: "recipe_images",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_recipe_images_TenantId",
                table: "recipe_images",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_recipe_images_TenantId_RecipeId",
                table: "recipe_images",
                columns: new[] { "TenantId", "RecipeId" });

            migrationBuilder.CreateIndex(
                name: "IX_recipe_share_tokens_RecipeId",
                table: "recipe_share_tokens",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_recipe_share_tokens_TenantId",
                table: "recipe_share_tokens",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_recipe_share_tokens_Token",
                table: "recipe_share_tokens",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_recipe_steps_RecipeId",
                table: "recipe_steps",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_recipe_steps_RecipeId_StepOrder",
                table: "recipe_steps",
                columns: new[] { "RecipeId", "StepOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_recipe_steps_TenantId",
                table: "recipe_steps",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_recipe_nestings_recipes_IncludesRecipeId",
                table: "recipe_nestings",
                column: "IncludesRecipeId",
                principalTable: "recipes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_recipe_nestings_recipes_RecipeId",
                table: "recipe_nestings",
                column: "RecipeId",
                principalTable: "recipes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_recipe_positions_products_ProductId",
                table: "recipe_positions",
                column: "ProductId",
                principalTable: "products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_recipe_positions_quantity_units_QuantityUnitId",
                table: "recipe_positions",
                column: "QuantityUnitId",
                principalTable: "quantity_units",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_recipe_positions_recipe_steps_RecipeStepId",
                table: "recipe_positions",
                column: "RecipeStepId",
                principalTable: "recipe_steps",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_recipes_contacts_CreatedByContactId",
                table: "recipes",
                column: "CreatedByContactId",
                principalTable: "contacts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_recipe_nestings_recipes_IncludesRecipeId",
                table: "recipe_nestings");

            migrationBuilder.DropForeignKey(
                name: "FK_recipe_nestings_recipes_RecipeId",
                table: "recipe_nestings");

            migrationBuilder.DropForeignKey(
                name: "FK_recipe_positions_products_ProductId",
                table: "recipe_positions");

            migrationBuilder.DropForeignKey(
                name: "FK_recipe_positions_quantity_units_QuantityUnitId",
                table: "recipe_positions");

            migrationBuilder.DropForeignKey(
                name: "FK_recipe_positions_recipe_steps_RecipeStepId",
                table: "recipe_positions");

            migrationBuilder.DropForeignKey(
                name: "FK_recipes_contacts_CreatedByContactId",
                table: "recipes");

            migrationBuilder.DropTable(
                name: "recipe_images");

            migrationBuilder.DropTable(
                name: "recipe_share_tokens");

            migrationBuilder.DropTable(
                name: "recipe_steps");

            migrationBuilder.DropIndex(
                name: "IX_recipes_CreatedByContactId",
                table: "recipes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_recipe_positions",
                table: "recipe_positions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_recipe_nestings",
                table: "recipe_nestings");

            migrationBuilder.DropColumn(
                name: "Attribution",
                table: "recipes");

            migrationBuilder.DropColumn(
                name: "CreatedByContactId",
                table: "recipes");

            migrationBuilder.DropColumn(
                name: "IsMeal",
                table: "recipes");

            migrationBuilder.DropColumn(
                name: "Servings",
                table: "recipes");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "recipes");

            migrationBuilder.DropColumn(
                name: "AmountInGrams",
                table: "recipe_positions");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "recipe_positions");

            migrationBuilder.RenameTable(
                name: "recipe_positions",
                newName: "recipes_pos");

            migrationBuilder.RenameTable(
                name: "recipe_nestings",
                newName: "recipes_nestings");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "recipes",
                newName: "name");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "recipes",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "recipes",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                table: "recipes",
                newName: "tenant_id");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "recipes",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "Notes",
                table: "recipes",
                newName: "description");

            migrationBuilder.RenameIndex(
                name: "IX_recipes_TenantId_Name",
                table: "recipes",
                newName: "ix_recipes_tenant_name");

            migrationBuilder.RenameIndex(
                name: "IX_recipes_TenantId",
                table: "recipes",
                newName: "ix_recipes_tenant_id");

            migrationBuilder.RenameColumn(
                name: "Note",
                table: "recipes_pos",
                newName: "note");

            migrationBuilder.RenameColumn(
                name: "Amount",
                table: "recipes_pos",
                newName: "amount");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "recipes_pos",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "recipes_pos",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                table: "recipes_pos",
                newName: "tenant_id");

            migrationBuilder.RenameColumn(
                name: "QuantityUnitId",
                table: "recipes_pos",
                newName: "qu_id");

            migrationBuilder.RenameColumn(
                name: "ProductId",
                table: "recipes_pos",
                newName: "product_id");

            migrationBuilder.RenameColumn(
                name: "OnlyCheckSingleUnitInStock",
                table: "recipes_pos",
                newName: "only_check_single_unit_in_stock");

            migrationBuilder.RenameColumn(
                name: "NotCheckStockFulfillment",
                table: "recipes_pos",
                newName: "not_check_stock_fulfillment");

            migrationBuilder.RenameColumn(
                name: "IngredientGroup",
                table: "recipes_pos",
                newName: "ingredient_group");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "recipes_pos",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "RecipeStepId",
                table: "recipes_pos",
                newName: "recipe_id");

            migrationBuilder.RenameIndex(
                name: "IX_recipe_positions_TenantId",
                table: "recipes_pos",
                newName: "ix_recipes_pos_tenant_id");

            migrationBuilder.RenameIndex(
                name: "IX_recipe_positions_RecipeStepId",
                table: "recipes_pos",
                newName: "ix_recipes_pos_recipe_id");

            migrationBuilder.RenameIndex(
                name: "IX_recipe_positions_QuantityUnitId",
                table: "recipes_pos",
                newName: "IX_recipes_pos_qu_id");

            migrationBuilder.RenameIndex(
                name: "IX_recipe_positions_ProductId",
                table: "recipes_pos",
                newName: "ix_recipes_pos_product_id");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "recipes_nestings",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "recipes_nestings",
                newName: "updated_at");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                table: "recipes_nestings",
                newName: "tenant_id");

            migrationBuilder.RenameColumn(
                name: "RecipeId",
                table: "recipes_nestings",
                newName: "recipe_id");

            migrationBuilder.RenameColumn(
                name: "IncludesRecipeId",
                table: "recipes_nestings",
                newName: "includes_recipe_id");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "recipes_nestings",
                newName: "created_at");

            migrationBuilder.RenameIndex(
                name: "IX_recipe_nestings_TenantId",
                table: "recipes_nestings",
                newName: "ix_recipes_nestings_tenant_id");

            migrationBuilder.RenameIndex(
                name: "IX_recipe_nestings_RecipeId_IncludesRecipeId",
                table: "recipes_nestings",
                newName: "ux_recipes_nestings_recipe_includes");

            migrationBuilder.RenameIndex(
                name: "IX_recipe_nestings_IncludesRecipeId",
                table: "recipes_nestings",
                newName: "IX_recipes_nestings_includes_recipe_id");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "recipes",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<DateTime>(
                name: "updated_at",
                table: "recipes",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "note",
                table: "recipes_pos",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "amount",
                table: "recipes_pos",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)",
                oldPrecision: 18,
                oldScale: 4);

            migrationBuilder.AlterColumn<DateTime>(
                name: "updated_at",
                table: "recipes_pos",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<short>(
                name: "only_check_single_unit_in_stock",
                table: "recipes_pos",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<short>(
                name: "not_check_stock_fulfillment",
                table: "recipes_pos",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "ingredient_group",
                table: "recipes_pos",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "updated_at",
                table: "recipes_nestings",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_recipes_pos",
                table: "recipes_pos",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_recipes_nestings",
                table: "recipes_nestings",
                column: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_recipes_nestings_includes_recipe_id",
                table: "recipes_nestings",
                column: "includes_recipe_id",
                principalTable: "recipes",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_recipes_nestings_recipe_id",
                table: "recipes_nestings",
                column: "recipe_id",
                principalTable: "recipes",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_recipes_pos_products",
                table: "recipes_pos",
                column: "product_id",
                principalTable: "products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_recipes_pos_quantity_units",
                table: "recipes_pos",
                column: "qu_id",
                principalTable: "quantity_units",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_recipes_pos_recipes",
                table: "recipes_pos",
                column: "recipe_id",
                principalTable: "recipes",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
