using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace itsm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketEscalationAcceptance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "accepted_at",
                schema: "public",
                table: "ticket_escalations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "accepted_by_user_id",
                schema: "public",
                table: "ticket_escalations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "comment",
                schema: "public",
                table: "ticket_escalations",
                type: "text",
                nullable: true);

            migrationBuilder.InsertData(
                schema: "public",
                table: "ticket_statuses",
                columns: new[] { "id", "code", "name", "sort_order", "tenant_id" },
                values: new object[] { 6, "escalated", "Эскалирована", 6, 1 });

            migrationBuilder.InsertData(
                schema: "public",
                table: "ticket_status_transitions",
                columns: new[] { "id", "created_at", "from_status_id", "required_role_code", "to_status_id" },
                values: new object[,]
                {
                    { 6, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 1, null, 6 },
                    { 7, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, null, 6 },
                    { 8, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 3, null, 6 },
                    { 9, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6, "agent", 3 },
                    { 10, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 6, "admin", 5 }
                });

            migrationBuilder.CreateIndex(
                name: "ix_ticket_escalations_accepted_by_user_id",
                schema: "public",
                table: "ticket_escalations",
                column: "accepted_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_ticket_escalations_ticket_id_accepted_at",
                schema: "public",
                table: "ticket_escalations",
                columns: new[] { "ticket_id", "accepted_at" });

            migrationBuilder.AddForeignKey(
                name: "fk_ticket_escalations_users_accepted_by_user_id",
                schema: "public",
                table: "ticket_escalations",
                column: "accepted_by_user_id",
                principalSchema: "public",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_ticket_escalations_users_accepted_by_user_id",
                schema: "public",
                table: "ticket_escalations");

            migrationBuilder.DropIndex(
                name: "ix_ticket_escalations_accepted_by_user_id",
                schema: "public",
                table: "ticket_escalations");

            migrationBuilder.DropIndex(
                name: "ix_ticket_escalations_ticket_id_accepted_at",
                schema: "public",
                table: "ticket_escalations");

            migrationBuilder.DeleteData(
                schema: "public",
                table: "ticket_status_transitions",
                keyColumn: "id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                schema: "public",
                table: "ticket_status_transitions",
                keyColumn: "id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                schema: "public",
                table: "ticket_status_transitions",
                keyColumn: "id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                schema: "public",
                table: "ticket_status_transitions",
                keyColumn: "id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                schema: "public",
                table: "ticket_status_transitions",
                keyColumn: "id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                schema: "public",
                table: "ticket_statuses",
                keyColumn: "id",
                keyValue: 6);

            migrationBuilder.DropColumn(
                name: "accepted_at",
                schema: "public",
                table: "ticket_escalations");

            migrationBuilder.DropColumn(
                name: "accepted_by_user_id",
                schema: "public",
                table: "ticket_escalations");

            migrationBuilder.DropColumn(
                name: "comment",
                schema: "public",
                table: "ticket_escalations");
        }
    }
}
