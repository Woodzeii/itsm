namespace itsm.Domain.Entities;

public class SystemRole
{
    public int Id { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;

    public ICollection<UserRoleMapping> UserMappings { get; set; } = new List<UserRoleMapping>();
}