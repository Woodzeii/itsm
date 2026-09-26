namespace itsm.Domain.Entities;

public class UserRoleMapping
{
    public int UserId { get; set; }
    public int RoleId { get; set; }

    public User User { get; set; } = null!;
    public SystemRole Role { get; set; } = null!;
}