using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace itsm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLimitsAndBlocksToTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "block_reason",
                schema: "public",
                table: "tenants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "blocked_at",
                schema: "public",
                table: "tenants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "blocked_until",
                schema: "public",
                table: "tenants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "current_asset_count",
                schema: "public",
                table: "tenants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "current_storage_bytes",
                schema: "public",
                table: "tenants",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "current_ticket_count",
                schema: "public",
                table: "tenants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "current_user_count",
                schema: "public",
                table: "tenants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "is_blocked",
                schema: "public",
                table: "tenants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "max_assets",
                schema: "public",
                table: "tenants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "max_storage_bytes",
                schema: "public",
                table: "tenants",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "max_tickets",
                schema: "public",
                table: "tenants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "max_users",
                schema: "public",
                table: "tenants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "tenants",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "block_reason", "blocked_at", "blocked_until", "current_asset_count", "current_storage_bytes", "current_ticket_count", "current_user_count", "is_blocked", "max_assets", "max_storage_bytes", "max_tickets", "max_users" },
                values: new object[] { null, null, null, 0, 0L, 0, 0, false, 1000, 10737418240L, 10000, 100 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "block_reason",
                schema: "public",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "blocked_at",
                schema: "public",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "blocked_until",
                schema: "public",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "current_asset_count",
                schema: "public",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "current_storage_bytes",
                schema: "public",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "current_ticket_count",
                schema: "public",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "current_user_count",
                schema: "public",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "is_blocked",
                schema: "public",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "max_assets",
                schema: "public",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "max_storage_bytes",
                schema: "public",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "max_tickets",
                schema: "public",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "max_users",
                schema: "public",
                table: "tenants");
        }
    }
}
