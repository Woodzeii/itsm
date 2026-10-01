namespace itsm.Domain.Entities;
public class Dictionary
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsSystem { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<DictionaryValue> Values { get; set; } = new List<DictionaryValue>();
}