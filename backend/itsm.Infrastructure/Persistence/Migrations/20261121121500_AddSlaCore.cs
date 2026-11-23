using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using itsm.Infrastructure.Persistence;

#nullable disable

namespace itsm.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ItsmDbContext))]
[Migration("20261121121500_AddSlaCore")]
public partial class AddSlaCore : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "time_zone_id",
            schema: "public",
            table: "working_schedules",
            type: "text",
            nullable: false,
            defaultValue: "Europe/Moscow");

        migrationBuilder.AddColumn<int>(
            name: "version",
            schema: "public",
            table: "working_schedules",
            type: "integer",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<int>(
            name: "tenant_id",
            schema: "public",
            table: "working_schedules",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "working_hours_json",
            schema: "public",
            table: "calendar_exceptions",
            type: "jsonb",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "tenant_id",
            schema: "public",
            table: "calendar_exceptions",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "sla_paused_at",
            schema: "public",
            table: "tickets",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "sla_calendar_snapshot",
            schema: "public",
            table: "tickets",
            type: "jsonb",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "sla_reaction_breached_at",
            schema: "public",
            table: "tickets",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "sla_resolution_breached_at",
            schema: "public",
            table: "tickets",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "sla_policy_id",
            schema: "public",
            table: "sla_values",
            type: "integer",
            nullable: true);

        migrationBuilder.DropIndex(
            name: "ix_working_hours_working_schedule_id_day_of_week",
            schema: "public",
            table: "working_hours");

        migrationBuilder.DropIndex(
            name: "ix_calendar_exceptions_exception_date",
            schema: "public",
            table: "calendar_exceptions");

        migrationBuilder.CreateIndex(
            name: "ix_working_hours_schedule_day_interval",
            schema: "public",
            table: "working_hours",
            columns: new[] { "working_schedule_id", "day_of_week", "start_time", "end_time" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_calendar_exceptions_global_exception_date",
            schema: "public",
            table: "calendar_exceptions",
            column: "exception_date",
            unique: true,
            filter: "tenant_id IS NULL");

        migrationBuilder.CreateIndex(
            name: "ix_calendar_exceptions_tenant_exception_date",
            schema: "public",
            table: "calendar_exceptions",
            columns: new[] { "tenant_id", "exception_date" },
            unique: true,
            filter: "tenant_id IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "ix_working_schedules_tenant_schedule_active",
            schema: "public",
            table: "working_schedules",
            columns: new[] { "tenant_id", "schedule_type", "is_active" });

        migrationBuilder.CreateIndex(
            name: "ix_sla_values_sla_policy_id",
            schema: "public",
            table: "sla_values",
            column: "sla_policy_id");

        migrationBuilder.AddForeignKey(
            name: "fk_sla_values_sla_policies_sla_policy_id",
            schema: "public",
            table: "sla_values",
            column: "sla_policy_id",
            principalSchema: "public",
            principalTable: "sla_policies",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_working_hours_schedule_day_interval",
            schema: "public",
            table: "working_hours");

        migrationBuilder.DropForeignKey(
            name: "fk_sla_values_sla_policies_sla_policy_id",
            schema: "public",
            table: "sla_values");

        migrationBuilder.DropIndex(
            name: "ix_sla_values_sla_policy_id",
            schema: "public",
            table: "sla_values");

        migrationBuilder.CreateIndex(
            name: "ix_working_hours_working_schedule_id_day_of_week",
            schema: "public",
            table: "working_hours",
            columns: new[] { "working_schedule_id", "day_of_week" },
            unique: true);

        migrationBuilder.DropIndex(
            name: "ix_calendar_exceptions_global_exception_date",
            schema: "public",
            table: "calendar_exceptions");

        migrationBuilder.DropIndex(
            name: "ix_calendar_exceptions_tenant_exception_date",
            schema: "public",
            table: "calendar_exceptions");

        migrationBuilder.DropIndex(
            name: "ix_working_schedules_tenant_schedule_active",
            schema: "public",
            table: "working_schedules");

        migrationBuilder.CreateIndex(
            name: "ix_calendar_exceptions_exception_date",
            schema: "public",
            table: "calendar_exceptions",
            column: "exception_date",
            unique: true);

        migrationBuilder.DropColumn(name: "tenant_id", schema: "public", table: "working_schedules");
        migrationBuilder.DropColumn(name: "tenant_id", schema: "public", table: "calendar_exceptions");
        migrationBuilder.DropColumn(name: "time_zone_id", schema: "public", table: "working_schedules");
        migrationBuilder.DropColumn(name: "version", schema: "public", table: "working_schedules");
        migrationBuilder.DropColumn(name: "working_hours_json", schema: "public", table: "calendar_exceptions");
        migrationBuilder.DropColumn(name: "sla_paused_at", schema: "public", table: "tickets");
        migrationBuilder.DropColumn(name: "sla_calendar_snapshot", schema: "public", table: "tickets");
        migrationBuilder.DropColumn(name: "sla_reaction_breached_at", schema: "public", table: "tickets");
        migrationBuilder.DropColumn(name: "sla_resolution_breached_at", schema: "public", table: "tickets");
        migrationBuilder.DropColumn(name: "sla_policy_id", schema: "public", table: "sla_values");
    }
}
