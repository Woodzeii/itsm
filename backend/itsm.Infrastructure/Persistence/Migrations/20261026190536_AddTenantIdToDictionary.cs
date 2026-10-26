using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace itsm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantIdToDictionary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_dictionaries_code",
                schema: "public",
                table: "dictionaries");

            migrationBuilder.AddColumn<int>(
                name: "tenant_id",
                schema: "public",
                table: "dictionaries",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "dictionaries",
                keyColumn: "id",
                keyValue: 1,
                column: "tenant_id",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "dictionaries",
                keyColumn: "id",
                keyValue: 2,
                column: "tenant_id",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "dictionaries",
                keyColumn: "id",
                keyValue: 3,
                column: "tenant_id",
                value: 1);

            migrationBuilder.CreateIndex(
                name: "ix_dictionaries_tenant_id_code",
                schema: "public",
                table: "dictionaries",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_dictionaries_tenants_tenant_id",
                schema: "public",
                table: "dictionaries",
                column: "tenant_id",
                principalSchema: "public",
                principalTable: "tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_dictionaries_tenants_tenant_id",
                schema: "public",
                table: "dictionaries");

            migrationBuilder.DropIndex(
                name: "ix_dictionaries_tenant_id_code",
                schema: "public",
                table: "dictionaries");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "public",
                table: "dictionaries");

            migrationBuilder.CreateIndex(
                name: "ix_dictionaries_code",
                schema: "public",
                table: "dictionaries",
                column: "code",
                unique: true);
        }
    }
}
