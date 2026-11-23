using System.Text.Json;
using itsm.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace itsm.WebApi.Controllers;

[ApiController]
[Route("api/form-templates")]
[Authorize(Roles = "admin,tenant_owner")]
public sealed class FormTemplatesController(ItsmDbContext db) : ControllerBase
{
    [HttpGet("{ticketTypeId:int}")]
    [EndpointSummary("Получить схему формы типа заявки")]
    [ProducesResponseType(typeof(FormTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FormTemplateResponse>> GetByTicketType(int ticketTypeId, CancellationToken ct)
    {
        var template = await db.FormTemplates
            .AsNoTracking()
            .Where(item => item.TenantId == db.CurrentTenantId && item.TicketTypeId == ticketTypeId)
            .Include(item => item.TicketType)
            .Include(item => item.Fields)
            .OrderByDescending(item => item.UpdatedAt)
            .ThenByDescending(item => item.Id)
            .FirstOrDefaultAsync(ct);

        if (template?.TicketType is null)
            return NotFound();

        var fields = template.Fields
            .OrderBy(field => field.SortOrder)
            .ThenBy(field => field.Id)
            .Select(field => new FormFieldResponse(
                field.Code,
                field.Name,
                field.FieldType,
                field.IsRequired,
                field.IsBuiltIn,
                field.IsEngineerOnly,
                field.DefaultValue,
                field.ValidationRules,
                field.Hint,
                field.DictionaryId,
                field.SortOrder))
            .ToArray();

        return Ok(new FormTemplateResponse(
            template.TicketTypeId!.Value,
            template.TicketType.Name,
            template.FieldsSchema,
            fields));
    }

    public sealed record FormTemplateResponse(
        int TicketTypeId,
        string TicketTypeName,
        JsonElement FieldsSchema,
        IReadOnlyList<FormFieldResponse> Fields);

    public sealed record FormFieldResponse(
        string Code,
        string Name,
        string FieldType,
        bool IsRequired,
        bool IsBuiltIn,
        bool IsEngineerOnly,
        string? DefaultValue,
        string? ValidationRules,
        string? Hint,
        int? DictionaryId,
        int SortOrder);
}
