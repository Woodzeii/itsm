namespace itsm.Domain.Entities;
public class AssignmentSetting : IMultiTenant
{
    public int Id { get; set; }
    public int TenantId { get; set; } = 1;
    public string Mode { get; set; } = "auto";
    public int? ManualAssignerUserId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public User? ManualAssigner { get; set; }
}
