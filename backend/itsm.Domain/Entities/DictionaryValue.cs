namespace itsm.Domain.Entities;

public class DictionaryValue
{
    public int Id { get; set; }
    public int DictionaryId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsArchived { get; set; }
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Dictionary Dictionary { get; set; } = null!;
}