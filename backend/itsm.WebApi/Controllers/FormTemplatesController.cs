using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using itsm.Domain.Entities;
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

        return Ok(ToResponse(template));
    }

    [HttpPut("{ticketTypeId:int}")]
    [EndpointSummary("Сохранить схему формы типа заявки")]
    [ProducesResponseType(typeof(FormTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FormTemplateResponse>> Save(
        int ticketTypeId,
        SaveFormTemplateRequest request,
        CancellationToken ct)
    {
        if (request.TicketTypeId != ticketTypeId)
            return Invalid("Идентификатор типа заявки в пути и теле запроса должен совпадать.");

        var template = await db.FormTemplates
            .Where(item => item.TenantId == db.CurrentTenantId && item.TicketTypeId == ticketTypeId)
            .Include(item => item.TicketType)
            .Include(item => item.Fields)
            .OrderByDescending(item => item.UpdatedAt)
            .ThenByDescending(item => item.Id)
            .FirstOrDefaultAsync(ct);

        if (template?.TicketType is null)
            return NotFound();

        if (!string.Equals(request.TicketTypeName, template.TicketType.Name, StringComparison.Ordinal))
            return Invalid("Название типа заявки не соответствует данным сервера.");

        var validationError = ValidateFields(request.Fields, template, out var fields);
        if (validationError is not null)
            return Invalid(validationError);

        template.FieldsSchema = JsonSerializer.SerializeToElement(new
        {
            ticketTypeId,
            ticketTypeName = template.TicketType.Name,
            fields,
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        template.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        return Ok(ToResponse(template));
    }

    private static FormTemplateResponse ToResponse(FormTemplate template)
    {
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

        return new FormTemplateResponse(
            template.TicketTypeId!.Value,
            template.TicketType!.Name,
            template.FieldsSchema,
            fields);
    }

    private static string? ValidateFields(
        IReadOnlyList<FormFieldSchemaRequest>? requestedFields,
        FormTemplate template,
        out FormFieldSchema[] normalizedFields)
    {
        normalizedFields = [];
        if (requestedFields is null || requestedFields.Count is < 3 or > 100)
            return "В схеме должно быть от 3 до 100 полей.";

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var builtInIds = template.Fields
            .Where(field => field.IsBuiltIn)
            .Select(field => field.Code)
            .ToHashSet(StringComparer.Ordinal);
        AddSchemaBuiltInIds(template.FieldsSchema, builtInIds);

        foreach (var field in requestedFields)
        {
            if (field is null)
                return "Схема содержит пустое поле.";
            if (string.IsNullOrWhiteSpace(field.Id) || field.Id.Length > 100
                || string.IsNullOrWhiteSpace(field.Name) || field.Name.Length > 200)
                return "У каждого поля должны быть заданные идентификатор и название.";
            if (!ids.Add(field.Id))
                return $"Идентификатор поля «{field.Id}» повторяется.";
            if (!AllowedFieldTypes.Contains(field.Type ?? string.Empty))
                return $"Для поля «{field.Name}» задан неизвестный тип.";
            if (field.Required is null || field.VisibleToRequester is null)
                return $"Для поля «{field.Name}» не заданы обязательные настройки.";
            if (field.DefaultValue?.Length > 1000 || field.Validation?.Length > 1000 || field.Hint?.Length > 500)
                return $"Настройки поля «{field.Name}» превышают допустимую длину.";
            if (field.Options is { Count: > 100 }
                || field.Options?.Any(option => option is null || option.Length > 200) == true)
                return $"Список значений поля «{field.Name}» некорректен.";
            if (field.ConditionOperator is not null
                && field.ConditionOperator is not ("equals" or "notEquals" or "greaterThan"))
                return $"Для поля «{field.Name}» задан неизвестный оператор условия.";
        }

        if (builtInIds.Any(id => !ids.Contains(id)))
            return "Встроенные поля нельзя удалить.";

        var fieldsById = requestedFields.ToDictionary(field => field.Id!, StringComparer.Ordinal);
        foreach (var field in requestedFields)
        {
            if (string.IsNullOrEmpty(field.ConditionFieldId))
            {
                if (field.ConditionOperator is not null || field.ConditionValue is not null)
                    return $"Для поля «{field.Name}» условие задано не полностью.";
                continue;
            }

            if (field.ConditionFieldId == field.Id || !fieldsById.TryGetValue(field.ConditionFieldId, out var source))
                return $"Для поля «{field.Name}» указано неизвестное поле условия.";
            if (source is null)
                return $"Для поля «{field.Name}» указано пустое поле условия.";
            if (field.ConditionOperator is null || field.ConditionValue is null || field.ConditionValue.Length > 1000)
                return $"Для поля «{field.Name}» не заданы оператор или значение условия.";
            if (field.ConditionOperator == "greaterThan"
                && (source.Type != "number"
                    || !decimal.TryParse(field.ConditionValue, NumberStyles.Number, CultureInfo.InvariantCulture, out _)))
                return $"Оператор «Больше» для поля «{field.Name}» требует числовое поле и значение.";
            if (builtInIds.Contains(field.Id!))
                return "Для встроенных полей нельзя задавать условия отображения.";
        }

        normalizedFields = requestedFields.Select(field => new FormFieldSchema(
            field.Id!,
            field.Name!,
            field.Type!,
            field.Required!.Value,
            field.DefaultValue,
            field.Validation,
            field.Hint,
            field.VisibleToRequester!.Value,
            field.Options,
            builtInIds.Contains(field.Id!),
            field.ConditionFieldId,
            field.ConditionOperator,
            field.ConditionValue)).ToArray();
        return null;
    }

    private static void AddSchemaBuiltInIds(JsonElement schema, ISet<string> ids)
    {
        if (schema.ValueKind != JsonValueKind.Object
            || !TryGetProperty(schema, "fields", out var fields)
            || fields.ValueKind != JsonValueKind.Array)
            return;

        foreach (var field in fields.EnumerateArray())
        {
            if (field.ValueKind != JsonValueKind.Object)
                continue;

            var isBuiltIn = TryGetProperty(field, "isBuiltIn", out var builtIn)
                && builtIn.ValueKind == JsonValueKind.True;
            var isImmutable = TryGetProperty(field, "immutable", out var immutable)
                && immutable.ValueKind == JsonValueKind.True;
            if (!isBuiltIn && !isImmutable)
                continue;
            if (!TryGetProperty(field, "id", out var id)
                && !TryGetProperty(field, "code", out id))
                continue;
            if (id.ValueKind != JsonValueKind.String)
                continue;
            ids.Add(id.GetString()!);
        }
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }
        value = default;
        return false;
    }

    private BadRequestObjectResult Invalid(string message) =>
        BadRequest(new ProblemDetails { Title = "Некорректная схема формы", Detail = message });

    private static readonly HashSet<string> AllowedFieldTypes = new(StringComparer.Ordinal)
    {
        "string", "textarea", "number", "date", "dictionary", "asset", "file", "boolean",
    };

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed class SaveFormTemplateRequest
    {
        public int TicketTypeId { get; init; }
        public string? TicketTypeName { get; init; }
        public List<FormFieldSchemaRequest>? Fields { get; init; }
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed class FormFieldSchemaRequest
    {
        public string? Id { get; init; }
        public string? Name { get; init; }
        public string? Type { get; init; }
        public bool? Required { get; init; }
        public string? DefaultValue { get; init; }
        public string? Validation { get; init; }
        public string? Hint { get; init; }
        public bool? VisibleToRequester { get; init; }
        public List<string?>? Options { get; init; }
        public bool? Immutable { get; init; }
        public string? ConditionFieldId { get; init; }
        public string? ConditionOperator { get; init; }
        public string? ConditionValue { get; init; }
    }

    public sealed record FormFieldSchema(
        string Id,
        string Name,
        string Type,
        bool Required,
        string? DefaultValue,
        string? Validation,
        string? Hint,
        bool VisibleToRequester,
        IReadOnlyList<string?>? Options,
        bool Immutable,
        string? ConditionFieldId,
        string? ConditionOperator,
        string? ConditionValue);

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
