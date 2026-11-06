using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace itsm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TenantScopeServiceDeskHead : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "pk_service_desk_head_settings",
                schema: "public",
                table: "service_desk_head_settings");

            migrationBuilder.AlterColumn<int>(
                name: "id",
                schema: "public",
                table: "service_desk_head_settings",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<int>(
                name: "tenant_id",
                schema: "public",
                table: "service_desk_head_settings",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE public.service_desk_head_settings SET tenant_id = 1;");

            migrationBuilder.AlterColumn<int>(
                name: "tenant_id",
                schema: "public",
                table: "service_desk_head_settings",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "pk_service_desk_head_settings",
                schema: "public",
                table: "service_desk_head_settings",
                columns: new[] { "tenant_id", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DELETE FROM public.service_desk_head_settings WHERE tenant_id <> 1;");

            migrationBuilder.DropPrimaryKey(
                name: "pk_service_desk_head_settings",
                schema: "public",
                table: "service_desk_head_settings");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "public",
                table: "service_desk_head_settings");

            migrationBuilder.AlterColumn<int>(
                name: "id",
                schema: "public",
                table: "service_desk_head_settings",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddPrimaryKey(
                name: "pk_service_desk_head_settings",
                schema: "public",
                table: "service_desk_head_settings",
                column: "id");
        }
    }
}
