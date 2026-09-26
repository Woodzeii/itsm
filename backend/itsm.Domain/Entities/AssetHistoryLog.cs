namespace itsm.Domain.Entities;

public class AssetHistoryLog
{
    public int Id { get; set; }
    public int AssetId { get; set; }
    public int UserId { get; set; }
    public string? ActionType { get; set; }
    public string? OldStatus { get; set; }
    public string? NewStatus { get; set; }
    public int? OldAssignedUserId { get; set; }
    public int? NewAssignedUserId { get; set; }
    public string? Comment { get; set; }
    public DateTimeOffset ChangedAt { get; set; } = DateTimeOffset.UtcNow;

    public Asset Asset { get; set; } = null!;
    public User User { get; set; } = null!;
    public User? OldAssignedUser { get; set; }
    public User? NewAssignedUser { get; set; }
}