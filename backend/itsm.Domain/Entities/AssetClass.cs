namespace itsm.Domain.Entities;
public class AssetClass
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<AssetClassAttribute> Attributes { get; set; } = new List<AssetClassAttribute>();
    public ICollection<Asset> Assets { get; set; } = new List<Asset>();
}