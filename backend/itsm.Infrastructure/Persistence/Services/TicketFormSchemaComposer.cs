using System.Text.Json;
using itsm.Domain.Entities;

namespace itsm.Infrastructure.Persistence.Services;

public static class TicketFormSchemaComposer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static JsonElement CreateDefault(TicketType ticketType)
    {
        var fields = new List<object>
        {
            new { id = "subject", name = "Тема", type = "string", required = true, visibleToRequester = true, immutable = true },
            new { id = "description", name = "Описание", type = "textarea", required = true, visibleToRequester = true, immutable = true },
            new { id = "criticality", name = "Критичность", type = "dictionary", required = true, visibleToRequester = true, immutable = true }
        };
        if (ticketType.Code == "repair")
            fields.Add(new { id = "asset", name = "Актив", type = "asset", required = true, visibleToRequester = true, immutable = true });

        return Build(ticketType.Id, ticketType.Name,
            fields.Select(field => JsonSerializer.SerializeToElement(field, JsonOptions)));
    }

    public static JsonElement CreateCommon() =>
        Build(null, "Общие поля", []);

    public static JsonElement Compose(
        int? ticketTypeId,
        string ticketTypeName,
        params JsonElement[] schemas)
    {
        var fieldsById = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var schema in schemas)
        {
            foreach (var field in GetFields(schema))
            {
                if (TryGetProperty(field, "id", out var id)
                    && id.ValueKind == JsonValueKind.String
                    && id.GetString() is { Length: > 0 } fieldId)
                    fieldsById[fieldId] = field;
            }
        }

        return Build(ticketTypeId, ticketTypeName, fieldsById.Values);
    }

    public static IReadOnlyList<JsonElement> GetFields(JsonElement schema)
    {
        if (schema.ValueKind != JsonValueKind.Object
            || !TryGetProperty(schema, "fields", out var fields)
            || fields.ValueKind != JsonValueKind.Array)
            return [];

        return fields.EnumerateArray().Select(field => field.Clone()).ToArray();
    }

    public static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }
        value = default;
        return false;
    }

    private static JsonElement Build(int? ticketTypeId, string ticketTypeName, IEnumerable<JsonElement> fields) =>
        JsonSerializer.SerializeToElement(new
        {
            ticketTypeId,
            ticketTypeName,
            fields,
        }, JsonOptions);
}
