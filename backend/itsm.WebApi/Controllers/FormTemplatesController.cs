using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using itsm.Domain.Entities;
using itsm.Infrastructure.Persistence;
using itsm.Infrastructure.Persistence.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace itsm.WebApi.Controllers;

[ApiController]
[Route("api/form-templates")]
[Authorize]
public sealed class FormTemplatesController(ItsmDbContext db) : ControllerBase
{
    [HttpGet("{ticketTypeId:int}")]
    [Authorize(Roles = "admin,tenant_owner,manager,agent,portal_user")]
    [EndpointSummary("Получить схему формы типа заявки")]
    [ProducesResponseType(typeof(FormTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FormTemplateResponse>> GetByTicketType(
        int ticketTypeId,
        [FromQuery] bool includeCommon = true,
        CancellationToken ct = default)
    {
        var ticketType = await db.TicketTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantId == db.CurrentTenantId && item.Id == ticketTypeId, ct);
        if (ticketType is null)
            return NotFound();
        if (User.IsInRole("portal_user") && !IsStaff() && !ticketType.IsPortalAvailable)
            return NotFound();

        var template = await db.FormTemplates
            .AsNoTracking()
            .Where(item => item.TenantId == db.CurrentTenantId && item.TicketTypeId == ticketTypeId)
            .OrderByDescending(item => item.UpdatedAt)
            .ThenByDescending(item => item.Id)
            .FirstOrDefaultAsync(ct);
        var schema = TicketFormSchemaComposer.Compose(
            ticketType.Id,
            ticketType.Name,
            TicketFormSchemaComposer.CreateDefault(ticketType),
            template?.FieldsSchema ?? TicketFormSchemaComposer.CreateDefault(ticketType));
        if (includeCommon)
        {
            var common = await FindTemplateAsync(null, ct);
            schema = TicketFormSchemaComposer.Compose(
                ticketType.Id,
                ticketType.Name,
                common?.FieldsSchema ?? TicketFormSchemaComposer.CreateCommon(),
                schema);
        }
        schema = await PopulateDictionaryOptionsAsync(schema, ct);
        return Ok(ToResponse(ticketType.Id, ticketType.Name, schema));
    }

    [HttpGet("common")]
    [Authorize(Roles = "admin,tenant_owner,manager,agent")]
    [EndpointSummary("Получить общие поля для всех типов заявок")]
    [ProducesResponseType(typeof(FormTemplateResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<FormTemplateResponse>> GetCommon(CancellationToken ct)
    {
        var template = await FindTemplateAsync(null, ct);
        var schema = await PopulateDictionaryOptionsAsync(
            template?.FieldsSchema ?? TicketFormSchemaComposer.CreateCommon(),
            ct);
        return Ok(ToResponse(null, "Общие поля", schema));
    }

    [HttpPut("{ticketTypeId:int}")]
    [Authorize(Roles = "admin,tenant_owner")]
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

        var ticketType = await db.TicketTypes
            .FirstOrDefaultAsync(item => item.TenantId == db.CurrentTenantId && item.Id == ticketTypeId, ct);
        if (ticketType is null)
            return NotFound();

        if (!string.Equals(request.TicketTypeName, ticketType.Name, StringComparison.Ordinal))
            return Invalid("Название типа заявки не соответствует данным сервера.");

        var template = await FindTemplateAsync(ticketTypeId, ct);
        var currentSchema = TicketFormSchemaComposer.Compose(
            ticketType.Id,
            ticketType.Name,
            TicketFormSchemaComposer.CreateDefault(ticketType),
            template?.FieldsSchema ?? TicketFormSchemaComposer.CreateDefault(ticketType));
        var commonTemplate = await FindTemplateAsync(null, ct);
        var commonIds = TicketFormSchemaComposer.GetFields(commonTemplate?.FieldsSchema ?? TicketFormSchemaComposer.CreateCommon())
            .Select(field => ReadString(field, "id"))
            .Where(id => id is not null)
            .ToHashSet(StringComparer.Ordinal);

        var validationError = ValidateFields(request.Fields, currentSchema, isCommon: false, ticketType.Code == "repair", out var fields);
        if (validationError is not null)
            return Invalid(validationError);
        var dictionaryError = await ValidateDictionaryReferencesAsync(fields, ct);
        if (dictionaryError is not null)
            return Invalid(dictionaryError);
        if (fields.Any(field => commonIds.Contains(field.Id)))
            return Invalid("Идентификаторы общих полей нельзя повторно использовать в форме типа заявки.");

        var nextSchema = JsonSerializer.SerializeToElement(new
        {
            ticketTypeId,
            ticketTypeName = ticketType.Name,
            fields,
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var cleanupError = await ClearRemovedFieldValuesAsync(template?.FieldsSchema, nextSchema, ticketTypeId, ct);
        if (cleanupError is not null)
            return Invalid(cleanupError);

        template ??= new FormTemplate
        {
            TenantId = db.CurrentTenantId,
            TicketTypeId = ticketTypeId,
            FieldsSchema = nextSchema
        };
        template.FieldsSchema = nextSchema;
        template.UpdatedAt = DateTimeOffset.UtcNow;
        if (template.Id == 0)
            db.FormTemplates.Add(template);
        await db.SaveChangesAsync(ct);

        return Ok(ToResponse(ticketTypeId, ticketType.Name, await PopulateDictionaryOptionsAsync(nextSchema, ct)));
    }

    [HttpPut("common")]
    [Authorize(Roles = "admin,tenant_owner")]
    [EndpointSummary("Сохранить общие поля для всех типов заявок")]
    [ProducesResponseType(typeof(FormTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FormTemplateResponse>> SaveCommon(SaveFormTemplateRequest request, CancellationToken ct)
    {
        if (request.TicketTypeId is not null)
            return Invalid("Для общей формы идентификатор типа заявки должен быть пустым.");
        if (!string.Equals(request.TicketTypeName, "Общие поля", StringComparison.Ordinal))
            return Invalid("Название общей формы не соответствует данным сервера.");

        var template = await FindTemplateAsync(null, ct);
        var currentSchema = template?.FieldsSchema ?? TicketFormSchemaComposer.CreateCommon();
        var validationError = ValidateFields(request.Fields, currentSchema, isCommon: true, isRepair: false, out var fields);
        if (validationError is not null)
            return Invalid(validationError);
        var dictionaryError = await ValidateDictionaryReferencesAsync(fields, ct);
        if (dictionaryError is not null)
            return Invalid(dictionaryError);
        var typeSchemas = await db.FormTemplates
            .AsNoTracking()
            .Where(item => item.TenantId == db.CurrentTenantId && item.TicketTypeId != null)
            .Select(item => item.FieldsSchema)
            .ToArrayAsync(ct);
        var commonIds = fields.Select(field => field.Id).ToHashSet(StringComparer.Ordinal);
        if (typeSchemas.SelectMany(TicketFormSchemaComposer.GetFields)
            .Any(field => ReadString(field, "id") is { } id && commonIds.Contains(id)))
            return Invalid("Идентификаторы общих полей нельзя повторно использовать в форме конкретного типа.");

        var nextSchema = JsonSerializer.SerializeToElement(new
        {
            ticketTypeId = (int?)null,
            ticketTypeName = "Общие поля",
            fields,
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var cleanupError = await ClearRemovedFieldValuesAsync(template?.FieldsSchema, nextSchema, null, ct);
        if (cleanupError is not null)
            return Invalid(cleanupError);

        template ??= new FormTemplate
        {
            TenantId = db.CurrentTenantId,
            FieldsSchema = nextSchema
        };
        template.FieldsSchema = nextSchema;
        template.UpdatedAt = DateTimeOffset.UtcNow;
        if (template.Id == 0)
            db.FormTemplates.Add(template);
        await db.SaveChangesAsync(ct);

        return Ok(ToResponse(null, "Общие поля", await PopulateDictionaryOptionsAsync(nextSchema, ct)));
    }

    private async Task<FormTemplate?> FindTemplateAsync(int? ticketTypeId, CancellationToken ct) =>
        await db.FormTemplates
            .Where(item => item.TenantId == db.CurrentTenantId && item.TicketTypeId == ticketTypeId)
            .OrderByDescending(item => item.UpdatedAt)
            .ThenByDescending(item => item.Id)
            .FirstOrDefaultAsync(ct);

    private static FormTemplateResponse ToResponse(int? ticketTypeId, string ticketTypeName, JsonElement schema)
    {
        var fields = TicketFormSchemaComposer.GetFields(schema)
            .Select((field, index) => new FormFieldResponse(
                ReadString(field, "id") ?? ReadString(field, "code") ?? string.Empty,
                ReadString(field, "name") ?? string.Empty,
                ReadString(field, "type") ?? ReadString(field, "fieldType") ?? "string",
                ReadBoolean(field, "required") ?? ReadBoolean(field, "isRequired") ?? false,
                ReadBoolean(field, "immutable") == true || ReadBoolean(field, "isBuiltIn") == true,
                !(ReadBoolean(field, "visibleToRequester") ?? !(ReadBoolean(field, "isEngineerOnly") ?? false)),
                ReadString(field, "defaultValue"),
                ReadString(field, "validation") ?? ReadString(field, "validationRules"),
                ReadString(field, "hint"),
                null,
                index))
            .ToArray();

        return new FormTemplateResponse(ticketTypeId, ticketTypeName, schema, fields);
    }

    private async Task<string?> ValidateDictionaryReferencesAsync(
        IReadOnlyList<FormFieldSchema> fields,
        CancellationToken ct)
    {
        var codes = fields.Select(field => field.DictionaryCode)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var dictionaryFields = fields.Where(field => field.Type == "dictionary" && !field.Immutable).ToArray();
        foreach (var field in dictionaryFields)
        {
            if (string.IsNullOrWhiteSpace(field.DictionaryCode) && field.Options is not { Count: > 0 })
                return $"Для поля «{field.Name}» выберите справочник или укажите значения.";
        }
        if (codes.Length == 0)
            return null;

        var found = await db.Dictionaries.AsNoTracking()
            .Include(dictionary => dictionary.Values.Where(value => !value.IsArchived))
            .Where(dictionary => dictionary.TenantId == db.CurrentTenantId
                && !dictionary.IsArchived && codes.Contains(dictionary.Code))
            .ToDictionaryAsync(dictionary => dictionary.Code, dictionary => dictionary.Values.Count > 0, StringComparer.Ordinal, ct);
        var missing = codes.FirstOrDefault(code => !found.TryGetValue(code, out var hasValues) || !hasValues);
        return missing is null ? null : $"Справочник «{missing}» не найден, архивирован или не содержит активных значений.";
    }

    private async Task<JsonElement> PopulateDictionaryOptionsAsync(JsonElement schema, CancellationToken ct)
    {
        var dictionaryCodes = TicketFormSchemaComposer.GetFields(schema)
            .Select(field => ReadString(field, "dictionaryCode"))
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (dictionaryCodes.Length == 0)
            return schema;

        var dictionaries = await db.Dictionaries.AsNoTracking()
            .Include(dictionary => dictionary.Values.Where(value => !value.IsArchived))
            .Where(dictionary => dictionary.TenantId == db.CurrentTenantId
                && !dictionary.IsArchived && dictionaryCodes.Contains(dictionary.Code))
            .ToDictionaryAsync(
                dictionary => dictionary.Code,
                dictionary => dictionary.Values.OrderBy(value => value.SortOrder).ThenBy(value => value.Id)
                    .Select(value => value.Name).ToArray(),
                StringComparer.Ordinal,
                ct);

        var node = JsonNode.Parse(schema.GetRawText()) as JsonObject;
        if (node?["fields"] is JsonArray fields)
        {
            foreach (var field in fields.OfType<JsonObject>())
            {
                var code = field["dictionaryCode"]?.GetValue<string>();
                if (code is not null && dictionaries.TryGetValue(code, out var options))
                    field["options"] = JsonSerializer.SerializeToNode(options);
            }
        }
        return node is null ? schema : JsonSerializer.SerializeToElement(node);
    }

    private static string? ValidateFields(
        IReadOnlyList<FormFieldSchemaRequest>? requestedFields,
        JsonElement currentSchema,
        bool isCommon,
        bool isRepair,
        out FormFieldSchema[] normalizedFields)
    {
        normalizedFields = [];
        if (requestedFields is null || requestedFields.Count > 100 || (!isCommon && requestedFields.Count < (isRepair ? 4 : 3)))
            return isCommon ? "В общей схеме должно быть не более 100 полей." : "В схеме должно быть от 3 до 100 полей.";

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var builtInIds = isCommon ? new HashSet<string>(StringComparer.Ordinal) : new HashSet<string>(
            isRepair ? ["subject", "description", "criticality", "asset"] : ["subject", "description", "criticality"],
            StringComparer.Ordinal);
        AddSchemaBuiltInIds(currentSchema, builtInIds);
        if (!isCommon)
        {
            builtInIds.UnionWith(isRepair
                ? ["subject", "description", "criticality", "asset"]
                : ["subject", "description", "criticality"]);
        }

        foreach (var field in requestedFields)
        {
            if (field is null)
                return "Схема содержит пустое поле.";
            if (string.IsNullOrWhiteSpace(field.Id) || field.Id.Length > 100
                || string.IsNullOrWhiteSpace(field.Name) || field.Name.Length > 200)
                return "У каждого поля должны быть заданные идентификатор и название.";
            if (isCommon && field.Id is "subject" or "title" or "description" or "criticality" or "priority" or "asset")
                return $"Идентификатор «{field.Id}» зарезервирован для встроенных полей.";
            if (!ids.Add(field.Id))
                return $"Идентификатор поля «{field.Id}» повторяется.";
            if (!AllowedFieldTypes.Contains(field.Type ?? string.Empty))
                return $"Для поля «{field.Name}» задан неизвестный тип.";
            if (isCommon && field.Type == "asset")
                return "Поле выбора актива должно быть задано для конкретного типа заявки.";
            if (field.Required is null || field.VisibleToRequester is null)
                return $"Для поля «{field.Name}» не заданы обязательные настройки.";
            if (field.DefaultValue?.Length > 1000 || field.Validation?.Length > 1000
                || field.Hint?.Length > 500 || field.DictionaryCode?.Length > 100)
                return $"Настройки поля «{field.Name}» превышают допустимую длину.";
            if (!string.IsNullOrEmpty(field.Validation))
            {
                try
                {
                    _ = new Regex(field.Validation, RegexOptions.None, TimeSpan.FromMilliseconds(100));
                }
                catch (ArgumentException)
                {
                    return $"Для поля «{field.Name}» задан некорректный шаблон проверки.";
                }
            }
            if (field.Options is { Count: > 100 }
                || field.Options?.Any(option => option is null || option.Length > 200) == true)
                return $"Список значений поля «{field.Name}» некорректен.";
            if (field.ConditionOperator is not null
                && field.ConditionOperator is not ("equals" or "notEquals" or "greaterThan"))
                return $"Для поля «{field.Name}» задан неизвестный оператор условия.";
        }

        if (requestedFields.Count(field => field?.Type == "asset") > 1)
            return "В форме может быть только одно поле выбора актива.";
        if (builtInIds.Any(id => !ids.Contains(id)))
            return "Встроенные поля нельзя удалить.";

        if (!isCommon)
        {
            var expected = new Dictionary<string, (string Name, string Type)>(StringComparer.Ordinal)
            {
                ["subject"] = ("Тема", "string"),
                ["description"] = ("Описание", "textarea"),
                ["criticality"] = ("Критичность", "dictionary")
            };
            if (isRepair)
                expected["asset"] = ("Актив", "asset");
            foreach (var (id, properties) in expected)
            {
                var builtIn = requestedFields.FirstOrDefault(field => field.Id == id);
                if (builtIn is null || builtIn.Name != properties.Name || builtIn.Type != properties.Type
                    || builtIn.Required != true || builtIn.Immutable != true || builtIn.VisibleToRequester != true)
                    return $"Встроенное поле «{properties.Name}» должно оставаться обязательным, неизменяемым и видимым автору.";
            }
        }

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
            field.DictionaryCode,
            field.VisibleToRequester!.Value,
            field.Options,
            builtInIds.Contains(field.Id!),
            field.ConditionFieldId,
            field.ConditionOperator,
            field.ConditionValue)).ToArray();
        return null;
    }

    private async Task<string?> ClearRemovedFieldValuesAsync(
        JsonElement? oldSchema,
        JsonElement newSchema,
        int? ticketTypeId,
        CancellationToken ct)
    {
        if (oldSchema is not JsonElement old)
            return null;

        var newIds = TicketFormSchemaComposer.GetFields(newSchema)
            .Select(field => ReadString(field, "id"))
            .Where(id => id is not null)
            .ToHashSet(StringComparer.Ordinal);
        var oldFields = TicketFormSchemaComposer.GetFields(old);
        var removedIds = oldFields
            .Select(field => ReadString(field, "id"))
            .Where(id => id is not null && !newIds.Contains(id))
            .Select(id => id!)
            .ToHashSet(StringComparer.Ordinal);
        if (removedIds.Count == 0)
            return null;

        var ticketsQuery = db.Tickets.Where(ticket => ticket.TenantId == db.CurrentTenantId);
        if (ticketTypeId is int typeId)
            ticketsQuery = ticketsQuery.Where(ticket => ticket.TypeId == typeId);
        var tickets = await ticketsQuery.ToListAsync(ct);

        var removedAssetIds = oldFields
            .Where(field => ReadString(field, "type") == "asset" || ReadString(field, "fieldType") == "asset")
            .Select(field => ReadString(field, "id"))
            .Where(id => id is not null && removedIds.Contains(id))
            .Select(id => id!)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var ticket in tickets)
        {
            if (removedAssetIds.Count > 0 && (ticket.AssetId is not null
                || HasNonEmptyValue(ticket.CustomFieldsData, removedAssetIds)))
                return "Нельзя удалить поле выбора актива, пока оно заполнено хотя бы в одной заявке.";
        }

        foreach (var ticket in tickets)
        {
            if (ticket.CustomFieldsData is not JsonElement data || data.ValueKind != JsonValueKind.Object)
                continue;
            var node = JsonNode.Parse(data.GetRawText()) as JsonObject;
            if (node is null)
                continue;
            foreach (var id in removedIds)
                node.Remove(id);
            ticket.CustomFieldsData = JsonSerializer.SerializeToElement(node);
        }

        return null;
    }

    private static bool HasNonEmptyValue(JsonElement? values, IReadOnlySet<string> ids)
    {
        if (values is not JsonElement json || json.ValueKind != JsonValueKind.Object)
            return false;
        foreach (var property in json.EnumerateObject())
        {
            if (ids.Contains(property.Name) && property.Value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined)
                && !(property.Value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(property.Value.GetString())))
                return true;
        }
        return false;
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

    private static string? ReadString(JsonElement element, string name) =>
        TicketFormSchemaComposer.TryGetProperty(element, name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool? ReadBoolean(JsonElement element, string name)
    {
        if (!TicketFormSchemaComposer.TryGetProperty(element, name, out var value))
            return null;
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
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

    private bool IsStaff() =>
        User.IsInRole("admin") || User.IsInRole("tenant_owner")
        || User.IsInRole("manager") || User.IsInRole("agent");

    private static readonly HashSet<string> AllowedFieldTypes = new(StringComparer.Ordinal)
    {
        "string", "textarea", "number", "date", "dictionary", "asset", "file", "boolean",
    };

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed class SaveFormTemplateRequest
    {
        public int? TicketTypeId { get; init; }
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
        public string? DictionaryCode { get; init; }
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
        string? DictionaryCode,
        bool VisibleToRequester,
        IReadOnlyList<string?>? Options,
        bool Immutable,
        string? ConditionFieldId,
        string? ConditionOperator,
        string? ConditionValue);

    public sealed record FormTemplateResponse(
        int? TicketTypeId,
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
