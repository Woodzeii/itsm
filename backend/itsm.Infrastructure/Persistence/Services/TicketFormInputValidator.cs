using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using itsm.Application.Common.Models;

namespace itsm.Infrastructure.Persistence.Services;

public static class TicketFormInputValidator
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);

    public static string? Validate(JsonElement schema, CreateTicketRequest request, bool isStaff)
    {
        if (schema.ValueKind != JsonValueKind.Object
            || !TryGetProperty(schema, "fields", out var schemaFields)
            || schemaFields.ValueKind != JsonValueKind.Array
            || schemaFields.GetArrayLength() == 0)
            return null;

        var fields = new List<FieldRule>();
        foreach (var item in schemaFields.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                return "Схема формы содержит поле в неверном формате.";

            var id = ReadString(item, "id") ?? ReadString(item, "code");
            var name = ReadString(item, "name");
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
                return "Схема формы содержит поле без идентификатора или названия.";

            fields.Add(new FieldRule(
                id,
                name,
                ReadString(item, "type") ?? ReadString(item, "fieldType") ?? "string",
                ReadBoolean(item, "required") ?? ReadBoolean(item, "isRequired") ?? false,
                ReadBoolean(item, "immutable") == true || ReadBoolean(item, "isBuiltIn") == true,
                ReadBoolean(item, "visibleToRequester") ?? !(ReadBoolean(item, "isEngineerOnly") ?? false),
                ReadString(item, "validation") ?? ReadString(item, "validationRules"),
                ReadString(item, "conditionFieldId"),
                ReadString(item, "conditionOperator") ?? "equals",
                ReadString(item, "conditionValue"),
                ReadStringArray(item, "options")));
        }

        if (fields.Select(field => field.Id).Distinct(StringComparer.Ordinal).Count() != fields.Count)
            return "Схема формы содержит повторяющиеся идентификаторы полей.";

        var customValues = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (request.CustomFieldsData is JsonElement customFields)
        {
            if (customFields.ValueKind != JsonValueKind.Object)
                return "Дополнительные поля должны быть JSON-объектом.";

            var customFieldIds = fields.Where(field => !field.IsBuiltIn)
                .Select(field => field.Id)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var property in customFields.EnumerateObject())
            {
                if (!customFieldIds.Contains(property.Name))
                    return $"Неизвестное поле «{property.Name}».";
                if (!TryScalarValue(property.Value, out var value))
                    return $"Поле «{fields.First(field => field.Id == property.Name).Name}» должно содержать простое значение.";
                customValues[property.Name] = value;
            }
        }

        var values = new Dictionary<string, string?>(customValues, StringComparer.Ordinal);
        foreach (var field in fields.Where(field => field.IsBuiltIn))
        {
            values[field.Id] = GetBuiltInValue(field, request);
        }

        foreach (var field in fields)
        {
            if (!isStaff && !field.VisibleToRequester)
                continue;
            if (!IsVisible(field, values))
                continue;

            values.TryGetValue(field.Id, out var value);
            if (!field.IsBuiltIn && field.Required && string.IsNullOrWhiteSpace(value))
                return $"Поле «{field.Name}» обязательно.";
            if (string.IsNullOrWhiteSpace(value))
                continue;

            var typeError = ValidateType(field, value);
            if (typeError is not null)
                return $"Поле «{field.Name}» {typeError}.";
            if (field.Options.Count > 0 && !field.Options.Contains(value, StringComparer.Ordinal))
                return $"Поле «{field.Name}» должно содержать одно из допустимых значений.";
            if (field.Validation is not null)
            {
                try
                {
                    if (!Regex.IsMatch(value, field.Validation, RegexOptions.None, RegexTimeout))
                        return $"Поле «{field.Name}» не соответствует заданному формату.";
                }
                catch (ArgumentException)
                {
                    return $"Для поля «{field.Name}» настроен некорректный шаблон проверки.";
                }
                catch (RegexMatchTimeoutException)
                {
                    return $"Проверка поля «{field.Name}» превысила допустимое время.";
                }
            }
        }

        return null;
    }

    private static bool IsVisible(FieldRule field, IReadOnlyDictionary<string, string?> values)
    {
        if (string.IsNullOrEmpty(field.ConditionFieldId))
            return true;
        if (!values.TryGetValue(field.ConditionFieldId, out var source) || source is null)
            return false;

        return field.ConditionOperator switch
        {
            "equals" => source == (field.ConditionValue ?? string.Empty),
            "notEquals" => source != (field.ConditionValue ?? string.Empty),
            "greaterThan" => decimal.TryParse(source, NumberStyles.Number, CultureInfo.InvariantCulture, out var left)
                && decimal.TryParse(field.ConditionValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var right)
                && left > right,
            _ => false,
        };
    }

    private static string? ValidateType(FieldRule field, string value)
    {
        return field.Type switch
        {
            "number" when !decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _) =>
                "должно быть числом",
            "date" when !DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _) =>
                "должно содержать дату",
            "boolean" when !bool.TryParse(value, out _) =>
                "должно содержать значение да или нет",
            _ => null,
        };
    }

    private static string? GetBuiltInValue(FieldRule field, CreateTicketRequest request)
    {
        var normalizedName = field.Name.Trim().ToLowerInvariant();
        var normalizedId = field.Id.Trim().ToLowerInvariant();
        if (normalizedName == "тема" || normalizedId is "subject" or "title")
            return request.Title;
        if (normalizedName == "описание" || normalizedId == "description")
            return request.Description;
        if (normalizedName == "критичность" || normalizedId is "criticality" or "priority")
            return request.Priority;
        return null;
    }

    private static string? ReadString(JsonElement element, string propertyName) =>
        TryGetProperty(element, propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool? ReadBoolean(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var value))
            return null;
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var values) || values.ValueKind != JsonValueKind.Array)
            return [];
        return values.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString()!)
            .ToArray();
    }

    private static bool TryScalarValue(JsonElement element, out string? value)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                value = element.GetString();
                return true;
            case JsonValueKind.Number:
                value = element.GetRawText();
                return true;
            case JsonValueKind.True:
                value = "true";
                return true;
            case JsonValueKind.False:
                value = "false";
                return true;
            case JsonValueKind.Null:
                value = null;
                return true;
            default:
                value = null;
                return false;
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

    private sealed record FieldRule(
        string Id,
        string Name,
        string Type,
        bool Required,
        bool IsBuiltIn,
        bool VisibleToRequester,
        string? Validation,
        string? ConditionFieldId,
        string ConditionOperator,
        string? ConditionValue,
        IReadOnlyList<string> Options);
}
