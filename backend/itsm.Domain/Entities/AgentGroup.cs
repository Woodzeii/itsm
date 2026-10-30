namespace itsm.Domain.Entities;

public class AgentGroup : IMultiTenant
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;

    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}