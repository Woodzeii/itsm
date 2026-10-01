namespace itsm.Domain.Entities;

public class TicketStatus
{
    public int Id { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsSlaPausing { get; set; } = false;
    public int SortOrder { get; set; } = 0;
}