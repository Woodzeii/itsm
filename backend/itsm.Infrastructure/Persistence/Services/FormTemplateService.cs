using System.Text.Json;
using itsm.Application.Common.Interfaces;
using itsm.Application.Common.Models;
using itsm.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace itsm.Infrastructure.Persistence.Services;

public sealed class FormTemplateService(ItsmDbContext db) : IFormTemplateService
{
    public async Task<IReadOnlyList<TicketTypeOptionResponse>> GetTicketTypesAsync(CancellationToken ct = default) =>
        await db.TicketTypes
            .AsNoTracking()
            .OrderBy(type => type.Name)
            .ThenBy(type => type.Id)
            .Select(type => new TicketTypeOptionResponse(
                type.Id,
                type.Code,
                type.Name,
                type.IsBuiltIn,
                type.IsPortalAvailable))
            .ToArrayAsync(ct);

    public async Task<TicketFormTemplateResponse?> GetFormTemplateAsync(int ticketTypeId, CancellationToken ct = default)
    {
        var ticketType = await db.TicketTypes
            .AsNoTracking()
            .Where(type => type.Id == ticketTypeId)
            .Select(type => new { type.Id, type.Name })
            .FirstOrDefaultAsync(ct);
        if (ticketType is null)
            return null;

        var template = await db.FormTemplates
            .AsNoTracking()
            .Include(item => item.Fields)
            .ThenInclude(field => field.Visibilities)
            .Where(item => item.TicketTypeId == ticketTypeId)
            .OrderByDescending(item => item.UpdatedAt)
            .ThenByDescending(item => item.Id)
            .FirstOrDefaultAsync(ct);

        var fields = template is null
            ? new List<TicketFormFieldResponse>()
            : ReadSchemaFields(template.FieldsSchema);
        if (fields.Count == 0 && template is not null)
            fields = template.Fields.Select(ToResponse).ToList();

        var criticalityOptions = await db.CriticalityLevels
            .AsNoTracking()
            .Where(level => level.IsActive)
            .OrderBy(level => level.SortOrder)
            .Select(level => level.Name)
            .ToArrayAsync(ct);

        EnsureBuiltInField(fields, "title", "Тема", "string", criticalityOptions);
        EnsureBuiltInField(fields, "description", "Описание", "textarea", criticalityOptions);
        EnsureBuiltInField(fields, "priority", "Критичность", "dictionary", criticalityOptions);

        return new TicketFormTemplateResponse(
            ticketType.Id,
            ticketType.Name,
            fields.OrderBy(field => field.SortOrder).ThenBy(field => field.Id, StringComparer.Ordinal).ToArray());
    }

