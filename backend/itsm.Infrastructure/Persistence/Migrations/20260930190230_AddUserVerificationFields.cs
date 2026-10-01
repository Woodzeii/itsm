using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace itsm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserVerificationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "password_hash",
                schema: "public",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                schema: "public",
                table: "users",
                type: "text",
                nullable: false,
                defaultValue: "Unverified");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "verification_token_expires_at",
                schema: "public",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "verification_token_hash",
                schema: "public",
                table: "users",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_verification_token_hash",
                schema: "public",
                table: "users",
                column: "verification_token_hash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_users_verification_token_hash",
                schema: "public",
                table: "users");

            migrationBuilder.DropColumn(
                name: "password_hash",
                schema: "public",
                table: "users");

            migrationBuilder.DropColumn(
                name: "status",
                schema: "public",
                table: "users");

            migrationBuilder.DropColumn(
                name: "verification_token_expires_at",
                schema: "public",
                table: "users");

            migrationBuilder.DropColumn(
                name: "verification_token_hash",
                schema: "public",
                table: "users");
        }
    }
}
