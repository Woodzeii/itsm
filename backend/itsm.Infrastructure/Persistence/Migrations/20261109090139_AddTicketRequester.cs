using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace itsm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketRequester : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "requester_id",
                schema: "public",
                table: "tickets",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_tickets_requester_id",
                schema: "public",
                table: "tickets",
                column: "requester_id");

            migrationBuilder.AddForeignKey(
                name: "fk_tickets_users_requester_id",
                schema: "public",
                table: "tickets",
                column: "requester_id",
                principalSchema: "public",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_tickets_users_requester_id",
                schema: "public",
                table: "tickets");

            migrationBuilder.DropIndex(
                name: "ix_tickets_requester_id",
                schema: "public",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "requester_id",
                schema: "public",
                table: "tickets");
        }
    }
}
