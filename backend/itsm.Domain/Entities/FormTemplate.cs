using System.Text.Json;

namespace itsm.Domain.Entities;

public class FormTemplate
{
    public int Id { get; set; }
    public int ServiceId { get; set; }
    public int? TicketTypeId { get; set; }
    public JsonElement FieldsSchema { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public ServiceCatalog Service { get; set; } = null!;
    public TicketType? TicketType { get; set; }
    public ICollection<FormField> Fields { get; set; } = new List<FormField>();
}