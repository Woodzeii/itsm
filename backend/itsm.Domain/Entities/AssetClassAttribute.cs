namespace itsm.Domain.Entities;

public class AssetClassAttribute
{
    public int Id { get; set; }
    public int AssetClassId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = "string";
    public bool IsRequired { get; set; }
    public string? DefaultValue { get; set; }
    public string? Options { get; set; }
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public AssetClass AssetClass { get; set; } = null!;
}