using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace itsm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowUsersWithoutTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "tenant_id",
                schema: "public",
                table: "users",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.Sql(
                """
                UPDATE public.users AS user_row
                SET tenant_id = NULL
                WHERE EXISTS (
                    SELECT 1
                    FROM public.user_role_mappings AS mapping
                    JOIN public.system_roles AS role ON role.id = mapping.role_id
                    WHERE mapping.user_id = user_row.id
                      AND role.code = 'tenant_admin'
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE public.users SET tenant_id = 1 WHERE tenant_id IS NULL;");

            migrationBuilder.AlterColumn<int>(
                name: "tenant_id",
                schema: "public",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
