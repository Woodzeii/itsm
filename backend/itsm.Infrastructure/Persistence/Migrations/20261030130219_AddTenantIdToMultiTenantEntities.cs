using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace itsm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantIdToMultiTenantEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_users_email",
                schema: "public",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_users_object_sid",
                schema: "public",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_users_username",
                schema: "public",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_ticket_types_code",
                schema: "public",
                table: "ticket_types");

            migrationBuilder.DropIndex(
                name: "ix_ticket_statuses_code",
                schema: "public",
                table: "ticket_statuses");

            migrationBuilder.DropIndex(
                name: "ix_criticality_levels_code",
                schema: "public",
                table: "criticality_levels");

            migrationBuilder.DropIndex(
                name: "ix_assets_inventory_number",
                schema: "public",
                table: "assets");

            migrationBuilder.DropIndex(
                name: "ix_agent_groups_code",
                schema: "public",
                table: "agent_groups");

            migrationBuilder.AlterColumn<int>(
                name: "tenant_id",
                schema: "public",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "tenant_id",
                schema: "public",
                table: "tickets",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "tenant_id",
                schema: "public",
                table: "ticket_types",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "tenant_id",
                schema: "public",
                table: "ticket_statuses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "tenant_id",
                schema: "public",
                table: "service_catalog",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "tenant_id",
                schema: "public",
                table: "form_templates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "tenant_id",
                schema: "public",
                table: "criticality_levels",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "tenant_id",
                schema: "public",
                table: "assets",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "tenant_id",
                schema: "public",
                table: "agent_groups",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "criticality_levels",
                keyColumn: "id",
                keyValue: 1,
                column: "tenant_id",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "criticality_levels",
                keyColumn: "id",
                keyValue: 2,
                column: "tenant_id",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "criticality_levels",
                keyColumn: "id",
                keyValue: 3,
                column: "tenant_id",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "ticket_statuses",
                keyColumn: "id",
                keyValue: 1,
                column: "tenant_id",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "ticket_statuses",
                keyColumn: "id",
                keyValue: 2,
                column: "tenant_id",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "ticket_statuses",
                keyColumn: "id",
                keyValue: 3,
                column: "tenant_id",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "ticket_statuses",
                keyColumn: "id",
                keyValue: 4,
                column: "tenant_id",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "ticket_statuses",
                keyColumn: "id",
                keyValue: 5,
                column: "tenant_id",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "ticket_types",
                keyColumn: "id",
                keyValue: 1,
                column: "tenant_id",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "ticket_types",
                keyColumn: "id",
                keyValue: 2,
                column: "tenant_id",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "ticket_types",
                keyColumn: "id",
                keyValue: 3,
                column: "tenant_id",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "ticket_types",
                keyColumn: "id",
                keyValue: 4,
                column: "tenant_id",
                value: 1);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "ticket_types",
                keyColumn: "id",
                keyValue: 5,
                column: "tenant_id",
                value: 1);

            migrationBuilder.CreateIndex(
                name: "ix_users_tenant_id_email",
                schema: "public",
                table: "users",
                columns: new[] { "tenant_id", "email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_tenant_id_object_sid",
                schema: "public",
                table: "users",
                columns: new[] { "tenant_id", "object_sid" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_tenant_id_username",
                schema: "public",
                table: "users",
                columns: new[] { "tenant_id", "username" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tickets_tenant_id",
                schema: "public",
                table: "tickets",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_ticket_types_tenant_id",
                schema: "public",
                table: "ticket_types",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_ticket_types_tenant_id_code",
                schema: "public",
                table: "ticket_types",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ticket_statuses_tenant_id",
                schema: "public",
                table: "ticket_statuses",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_ticket_statuses_tenant_id_code",
                schema: "public",
                table: "ticket_statuses",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_service_catalog_tenant_id",
                schema: "public",
                table: "service_catalog",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_form_templates_tenant_id",
                schema: "public",
                table: "form_templates",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_criticality_levels_tenant_id",
                schema: "public",
                table: "criticality_levels",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_criticality_levels_tenant_id_code",
                schema: "public",
                table: "criticality_levels",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_assets_tenant_id",
                schema: "public",
                table: "assets",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_assets_tenant_id_inventory_number",
                schema: "public",
                table: "assets",
                columns: new[] { "tenant_id", "inventory_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_agent_groups_tenant_id",
                schema: "public",
                table: "agent_groups",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_agent_groups_tenant_id_code",
                schema: "public",
                table: "agent_groups",
                columns: new[] { "tenant_id", "code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_users_tenant_id_email",
                schema: "public",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_users_tenant_id_object_sid",
                schema: "public",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_users_tenant_id_username",
                schema: "public",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_tickets_tenant_id",
                schema: "public",
                table: "tickets");

            migrationBuilder.DropIndex(
                name: "ix_ticket_types_tenant_id",
                schema: "public",
                table: "ticket_types");

            migrationBuilder.DropIndex(
                name: "ix_ticket_types_tenant_id_code",
                schema: "public",
                table: "ticket_types");

            migrationBuilder.DropIndex(
                name: "ix_ticket_statuses_tenant_id",
                schema: "public",
                table: "ticket_statuses");

            migrationBuilder.DropIndex(
                name: "ix_ticket_statuses_tenant_id_code",
                schema: "public",
                table: "ticket_statuses");

            migrationBuilder.DropIndex(
                name: "ix_service_catalog_tenant_id",
                schema: "public",
                table: "service_catalog");

            migrationBuilder.DropIndex(
                name: "ix_form_templates_tenant_id",
                schema: "public",
                table: "form_templates");

            migrationBuilder.DropIndex(
                name: "ix_criticality_levels_tenant_id",
                schema: "public",
                table: "criticality_levels");

            migrationBuilder.DropIndex(
                name: "ix_criticality_levels_tenant_id_code",
                schema: "public",
                table: "criticality_levels");

            migrationBuilder.DropIndex(
                name: "ix_assets_tenant_id",
                schema: "public",
                table: "assets");

            migrationBuilder.DropIndex(
                name: "ix_assets_tenant_id_inventory_number",
                schema: "public",
                table: "assets");

            migrationBuilder.DropIndex(
                name: "ix_agent_groups_tenant_id",
                schema: "public",
                table: "agent_groups");

            migrationBuilder.DropIndex(
                name: "ix_agent_groups_tenant_id_code",
                schema: "public",
                table: "agent_groups");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "public",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "public",
                table: "ticket_types");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "public",
                table: "ticket_statuses");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "public",
                table: "service_catalog");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "public",
                table: "form_templates");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "public",
                table: "criticality_levels");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "public",
                table: "assets");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "public",
                table: "agent_groups");

            migrationBuilder.AlterColumn<int>(
                name: "tenant_id",
                schema: "public",
                table: "users",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                schema: "public",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_object_sid",
                schema: "public",
                table: "users",
                column: "object_sid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_username",
                schema: "public",
                table: "users",
                column: "username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ticket_types_code",
                schema: "public",
                table: "ticket_types",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ticket_statuses_code",
                schema: "public",
                table: "ticket_statuses",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_criticality_levels_code",
                schema: "public",
                table: "criticality_levels",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_assets_inventory_number",
                schema: "public",
                table: "assets",
                column: "inventory_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_agent_groups_code",
                schema: "public",
                table: "agent_groups",
                column: "code",
                unique: true);
        }
    }
}
