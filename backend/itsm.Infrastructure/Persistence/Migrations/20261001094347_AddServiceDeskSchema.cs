using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace itsm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceDeskSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "criticality_level_id",
                schema: "public",
                table: "tickets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "asset_id",
                schema: "public",
                table: "tickets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "escalation_level",
                schema: "public",
                table: "tickets",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "is_sla_paused",
                schema: "public",
                table: "tickets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "sla_reaction_started_at",
                schema: "public",
                table: "tickets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "sla_resolution_started_at",
                schema: "public",
                table: "tickets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_sla_pausing",
                schema: "public",
                table: "ticket_statuses",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "sort_order",
                schema: "public",
                table: "ticket_statuses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "assignment_settings",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    mode = table.Column<string>(type: "text", nullable: false, defaultValue: "auto"),
                    manual_assigner_user_id = table.Column<int>(type: "integer", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assignment_settings", x => x.id);
                    table.ForeignKey(
                        name: "fk_assignment_settings_users_manual_assigner_user_id",
                        column: x => x.manual_assigner_user_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "criticality_levels",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_criticality_levels", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ticket_status_transitions",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    from_status_id = table.Column<int>(type: "integer", nullable: false),
                    to_status_id = table.Column<int>(type: "integer", nullable: false),
                    required_role_code = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ticket_status_transitions", x => x.id);
                    table.ForeignKey(
                        name: "fk_ticket_status_transitions_ticket_statuses_from_status_id",
                        column: x => x.from_status_id,
                        principalSchema: "public",
                        principalTable: "ticket_statuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_ticket_status_transitions_ticket_statuses_to_status_id",
                        column: x => x.to_status_id,
                        principalSchema: "public",
                        principalTable: "ticket_statuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "working_schedules",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    schedule_type = table.Column<string>(type: "text", nullable: false, defaultValue: "24x7"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_working_schedules", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sla_values",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ticket_type_id = table.Column<int>(type: "integer", nullable: false),
                    criticality_level_id = table.Column<int>(type: "integer", nullable: false),
                    reaction_time_minutes = table.Column<int>(type: "integer", nullable: true),
                    resolution_time_minutes = table.Column<int>(type: "integer", nullable: true),
                    is_disabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sla_values", x => x.id);
                    table.ForeignKey(
                        name: "fk_sla_values_criticality_levels_criticality_level_id",
                        column: x => x.criticality_level_id,
                        principalSchema: "public",
                        principalTable: "criticality_levels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sla_values_ticket_types_ticket_type_id",
                        column: x => x.ticket_type_id,
                        principalSchema: "public",
                        principalTable: "ticket_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "working_hours",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    working_schedule_id = table.Column<int>(type: "integer", nullable: false),
                    day_of_week = table.Column<int>(type: "integer", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_working_hours", x => x.id);
                    table.ForeignKey(
                        name: "fk_working_hours_working_schedules_working_schedule_id",
                        column: x => x.working_schedule_id,
                        principalSchema: "public",
                        principalTable: "working_schedules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tickets_asset_id",
                schema: "public",
                table: "tickets",
                column: "asset_id");

            migrationBuilder.CreateIndex(
                name: "ix_tickets_criticality_level_id",
                schema: "public",
                table: "tickets",
                column: "criticality_level_id");

            migrationBuilder.CreateIndex(
                name: "ix_assignment_settings_manual_assigner_user_id",
                schema: "public",
                table: "assignment_settings",
                column: "manual_assigner_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_criticality_levels_code",
                schema: "public",
                table: "criticality_levels",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sla_values_criticality_level_id",
                schema: "public",
                table: "sla_values",
                column: "criticality_level_id");

            migrationBuilder.CreateIndex(
                name: "ix_sla_values_ticket_type_id_criticality_level_id",
                schema: "public",
                table: "sla_values",
                columns: new[] { "ticket_type_id", "criticality_level_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ticket_status_transitions_from_status_id_to_status_id",
                schema: "public",
                table: "ticket_status_transitions",
                columns: new[] { "from_status_id", "to_status_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ticket_status_transitions_to_status_id",
                schema: "public",
                table: "ticket_status_transitions",
                column: "to_status_id");

            migrationBuilder.CreateIndex(
                name: "ix_working_hours_working_schedule_id_day_of_week",
                schema: "public",
                table: "working_hours",
                columns: new[] { "working_schedule_id", "day_of_week" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_tickets_assets_asset_id",
                schema: "public",
                table: "tickets",
                column: "asset_id",
                principalSchema: "public",
                principalTable: "assets",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_tickets_criticality_levels_criticality_level_id",
                schema: "public",
                table: "tickets",
                column: "criticality_level_id",
                principalSchema: "public",
                principalTable: "criticality_levels",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_tickets_assets_asset_id",
                schema: "public",
                table: "tickets");

            migrationBuilder.DropForeignKey(
                name: "fk_tickets_criticality_levels_criticality_level_id",
                schema: "public",
                table: "tickets");

            migrationBuilder.DropTable(name: "assignment_settings", schema: "public");
            migrationBuilder.DropTable(name: "sla_values", schema: "public");
            migrationBuilder.DropTable(name: "ticket_status_transitions", schema: "public");
            migrationBuilder.DropTable(name: "working_hours", schema: "public");
            migrationBuilder.DropTable(name: "criticality_levels", schema: "public");
            migrationBuilder.DropTable(name: "working_schedules", schema: "public");

            migrationBuilder.DropIndex(name: "ix_tickets_asset_id", schema: "public", table: "tickets");
            migrationBuilder.DropIndex(name: "ix_tickets_criticality_level_id", schema: "public", table: "tickets");

            migrationBuilder.DropColumn(name: "asset_id", schema: "public", table: "tickets");
            migrationBuilder.DropColumn(name: "criticality_level_id", schema: "public", table: "tickets");
            migrationBuilder.DropColumn(name: "escalation_level", schema: "public", table: "tickets");
            migrationBuilder.DropColumn(name: "is_sla_paused", schema: "public", table: "tickets");
            migrationBuilder.DropColumn(name: "sla_reaction_started_at", schema: "public", table: "tickets");
            migrationBuilder.DropColumn(name: "sla_resolution_started_at", schema: "public", table: "tickets");
            migrationBuilder.DropColumn(name: "is_sla_pausing", schema: "public", table: "ticket_statuses");
            migrationBuilder.DropColumn(name: "sort_order", schema: "public", table: "ticket_statuses");
        }
    }
}