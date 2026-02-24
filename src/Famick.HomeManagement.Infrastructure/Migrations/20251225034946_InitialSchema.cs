using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Famick.HomeManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RefreshTokens_tenants_TenantId",
                table: "RefreshTokens");

            migrationBuilder.DropForeignKey(
                name: "FK_users_tenants_TenantId",
                table: "users");

            migrationBuilder.DropTable(
                name: "tenants");

            migrationBuilder.AddColumn<Guid>(
                name: "ProductGroupId",
                table: "products",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ShoppingLocationId",
                table: "products",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "chores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    period_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    period_days = table.Column<int>(type: "integer", nullable: true),
                    track_date_only = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    rollover = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    assignment_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    assignment_config = table.Column<string>(type: "text", nullable: true),
                    next_execution_assigned_to_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    consume_product_on_execution = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    product_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chores", x => x.id);
                    table.ForeignKey(
                        name: "fk_chores_products",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_chores_users",
                        column: x => x.next_execution_assigned_to_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "product_groups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_groups", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "recipes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recipes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "shopping_lists",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shopping_lists", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "shopping_locations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shopping_locations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "stock",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    best_before_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    purchased_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    stock_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    price = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    open = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    opened_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    shopping_location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock", x => x.id);
                    table.ForeignKey(
                        name: "fk_stock_locations",
                        column: x => x.location_id,
                        principalTable: "locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_stock_products",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "chores_log",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chore_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tracked_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    done_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    undone = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    undone_timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    skipped = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    scheduled_execution_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chores_log", x => x.id);
                    table.ForeignKey(
                        name: "fk_chores_log_chores",
                        column: x => x.chore_id,
                        principalTable: "chores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_chores_log_users",
                        column: x => x.done_by_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "recipes_nestings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    includes_recipe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recipes_nestings", x => x.id);
                    table.ForeignKey(
                        name: "fk_recipes_nestings_includes_recipe_id",
                        column: x => x.includes_recipe_id,
                        principalTable: "recipes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_recipes_nestings_recipe_id",
                        column: x => x.recipe_id,
                        principalTable: "recipes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recipes_pos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false, defaultValue: 0m),
                    note = table.Column<string>(type: "text", nullable: true),
                    qu_id = table.Column<Guid>(type: "uuid", nullable: true),
                    only_check_single_unit_in_stock = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    ingredient_group = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    not_check_stock_fulfillment = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recipes_pos", x => x.id);
                    table.ForeignKey(
                        name: "fk_recipes_pos_products",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_recipes_pos_quantity_units",
                        column: x => x.qu_id,
                        principalTable: "quantity_units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_recipes_pos_recipes",
                        column: x => x.recipe_id,
                        principalTable: "recipes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shopping_list",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shopping_list_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false, defaultValue: 0m),
                    note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shopping_list", x => x.id);
                    table.ForeignKey(
                        name: "fk_shopping_list_products",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_shopping_list_shopping_lists",
                        column: x => x.shopping_list_id,
                        principalTable: "shopping_lists",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stock_log",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    best_before_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    purchased_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    used_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    spoiled = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    stock_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    transaction_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    price = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    undone = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    undone_timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    opened_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    recipe_id = table.Column<Guid>(type: "uuid", nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    transaction_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    stock_row_id = table.Column<Guid>(type: "uuid", nullable: true),
                    shopping_location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_log", x => x.id);
                    table.ForeignKey(
                        name: "fk_stock_log_locations",
                        column: x => x.location_id,
                        principalTable: "locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_stock_log_products",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_log_stock",
                        column: x => x.stock_row_id,
                        principalTable: "stock",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_stock_log_users",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_products_ProductGroupId",
                table: "products",
                column: "ProductGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_products_ShoppingLocationId",
                table: "products",
                column: "ShoppingLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_chores_next_execution_assigned_to_user_id",
                table: "chores",
                column: "next_execution_assigned_to_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_chores_product_id",
                table: "chores",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_chores_tenant_id",
                table: "chores",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_chores_tenant_name",
                table: "chores",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_chores_log_chore_id",
                table: "chores_log",
                column: "chore_id");

            migrationBuilder.CreateIndex(
                name: "ix_chores_log_done_by_user_id",
                table: "chores_log",
                column: "done_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_chores_log_tenant_id",
                table: "chores_log",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_groups_tenant_id",
                table: "product_groups",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_product_groups_tenant_name",
                table: "product_groups",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_recipes_tenant_id",
                table: "recipes",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_recipes_tenant_name",
                table: "recipes",
                columns: new[] { "tenant_id", "name" });

            migrationBuilder.CreateIndex(
                name: "IX_recipes_nestings_includes_recipe_id",
                table: "recipes_nestings",
                column: "includes_recipe_id");

            migrationBuilder.CreateIndex(
                name: "ix_recipes_nestings_tenant_id",
                table: "recipes_nestings",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_recipes_nestings_recipe_includes",
                table: "recipes_nestings",
                columns: new[] { "recipe_id", "includes_recipe_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_recipes_pos_product_id",
                table: "recipes_pos",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_recipes_pos_qu_id",
                table: "recipes_pos",
                column: "qu_id");

            migrationBuilder.CreateIndex(
                name: "ix_recipes_pos_recipe_id",
                table: "recipes_pos",
                column: "recipe_id");

            migrationBuilder.CreateIndex(
                name: "ix_recipes_pos_tenant_id",
                table: "recipes_pos",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_shopping_list_product_id",
                table: "shopping_list",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_shopping_list_shopping_list_id",
                table: "shopping_list",
                column: "shopping_list_id");

            migrationBuilder.CreateIndex(
                name: "ix_shopping_list_tenant_id",
                table: "shopping_list",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_shopping_lists_tenant_id",
                table: "shopping_lists",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_shopping_lists_tenant_name",
                table: "shopping_lists",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_shopping_locations_tenant_id",
                table: "shopping_locations",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_shopping_locations_tenant_name",
                table: "shopping_locations",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stock_location_id",
                table: "stock",
                column: "location_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_performance1",
                table: "stock",
                columns: new[] { "product_id", "open", "best_before_date", "amount" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_stock_id",
                table: "stock",
                column: "stock_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_tenant_id",
                table: "stock",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ux_stock_tenant_stock_id",
                table: "stock",
                columns: new[] { "tenant_id", "stock_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stock_log_correlation_id",
                table: "stock_log",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "IX_stock_log_location_id",
                table: "stock_log",
                column: "location_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_log_performance1",
                table: "stock_log",
                columns: new[] { "stock_id", "transaction_type", "amount" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_log_performance2",
                table: "stock_log",
                columns: new[] { "product_id", "best_before_date", "purchased_date", "transaction_type", "stock_id", "undone" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_log_stock_row_id",
                table: "stock_log",
                column: "stock_row_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_log_tenant_id",
                table: "stock_log",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_log_transaction_id",
                table: "stock_log",
                column: "transaction_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_log_user_id",
                table: "stock_log",
                column: "user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_products_product_groups_ProductGroupId",
                table: "products",
                column: "ProductGroupId",
                principalTable: "product_groups",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_products_shopping_locations_ShoppingLocationId",
                table: "products",
                column: "ShoppingLocationId",
                principalTable: "shopping_locations",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_products_product_groups_ProductGroupId",
                table: "products");

            migrationBuilder.DropForeignKey(
                name: "FK_products_shopping_locations_ShoppingLocationId",
                table: "products");

            migrationBuilder.DropTable(
                name: "chores_log");

            migrationBuilder.DropTable(
                name: "product_groups");

            migrationBuilder.DropTable(
                name: "recipes_nestings");

            migrationBuilder.DropTable(
                name: "recipes_pos");

            migrationBuilder.DropTable(
                name: "shopping_list");

            migrationBuilder.DropTable(
                name: "shopping_locations");

            migrationBuilder.DropTable(
                name: "stock_log");

            migrationBuilder.DropTable(
                name: "chores");

            migrationBuilder.DropTable(
                name: "recipes");

            migrationBuilder.DropTable(
                name: "shopping_lists");

            migrationBuilder.DropTable(
                name: "stock");

            migrationBuilder.DropIndex(
                name: "IX_products_ProductGroupId",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_ShoppingLocationId",
                table: "products");

            migrationBuilder.DropColumn(
                name: "ProductGroupId",
                table: "products");

            migrationBuilder.DropColumn(
                name: "ShoppingLocationId",
                table: "products");

            migrationBuilder.CreateTable(
                name: "tenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    MaxUsers = table.Column<int>(type: "integer", nullable: false, defaultValue: 5),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StorageQuotaMb = table.Column<int>(type: "integer", nullable: false, defaultValue: 1000),
                    Subdomain = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SubscriptionExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SubscriptionTier = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenants", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tenants_Subdomain",
                table: "tenants",
                column: "Subdomain",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_RefreshTokens_tenants_TenantId",
                table: "RefreshTokens",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_users_tenants_TenantId",
                table: "users",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
