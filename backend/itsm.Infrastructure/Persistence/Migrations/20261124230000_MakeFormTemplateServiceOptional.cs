using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using itsm.Infrastructure.Persistence;

#nullable disable

namespace itsm.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ItsmDbContext))]
[Migration("20261124230000_MakeFormTemplateServiceOptional")]
public partial class MakeFormTemplateServiceOptional : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<int>(
            name: "service_id",
            schema: "public",
            table: "form_templates",
            type: "integer",
            nullable: true,
            oldClrType: typeof(int),
            oldType: "integer");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<int>(
            name: "service_id",
            schema: "public",
            table: "form_templates",
            type: "integer",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "integer",
            oldNullable: true);
    }
}
