using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace itsm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTwoFactorRecoveryCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "recovery_codes_json",
                schema: "public",
                table: "users",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "recovery_codes_json",
                schema: "public",
                table: "users");
        }
    }
}
