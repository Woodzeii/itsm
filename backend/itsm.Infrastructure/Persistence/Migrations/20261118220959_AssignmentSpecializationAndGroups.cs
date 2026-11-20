using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace itsm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AssignmentSpecializationAndGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "pk_assignment_settings",
                schema: "public",
                table: "assignment_settings");

            migrationBuilder.DeleteData(
                schema: "public",
                table: "assignment_settings",
                keyColumn: "id",
                keyValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "assignment_group_id",
                schema: "public",
                table: "ticket_types",
                type: "integer",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "id",
                schema: "public",
                table: "assignment_settings",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<int>(
                name: "tenant_id",
                schema: "public",
                table: "assignment_settings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "pk_assignment_settings",
                schema: "public",
                table: "assignment_settings",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.CreateTable(
                name: "agent_group_members",
                schema: "public",
                columns: table => new
                {
                    agent_group_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agent_group_members", x => new { x.agent_group_id, x.user_id });
                    table.ForeignKey(
                        name: "fk_agent_group_members_agent_groups_agent_group_id",
                        column: x => x.agent_group_id,
                        principalSchema: "public",
                        principalTable: "agent_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_agent_group_members_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "public",
                table: "assignment_settings",
                columns: new[] { "id", "tenant_id", "manual_assigner_user_id", "mode", "updated_at" },
                values: new object[] { 1, 1, null, "auto", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) });

            migrationBuilder.UpdateData(
                schema: "public",
                table: "ticket_types",
                keyColumn: "id",
                keyValue: 1,
                column: "assignment_group_id",
                value: null);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "ticket_types",
                keyColumn: "id",
                keyValue: 2,
                column: "assignment_group_id",
                value: null);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "ticket_types",
                keyColumn: "id",
                keyValue: 3,
                column: "assignment_group_id",
                value: null);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "ticket_types",
                keyColumn: "id",
                keyValue: 4,
                column: "assignment_group_id",
                value: null);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "ticket_types",
                keyColumn: "id",
                keyValue: 5,
                column: "assignment_group_id",
                value: null);

            migrationBuilder.CreateIndex(
                name: "ix_ticket_types_assignment_group_id",
                schema: "public",
                table: "ticket_types",
                column: "assignment_group_id");

            migrationBuilder.CreateIndex(
                name: "ix_agent_group_members_user_id",
                schema: "public",
                table: "agent_group_members",
                column: "user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_ticket_types_agent_groups_assignment_group_id",
                schema: "public",
                table: "ticket_types",
                column: "assignment_group_id",
                principalSchema: "public",
                principalTable: "agent_groups",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_ticket_types_agent_groups_assignment_group_id",
                schema: "public",
                table: "ticket_types");

            migrationBuilder.DropTable(
                name: "agent_group_members",
                schema: "public");

            migrationBuilder.DropIndex(
                name: "ix_ticket_types_assignment_group_id",
                schema: "public",
                table: "ticket_types");

            migrationBuilder.DropPrimaryKey(
                name: "pk_assignment_settings",
                schema: "public",
                table: "assignment_settings");

            migrationBuilder.DeleteData(
                schema: "public",
                table: "assignment_settings",
                keyColumns: new[] { "id", "tenant_id" },
                keyColumnTypes: new[] { "integer", "integer" },
                keyValues: new object[] { 1, 1 });

            migrationBuilder.DropColumn(
                name: "assignment_group_id",
                schema: "public",
                table: "ticket_types");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "public",
                table: "assignment_settings");

            migrationBuilder.AlterColumn<int>(
                name: "id",
                schema: "public",
                table: "assignment_settings",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddPrimaryKey(
                name: "pk_assignment_settings",
                schema: "public",
                table: "assignment_settings",
                column: "id");

            migrationBuilder.InsertData(
                schema: "public",
                table: "assignment_settings",
                columns: new[] { "id", "manual_assigner_user_id", "mode", "updated_at" },
                values: new object[] { 1, null, "auto", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) });
        }
    }
}
