using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace itsm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAssetClassesMovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "license_expiration_date",
                schema: "public",
                table: "assets",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "asset_class_id",
                schema: "public",
                table: "assets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "department_id",
                schema: "public",
                table: "assets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "lifecycle_stage",
                schema: "public",
                table: "assets",
                type: "text",
                nullable: false,
                defaultValue: "Purchased");

            migrationBuilder.AddColumn<int>(
                name: "location_id",
                schema: "public",
                table: "assets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "warehouse_id",
                schema: "public",
                table: "assets",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "asset_classes",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asset_classes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "asset_movements",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    asset_id = table.Column<int>(type: "integer", nullable: false),
                    movement_type = table.Column<string>(type: "text", nullable: false),
                    from_user_id = table.Column<int>(type: "integer", nullable: true),
                    to_user_id = table.Column<int>(type: "integer", nullable: true),
                    from_warehouse_id = table.Column<int>(type: "integer", nullable: true),
                    to_warehouse_id = table.Column<int>(type: "integer", nullable: true),
                    from_location_id = table.Column<int>(type: "integer", nullable: true),
                    to_location_id = table.Column<int>(type: "integer", nullable: true),
                    from_department_id = table.Column<int>(type: "integer", nullable: true),
                    to_department_id = table.Column<int>(type: "integer", nullable: true),
                    performed_by_id = table.Column<int>(type: "integer", nullable: false),
                    performed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    comment = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asset_movements", x => x.id);
                    table.ForeignKey(
                        name: "fk_asset_movements_assets_asset_id",
                        column: x => x.asset_id,
                        principalSchema: "public",
                        principalTable: "assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_asset_movements_users_from_user_id",
                        column: x => x.from_user_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_asset_movements_users_performed_by_id",
                        column: x => x.performed_by_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_asset_movements_users_to_user_id",
                        column: x => x.to_user_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "asset_class_attributes",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    asset_class_id = table.Column<int>(type: "integer", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    data_type = table.Column<string>(type: "text", nullable: false, defaultValue: "string"),
                    is_required = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    default_value = table.Column<string>(type: "text", nullable: true),
                    options = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asset_class_attributes", x => x.id);
                    table.ForeignKey(
                        name: "fk_asset_class_attributes_asset_classes_asset_class_id",
                        column: x => x.asset_class_id,
                        principalSchema: "public",
                        principalTable: "asset_classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_assets_asset_class_id",
                schema: "public",
                table: "assets",
                column: "asset_class_id");

            migrationBuilder.CreateIndex(
                name: "ix_assets_lifecycle_stage",
                schema: "public",
                table: "assets",
                column: "lifecycle_stage");

            migrationBuilder.CreateIndex(
                name: "ix_asset_class_attributes_asset_class_id_code",
                schema: "public",
                table: "asset_class_attributes",
                columns: new[] { "asset_class_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_asset_classes_code",
                schema: "public",
                table: "asset_classes",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_asset_movements_asset_id",
                schema: "public",
                table: "asset_movements",
                column: "asset_id");

            migrationBuilder.CreateIndex(
                name: "ix_asset_movements_from_user_id",
                schema: "public",
                table: "asset_movements",
                column: "from_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_asset_movements_performed_at",
                schema: "public",
                table: "asset_movements",
                column: "performed_at");

            migrationBuilder.CreateIndex(
                name: "ix_asset_movements_performed_by_id",
                schema: "public",
                table: "asset_movements",
                column: "performed_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_asset_movements_to_user_id",
                schema: "public",
                table: "asset_movements",
                column: "to_user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_assets_asset_classes_asset_class_id",
                schema: "public",
                table: "assets",
                column: "asset_class_id",
                principalSchema: "public",
                principalTable: "asset_classes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_assets_asset_classes_asset_class_id",
                schema: "public",
                table: "assets");

            migrationBuilder.DropTable(
                name: "asset_class_attributes",
                schema: "public");

            migrationBuilder.DropTable(
                name: "asset_movements",
                schema: "public");

            migrationBuilder.DropTable(
                name: "asset_classes",
                schema: "public");

            migrationBuilder.DropIndex(
                name: "ix_assets_asset_class_id",
                schema: "public",
                table: "assets");

            migrationBuilder.DropIndex(
                name: "ix_assets_lifecycle_stage",
                schema: "public",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "asset_class_id",
                schema: "public",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "department_id",
                schema: "public",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "lifecycle_stage",
                schema: "public",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "location_id",
                schema: "public",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "warehouse_id",
                schema: "public",
                table: "assets");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "license_expiration_date",
                schema: "public",
                table: "assets",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);
        }
    }
}
