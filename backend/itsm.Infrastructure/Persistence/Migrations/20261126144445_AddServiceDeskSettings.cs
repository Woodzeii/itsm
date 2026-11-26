using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace itsm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceDeskSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_archived",
                schema: "public",
                table: "ticket_types",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_sla_enabled",
                schema: "public",
                table: "ticket_types",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "portal_sla_reaction_visible",
                schema: "public",
                table: "tenants",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "portal_sla_resolution_visible",
                schema: "public",
                table: "tenants",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "sla_enabled",
                schema: "public",
                table: "tenants",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "tenants",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "portal_sla_reaction_visible", "portal_sla_resolution_visible", "sla_enabled" },
                values: new object[] { true, true, true });

            migrationBuilder.UpdateData(
                schema: "public",
                table: "ticket_types",
                keyColumn: "id",
                keyValue: 1,
                column: "is_sla_enabled",
                value: true);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "ticket_types",
                keyColumn: "id",
                keyValue: 2,
                column: "is_sla_enabled",
                value: true);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "ticket_types",
                keyColumn: "id",
                keyValue: 3,
                column: "is_sla_enabled",
                value: true);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "ticket_types",
                keyColumn: "id",
                keyValue: 4,
                column: "is_sla_enabled",
                value: true);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "ticket_types",
                keyColumn: "id",
                keyValue: 5,
                column: "is_sla_enabled",
                value: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_archived",
                schema: "public",
                table: "ticket_types");

            migrationBuilder.DropColumn(
                name: "is_sla_enabled",
                schema: "public",
                table: "ticket_types");

            migrationBuilder.DropColumn(
                name: "portal_sla_reaction_visible",
                schema: "public",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "portal_sla_resolution_visible",
                schema: "public",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "sla_enabled",
                schema: "public",
                table: "tenants");
        }
    }
}
