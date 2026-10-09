using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace itsm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTotpFieldsToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_two_factor_enabled",
                schema: "public",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "two_factor_enabled_at",
                schema: "public",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_two_factor_enabled",
                schema: "public",
                table: "users");

            migrationBuilder.DropColumn(
                name: "two_factor_enabled_at",
                schema: "public",
                table: "users");
        }
    }
}
