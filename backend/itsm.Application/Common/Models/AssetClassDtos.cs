namespace itsm.Application.Common.Models;

// ==================== AssetClass ====================

public class AssetClassDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int AttributesCount { get; set; }
    public int AssetsCount { get; set; }
}

public class AssetClassDetailDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int AttributesCount { get; set; }
    public int AssetsCount { get; set; }
    public List<AssetClassAttributeDto> Attributes { get; set; } = new();
}

public class CreateAssetClassRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class UpdateAssetClassRequest
{
    public string Name { get; set; } = string.Empty;
}

// ==================== AssetClassAttribute ====================

public class AssetClassAttributeDto
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
}

public class CreateAssetClassAttributeRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = "string";
    public bool IsRequired { get; set; }
    public string? DefaultValue { get; set; }
    public string? Options { get; set; }
    public int SortOrder { get; set; }
}

public class UpdateAssetClassAttributeRequest
{
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = "string";
    public bool IsRequired { get; set; }
    public string? DefaultValue { get; set; }
    public string? Options { get; set; }
    public int SortOrder { get; set; }
}