using itsm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace itsm.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ItsmDbContext))]
[Migration("20261113160800_InitializeTenantTicketConfiguration")]
public sealed class InitializeTenantTicketConfiguration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            INSERT INTO public.ticket_types (tenant_id, code, name, is_built_in, is_portal_available)
            SELECT tenant.id, template.code, template.name, template.is_built_in, template.is_portal_available
            FROM public.tenants AS tenant
            CROSS JOIN public.ticket_types AS template
            WHERE tenant.id <> 1
              AND template.tenant_id = 1
              AND NOT EXISTS (
                  SELECT 1
                  FROM public.ticket_types AS existing
                  WHERE existing.tenant_id = tenant.id
                    AND existing.code = template.code
              );

            INSERT INTO public.ticket_statuses (tenant_id, code, name, is_sla_pausing, sort_order)
            SELECT tenant.id, template.code, template.name, template.is_sla_pausing, template.sort_order
            FROM public.tenants AS tenant
            CROSS JOIN public.ticket_statuses AS template
            WHERE tenant.id <> 1
              AND template.tenant_id = 1
              AND NOT EXISTS (
                  SELECT 1
                  FROM public.ticket_statuses AS existing
                  WHERE existing.tenant_id = tenant.id
                    AND existing.code = template.code
              );

            INSERT INTO public.criticality_levels (tenant_id, code, name, sort_order, is_active, created_at)
            SELECT tenant.id, template.code, template.name, template.sort_order, template.is_active, template.created_at
            FROM public.tenants AS tenant
            CROSS JOIN public.criticality_levels AS template
            WHERE tenant.id <> 1
              AND template.tenant_id = 1
              AND NOT EXISTS (
                  SELECT 1
                  FROM public.criticality_levels AS existing
                  WHERE existing.tenant_id = tenant.id
                    AND existing.code = template.code
              );

            INSERT INTO public.ticket_status_transitions
                (from_status_id, to_status_id, required_role_code, created_at)
            SELECT tenant_from.id, tenant_to.id, template_transition.required_role_code, template_transition.created_at
            FROM public.ticket_status_transitions AS template_transition
            JOIN public.ticket_statuses AS template_from
              ON template_from.id = template_transition.from_status_id
             AND template_from.tenant_id = 1
            JOIN public.ticket_statuses AS template_to
              ON template_to.id = template_transition.to_status_id
             AND template_to.tenant_id = 1
            JOIN public.ticket_statuses AS tenant_from
              ON tenant_from.tenant_id <> 1
             AND tenant_from.code = template_from.code
            JOIN public.ticket_statuses AS tenant_to
              ON tenant_to.tenant_id = tenant_from.tenant_id
             AND tenant_to.code = template_to.code
            WHERE NOT EXISTS (
                SELECT 1
                FROM public.ticket_status_transitions AS existing
                WHERE existing.from_status_id = tenant_from.id
                  AND existing.to_status_id = tenant_to.id
            );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }

    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
    }
}
