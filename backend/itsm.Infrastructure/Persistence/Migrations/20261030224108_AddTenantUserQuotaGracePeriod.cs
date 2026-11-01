using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace itsm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantUserQuotaGracePeriod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "user_limit_grace_ends_at",
                schema: "public",
                table: "tenants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.UpdateData(
                schema: "public",
                table: "tenants",
                keyColumn: "id",
                keyValue: 1,
                column: "user_limit_grace_ends_at",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "user_limit_grace_ends_at",
                schema: "public",
                table: "tenants");
        }
    }
}
