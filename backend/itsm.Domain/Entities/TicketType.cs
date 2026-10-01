namespace itsm.Domain.Entities;

public class TicketType
{
    public int Id { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsBuiltIn { get; set; } = false;
    public bool IsPortalAvailable { get; set; } = true;
}