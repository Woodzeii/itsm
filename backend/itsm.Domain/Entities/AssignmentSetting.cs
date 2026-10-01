namespace itsm.Domain.Entities;
public class AssignmentSetting
{
    public int Id { get; set; }
    public string Mode { get; set; } = "auto";
    public int? ManualAssignerUserId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public User? ManualAssigner { get; set; }
}