using System.Text.Json;
using itsm.Domain.Entities;
using itsm.Infrastructure.Persistence;
using itsm.Infrastructure.Persistence.Services;
using Microsoft.EntityFrameworkCore;

namespace itsm.Tests.IntegrationTests;

[Collection(TicketServiceTestCollection.Name)]
public sealed class FormTemplateServiceIntegrationTests(TicketServiceFixture fixture)
{
    [Fact]
    public async Task ReturnsSelectedTypeFieldsAndProtectsBuiltIns()
    {
        await using var scope = await fixture.CreateScopeAsync();
        var db = scope.Db;
        var ticketType = await db.TicketTypes.SingleAsync(type => type.Code == "incident");
        var serviceCatalog = new ServiceCatalog
        {
            TenantId = db.CurrentTenantId,
            Name = $"Incident service {Guid.NewGuid():N}"
        };
        db.ServiceCatalog.Add(serviceCatalog);
        await db.SaveChangesAsync();

        db.FormTemplates.Add(new FormTemplate
        {
            TenantId = db.CurrentTenantId,
            ServiceId = serviceCatalog.Id,
            TicketTypeId = ticketType.Id,
            UpdatedAt = DateTimeOffset.UtcNow,
            FieldsSchema = JsonDocument.Parse("""
                {
                  "fields": [
                    {
                      "code": "environment",
                      "name": "Environment",
                      "fieldType": "text",
                      "isRequired": true,
                      "isEngineerOnly": false
                    }
                  ]
                }
                """).RootElement.Clone()
        });
        await db.SaveChangesAsync();

        var service = new FormTemplateService(db);
        var form = await service.GetFormTemplateAsync(ticketType.Id);

        Assert.NotNull(form);
        Assert.Equal(ticketType.Id, form!.TicketTypeId);
        Assert.Equal(ticketType.Name, form.TicketTypeName);
        Assert.Equal("textarea", Assert.Single(form.Fields, field => field.Code == "environment").Type);
        Assert.Equal(
            new[] { "Тема", "Описание", "Критичность" },
            form.Fields.Where(field => field.Immutable).OrderBy(field => field.SortOrder).Select(field => field.Name));
    }

    [Fact]
    public async Task ListsTicketTypesFromCurrentTenant()
    {
        await using var scope = await fixture.CreateScopeAsync();

        var ticketTypes = await new FormTemplateService(scope.Db).GetTicketTypesAsync();

        Assert.Contains(ticketTypes, type => type.Code == "incident" && type.Name == "Инцидент");
        Assert.Contains(ticketTypes, type => type.Code == "repair" && type.IsBuiltIn && !type.IsPortalAvailable);
    }
}
