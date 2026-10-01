namespace itsm.Application.Common.Models;

public class DictionaryDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsSystem { get; set; }
    public int ValuesCount { get; set; }
}

public class DictionaryDetailDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsSystem { get; set; }
    public List<DictionaryValueDto> Values { get; set; } = new();
}

public class DictionaryValueDto
{
    public int Id { get; set; }
    public int DictionaryId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsArchived { get; set; }
    public int SortOrder { get; set; }
}

public class CreateDictionaryRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class UpdateDictionaryRequest
{
    public string Name { get; set; } = string.Empty;
}

public class CreateDictionaryValueRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; } = 0;
}

public class UpdateDictionaryValueRequest
{
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; } = 0;
}