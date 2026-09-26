namespace itsm.Domain.Entities;

public class TicketAssetMapping
{
    public int TicketId { get; set; }
    public int AssetId { get; set; }

    public Ticket Ticket { get; set; } = null!;
    public Asset Asset { get; set; } = null!;
}