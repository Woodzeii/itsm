namespace itsm.Domain.Entities;

public class ServiceDeskHeadSetting : IMultiTenant
{
    public int Id { get; set; } = 1;
    public int TenantId { get; set; }
    public int UserId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public User User { get; set; } = null!;
}
