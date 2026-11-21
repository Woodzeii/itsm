using itsm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace itsm.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ItsmDbContext))]
[Migration("20261121210000_SlaCalculationCore")]
public sealed class SlaCalculationCore : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "ticket_type_id",
            schema: "public",
            table: "sla_policies",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "criticality_level_id",
            schema: "public",
            table: "sla_policies",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "working_schedule_id",
            schema: "public",
            table: "sla_policies",
            type: "integer",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "ix_sla_policies_ticket_type_id_criticality_level_id",
            schema: "public",
            table: "sla_policies",
            columns: new[] { "ticket_type_id", "criticality_level_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_sla_policies_criticality_level_id",
            schema: "public",
            table: "sla_policies",
            column: "criticality_level_id");

        migrationBuilder.CreateIndex(
            name: "ix_sla_policies_working_schedule_id",
            schema: "public",
            table: "sla_policies",
            column: "working_schedule_id");

        migrationBuilder.AddForeignKey(
            name: "fk_sla_policies_ticket_types_ticket_type_id",
            schema: "public",
            table: "sla_policies",
            column: "ticket_type_id",
            principalSchema: "public",
            principalTable: "ticket_types",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "fk_sla_policies_criticality_levels_criticality_level_id",
            schema: "public",
            table: "sla_policies",
            column: "criticality_level_id",
            principalSchema: "public",
            principalTable: "criticality_levels",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "fk_sla_policies_working_schedules_working_schedule_id",
            schema: "public",
            table: "sla_policies",
            column: "working_schedule_id",
            principalSchema: "public",
            principalTable: "working_schedules",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);

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

        migrationBuilder.DropIndex(
            name: "ix_working_hours_working_schedule_id_day_of_week",
            schema: "public",
            table: "working_hours");

        migrationBuilder.CreateIndex(
            name: "ix_working_hours_working_schedule_id_day_of_week_start_time",
            schema: "public",
            table: "working_hours",
            columns: new[] { "working_schedule_id", "day_of_week", "start_time" },
            unique: true);

        migrationBuilder.DropIndex(
            name: "ix_calendar_exceptions_exception_date",
            schema: "public",
            table: "calendar_exceptions");

        migrationBuilder.AddColumn<int>(
            name: "working_schedule_id",
            schema: "public",
            table: "calendar_exceptions",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<TimeOnly>(
            name: "start_time",
            schema: "public",
            table: "calendar_exceptions",
            type: "time without time zone",
            nullable: true);

        migrationBuilder.AddColumn<TimeOnly>(
            name: "end_time",
            schema: "public",
            table: "calendar_exceptions",
            type: "time without time zone",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "ix_calendar_exceptions_exception_date",
            schema: "public",
            table: "calendar_exceptions",
            column: "exception_date",
            unique: true,
            filter: "working_schedule_id IS NULL");

        migrationBuilder.CreateIndex(
            name: "ix_calendar_exceptions_working_schedule_id_exception_date",
            schema: "public",
            table: "calendar_exceptions",
            columns: new[] { "working_schedule_id", "exception_date" },
            unique: true,
            filter: "working_schedule_id IS NOT NULL");

        migrationBuilder.AddForeignKey(
            name: "fk_calendar_exceptions_working_schedules_working_schedule_id",
            schema: "public",
            table: "calendar_exceptions",
            column: "working_schedule_id",
            principalSchema: "public",
            principalTable: "working_schedules",
            principalColumn: "id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "sla_pause_started_at",
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

        migrationBuilder.InsertData(
            schema: "public",
            table: "working_hours",
            columns: new[] { "id", "working_schedule_id", "day_of_week", "start_time", "end_time" },
            values: new object[,]
            {
                { 1, 2, 1, new TimeOnly(9, 0), new TimeOnly(18, 0) },
                { 2, 2, 2, new TimeOnly(9, 0), new TimeOnly(18, 0) },
                { 3, 2, 3, new TimeOnly(9, 0), new TimeOnly(18, 0) },
                { 4, 2, 4, new TimeOnly(9, 0), new TimeOnly(18, 0) },
                { 5, 2, 5, new TimeOnly(9, 0), new TimeOnly(18, 0) }
            });

        migrationBuilder.Sql(
            "SELECT setval(pg_get_serial_sequence('public.working_hours', 'id'), " +
            "COALESCE((SELECT MAX(id) FROM public.working_hours), 1));");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "sla_pause_started_at", schema: "public", table: "tickets");
        migrationBuilder.DropColumn(name: "sla_calendar_snapshot", schema: "public", table: "tickets");
        migrationBuilder.DropForeignKey(
            name: "fk_calendar_exceptions_working_schedules_working_schedule_id",
            schema: "public",
            table: "calendar_exceptions");
        migrationBuilder.DropIndex(
            name: "ix_calendar_exceptions_exception_date",
            schema: "public",
            table: "calendar_exceptions");
        migrationBuilder.DropIndex(
            name: "ix_calendar_exceptions_working_schedule_id_exception_date",
            schema: "public",
            table: "calendar_exceptions");
        migrationBuilder.DropColumn(name: "working_schedule_id", schema: "public", table: "calendar_exceptions");
        migrationBuilder.DropColumn(name: "start_time", schema: "public", table: "calendar_exceptions");
        migrationBuilder.DropColumn(name: "end_time", schema: "public", table: "calendar_exceptions");
        migrationBuilder.CreateIndex(
            name: "ix_calendar_exceptions_exception_date",
            schema: "public",
            table: "calendar_exceptions",
            column: "exception_date",
            unique: true);

        migrationBuilder.DropIndex(
            name: "ix_working_hours_working_schedule_id_day_of_week_start_time",
            schema: "public",
            table: "working_hours");
        migrationBuilder.CreateIndex(
            name: "ix_working_hours_working_schedule_id_day_of_week",
            schema: "public",
            table: "working_hours",
            columns: new[] { "working_schedule_id", "day_of_week" },
            unique: true);

        migrationBuilder.DeleteData(
            schema: "public",
            table: "working_hours",
            keyColumn: "id",
            keyValues: new object[] { 1, 2, 3, 4, 5 });

        migrationBuilder.DropColumn(name: "time_zone_id", schema: "public", table: "working_schedules");
        migrationBuilder.DropColumn(name: "version", schema: "public", table: "working_schedules");

        migrationBuilder.DropForeignKey(
            name: "fk_sla_policies_ticket_types_ticket_type_id",
            schema: "public",
            table: "sla_policies");
        migrationBuilder.DropForeignKey(
            name: "fk_sla_policies_criticality_levels_criticality_level_id",
            schema: "public",
            table: "sla_policies");
        migrationBuilder.DropForeignKey(
            name: "fk_sla_policies_working_schedules_working_schedule_id",
            schema: "public",
            table: "sla_policies");
        migrationBuilder.DropIndex(
            name: "ix_sla_policies_ticket_type_id_criticality_level_id",
            schema: "public",
            table: "sla_policies");
        migrationBuilder.DropIndex(
            name: "ix_sla_policies_criticality_level_id",
            schema: "public",
            table: "sla_policies");
        migrationBuilder.DropIndex(
            name: "ix_sla_policies_working_schedule_id",
            schema: "public",
            table: "sla_policies");
        migrationBuilder.DropColumn(name: "ticket_type_id", schema: "public", table: "sla_policies");
        migrationBuilder.DropColumn(name: "criticality_level_id", schema: "public", table: "sla_policies");
        migrationBuilder.DropColumn(name: "working_schedule_id", schema: "public", table: "sla_policies");
    }
}
