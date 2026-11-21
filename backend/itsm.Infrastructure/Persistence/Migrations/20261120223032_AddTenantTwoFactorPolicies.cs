using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace itsm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantTwoFactorPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "two_factor_grace_ends_at",
                schema: "public",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "two_factor_grace_days",
                schema: "public",
                table: "tenants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "two_factor_mode",
                schema: "public",
                table: "tenants",
                type: "text",
                nullable: false,
                defaultValue: "all");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "two_factor_grace_ends_at",
                schema: "public",
                table: "users");

            migrationBuilder.DropColumn(
                name: "two_factor_grace_days",
                schema: "public",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "two_factor_mode",
                schema: "public",
                table: "tenants");
        }
    }
}
