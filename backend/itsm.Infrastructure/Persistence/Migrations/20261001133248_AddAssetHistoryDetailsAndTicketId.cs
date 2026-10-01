using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace itsm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAssetHistoryDetailsAndTicketId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "user_id",
                schema: "public",
                table: "asset_history_logs",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "action_type",
                schema: "public",
                table: "asset_history_logs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "details",
                schema: "public",
                table: "asset_history_logs",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ticket_id",
                schema: "public",
                table: "asset_history_logs",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_asset_history_logs_action_type",
                schema: "public",
                table: "asset_history_logs",
                column: "action_type");

            migrationBuilder.CreateIndex(
                name: "ix_asset_history_logs_changed_at",
                schema: "public",
                table: "asset_history_logs",
                column: "changed_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_asset_history_logs_action_type",
                schema: "public",
                table: "asset_history_logs");

            migrationBuilder.DropIndex(
                name: "ix_asset_history_logs_changed_at",
                schema: "public",
                table: "asset_history_logs");

            migrationBuilder.DropColumn(
                name: "details",
                schema: "public",
                table: "asset_history_logs");

            migrationBuilder.DropColumn(
                name: "ticket_id",
                schema: "public",
                table: "asset_history_logs");

            migrationBuilder.AlterColumn<int>(
                name: "user_id",
                schema: "public",
                table: "asset_history_logs",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "action_type",
                schema: "public",
                table: "asset_history_logs",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);
        }
    }
}
