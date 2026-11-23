using System.Text.Json;
using itsm.Application.Common.Models;
using itsm.Infrastructure.Persistence.Services;

namespace itsm.Tests.UnitTests.Infrastructure;

public class TicketFormInputValidatorTests
{
    [Fact]
    public void MissingRequiredCustomFieldReturnsFieldName()
    {
        using var schema = JsonDocument.Parse(FormSchema("""
            { "id": "asset-tag", "name": "Инвентарный номер", "type": "string", "required": true, "visibleToRequester": true }
            """));

        var error = TicketFormInputValidator.Validate(schema.RootElement, Request("{}"), isStaff: false);

        Assert.Equal("Поле «Инвентарный номер» обязательно.", error);
    }

    [Fact]
    public void RegexMismatchReturnsFieldName()
    {
        using var schema = JsonDocument.Parse(FormSchema("""
            { "id": "asset-tag", "name": "Инвентарный номер", "type": "string", "required": true, "visibleToRequester": true, "validation": "^IT-\\d+$" }
            """));

        var error = TicketFormInputValidator.Validate(schema.RootElement, Request("""{"asset-tag":"invalid"}"""), isStaff: false);

        Assert.Equal("Поле «Инвентарный номер» не соответствует заданному формату.", error);
    }

    [Fact]
    public void HiddenConditionalRequiredFieldIsNotRequired()
    {
        using var schema = JsonDocument.Parse(FormSchema("""
            { "id": "kind", "name": "Тип", "type": "string", "required": true, "visibleToRequester": true },
            { "id": "address", "name": "Адрес", "type": "string", "required": true, "visibleToRequester": true, "conditionFieldId": "kind", "conditionOperator": "equals", "conditionValue": "Инцидент" }
            """));

        var error = TicketFormInputValidator.Validate(schema.RootElement, Request("""{"kind":"Запрос"}"""), isStaff: false);

        Assert.Null(error);
    }

    private static CreateTicketRequest Request(string customFields)
    {
        using var values = JsonDocument.Parse(customFields);
        return new CreateTicketRequest
        {
            Title = "Сбой",
            Description = "Описание",
            TypeId = 1,
            Priority = "Средний",
            CustomFieldsData = values.RootElement.Clone(),
        };
    }

    private static string FormSchema(string customFields) => $$"""
        {
          "fields": [
            { "id": "subject", "name": "Тема", "type": "string", "required": true, "immutable": true },
            { "id": "description", "name": "Описание", "type": "textarea", "required": true, "immutable": true },
            { "id": "criticality", "name": "Критичность", "type": "dictionary", "required": true, "immutable": true },
            {{customFields}}
          ]
        }
        """;
}
