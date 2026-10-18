using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace itsm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueTicketTypeEscalationRule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_escalation_rules_ticket_type_id",
                schema: "public",
                table: "escalation_rules");

            migrationBuilder.CreateIndex(
                name: "ix_escalation_rules_ticket_type_id",
                schema: "public",
                table: "escalation_rules",
                column: "ticket_type_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_escalation_rules_ticket_type_id",
                schema: "public",
                table: "escalation_rules");

            migrationBuilder.CreateIndex(
                name: "ix_escalation_rules_ticket_type_id",
                schema: "public",
                table: "escalation_rules",
                column: "ticket_type_id");
        }
    }
}
