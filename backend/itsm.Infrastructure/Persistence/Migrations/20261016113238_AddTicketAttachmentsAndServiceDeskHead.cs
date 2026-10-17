using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace itsm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketAttachmentsAndServiceDeskHead : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "service_desk_head_settings",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_service_desk_head_settings", x => x.id);
                    table.CheckConstraint("ck_service_desk_head_settings_singleton", "id = 1");
                    table.ForeignKey(
                        name: "fk_service_desk_head_settings_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ticket_attachment_uploads",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ticket_id = table.Column<int>(type: "integer", nullable: false),
                    uploaded_by_id = table.Column<int>(type: "integer", nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    storage_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ticket_attachment_uploads", x => x.id);
                    table.ForeignKey(
                        name: "fk_ticket_attachment_uploads_tickets_ticket_id",
                        column: x => x.ticket_id,
                        principalSchema: "public",
                        principalTable: "tickets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_ticket_attachment_uploads_users_uploaded_by_id",
                        column: x => x.uploaded_by_id,
                        principalSchema: "public",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_service_desk_head_settings_user_id",
                schema: "public",
                table: "service_desk_head_settings",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_ticket_attachment_uploads_ticket_id_uploaded_by_id_expires_",
                schema: "public",
                table: "ticket_attachment_uploads",
                columns: new[] { "ticket_id", "uploaded_by_id", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ix_ticket_attachment_uploads_uploaded_by_id",
                schema: "public",
                table: "ticket_attachment_uploads",
                column: "uploaded_by_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "service_desk_head_settings",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ticket_attachment_uploads",
                schema: "public");
        }
    }
}