    private static List<TicketFormFieldResponse> ReadSchemaFields(JsonElement schema)
    {
        if (schema.ValueKind != JsonValueKind.Object || !TryGetProperty(schema, "fields", out var fieldsElement))
            return [];
        if (fieldsElement.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Схема формы должна содержать массив fields.");

        var fields = new List<TicketFormFieldResponse>();
        var index = 0;
        foreach (var field in fieldsElement.EnumerateArray())
        {
            if (field.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException("Каждое поле схемы формы должно быть JSON-объектом.");

            var code = GetString(field, "code");
            var name = GetString(field, "name");
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("У каждого поля формы должны быть заполнены code и name.");

            var isEngineerOnly = GetBoolean(field, "isEngineerOnly");
            var explicitVisibility = GetBooleanOrNull(field, "visibleToRequester");
            fields.Add(new TicketFormFieldResponse(
                code,
                code,
                name,
                ToUiFieldType(GetString(field, "fieldType") ?? GetString(field, "type")),
                GetBoolean(field, "isRequired") || GetBoolean(field, "required"),
                GetString(field, "defaultValue"),
                GetString(field, "validation") ?? GetString(field, "validationRules"),
                GetString(field, "hint"),
                !isEngineerOnly && (explicitVisibility ?? IsVisibleToPortal(field)),
                GetStringArray(field, "options"),
                GetBoolean(field, "isBuiltIn") || IsBuiltIn(code, name),
                GetInteger(field, "sortOrder") ?? index + 3,
                GetString(field, "conditionFieldId"),
                GetString(field, "conditionValue")));
            index++;
        }

        return fields;
    }

    private static TicketFormFieldResponse ToResponse(FormField field)
    {
        var portalVisibility = field.Visibilities.FirstOrDefault(visibility =>
            string.Equals(visibility.RoleCode, "portal_user", StringComparison.OrdinalIgnoreCase));
        return new TicketFormFieldResponse(
            field.Code,
            field.Code,
            field.Name,
            ToUiFieldType(field.FieldType),
            field.IsRequired,
            field.DefaultValue,
            field.ValidationRules,
            field.Hint,
            !field.IsEngineerOnly && (portalVisibility?.IsVisible ?? true),
            ReadOptions(field.ValidationRules),
            field.IsBuiltIn || IsBuiltIn(field.Code, field.Name),
            field.SortOrder);
    }

    private static void EnsureBuiltInField(
        List<TicketFormFieldResponse> fields,
        string code,
        string name,
        string type,
        IReadOnlyList<string> criticalityOptions)
    {
        var index = fields.FindIndex(field => IsBuiltInField(field.Code, field.Name, code));
        if (index < 0)
        {
            fields.Add(new TicketFormFieldResponse(
                code,
                code,
                name,
                type,
                true,
                null,
                null,
                null,
                true,
                code == "priority" ? criticalityOptions : null,
                true,
                code switch { "title" => 0, "description" => 1, _ => 2 }));
            return;
        }

        var existing = fields[index];
        fields[index] = existing with
        {
            Immutable = true,
            Required = true,
            Options = code == "priority" && (existing.Options is null || existing.Options.Count == 0)
                ? criticalityOptions
                : existing.Options
        };
    }

    private static bool IsBuiltIn(string code, string name) =>
        IsBuiltInField(code, name, "title")
        || IsBuiltInField(code, name, "description")
        || IsBuiltInField(code, name, "priority");

    private static bool IsBuiltInField(string code, string name, string expectedCode) =>
        string.Equals(code, expectedCode, StringComparison.OrdinalIgnoreCase)
        || (expectedCode == "title" && string.Equals(name, "Тема", StringComparison.OrdinalIgnoreCase))
        || (expectedCode == "description" && string.Equals(name, "Описание", StringComparison.OrdinalIgnoreCase))
        || (expectedCode == "priority" && string.Equals(name, "Критичность", StringComparison.OrdinalIgnoreCase));

    private static string ToUiFieldType(string? type) => type?.ToLowerInvariant() switch
    {
        "text" or "textarea" => "textarea",
        "attachment" or "file" => "file",
        "number" => "number",
        "date" => "date",
        "dictionary" => "dictionary",
        "asset" => "asset",
        "boolean" => "boolean",
        _ => "string"
    };

    private static IReadOnlyList<string>? ReadOptions(string? validationRules)
    {
        if (string.IsNullOrWhiteSpace(validationRules))
            return null;
        if (!validationRules.TrimStart().StartsWith('{'))
            return null;

        using var document = JsonDocument.Parse(validationRules);
        return GetStringArray(document.RootElement, "options");
    }

    private static bool IsVisibleToPortal(JsonElement field)
    {
        if (!TryGetProperty(field, "visibilities", out var visibilities)
            || visibilities.ValueKind != JsonValueKind.Array)
            return true;

        foreach (var visibility in visibilities.EnumerateArray())
        {
            if (string.Equals(GetString(visibility, "roleCode"), "portal_user", StringComparison.OrdinalIgnoreCase))
                return GetBoolean(visibility, "isVisible");
        }

        return true;
    }

    private static string? GetString(JsonElement element, string propertyName) =>
        TryGetProperty(element, propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;

    private static bool GetBoolean(JsonElement element, string propertyName) =>
        GetBooleanOrNull(element, propertyName) ?? false;

    private static bool? GetBooleanOrNull(JsonElement element, string propertyName) =>
        TryGetProperty(element, propertyName, out var property)
            && property.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? property.GetBoolean()
                : null;

    private static int? GetInteger(JsonElement element, string propertyName) =>
        TryGetProperty(element, propertyName, out var property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt32(out var value)
            ? value
            : null;

    private static IReadOnlyList<string>? GetStringArray(JsonElement element, string propertyName) =>
        TryGetProperty(element, propertyName, out var property) && property.ValueKind == JsonValueKind.Array
            ? property.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString()!)
                .ToArray()
            : null;

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }
}
