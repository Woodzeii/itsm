using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace itsm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketPublicNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Колонка заполняется временными значениями, затем пересчитывается уникальным кодом.
            migrationBuilder.AddColumn<string>(
                name: "ticket_number",
                schema: "public",
                table: "tickets",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            // Бэкфилл: уникальный код XXX-000-00000 для каждой существующей заявки.
            migrationBuilder.Sql(@"
                UPDATE tickets
                SET ticket_number =
                    upper(substr(md5(random()::text || id::text), 1, 3))
                    || '-' || lpad((floor(random() * 1000))::int::text, 3, '0')
                    || '-' || lpad((floor(random() * 100000))::int::text, 5, '0');
            ");

            // Страховка на случай совпадений: перегенерировать все дубликаты.
            migrationBuilder.Sql(@"
                UPDATE tickets t
                SET ticket_number =
                    upper(substr(md5(random()::text || t.id::text || clock_timestamp()::text), 1, 3))
                    || '-' || lpad((floor(random() * 1000))::int::text, 3, '0')
                    || '-' || lpad((floor(random() * 100000))::int::text, 5, '0')
                WHERE t.id IN (
                    SELECT id FROM (
                        SELECT id, row_number() OVER (PARTITION BY tenant_id, ticket_number ORDER BY id) AS rn
                        FROM tickets
                    ) ranked
                    WHERE rn > 1
                );
            ");

            migrationBuilder.CreateIndex(
                name: "ix_tickets_tenant_id_ticket_number",
                schema: "public",
                table: "tickets",
                columns: new[] { "tenant_id", "ticket_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tickets_ticket_number",
                schema: "public",
                table: "tickets",
                column: "ticket_number");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_tickets_tenant_id_ticket_number",
                schema: "public",
                table: "tickets");

            migrationBuilder.DropIndex(
                name: "ix_tickets_ticket_number",
                schema: "public",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "ticket_number",
                schema: "public",
                table: "tickets");
        }
    }
}
