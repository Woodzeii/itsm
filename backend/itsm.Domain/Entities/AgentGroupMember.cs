namespace itsm.Domain.Entities;

public class AgentGroupMember
{
    public int AgentGroupId { get; set; }
    public int UserId { get; set; }

    public AgentGroup AgentGroup { get; set; } = null!;
    public User User { get; set; } = null!;
}
