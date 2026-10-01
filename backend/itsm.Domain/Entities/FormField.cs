namespace itsm.Domain.Entities;
public class FormField
{
    public int Id { get; set; }
    public int FormTemplateId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string FieldType { get; set; } = "string";
    public bool IsRequired { get; set; } = false;
    public string? DefaultValue { get; set; }
    public string? ValidationRules { get; set; }
    public string? Hint { get; set; }
    public bool IsBuiltIn { get; set; } = false;
    public bool IsEngineerOnly { get; set; } = false;
    public int? DictionaryId { get; set; }
    public int SortOrder { get; set; } = 0;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public FormTemplate FormTemplate { get; set; } = null!;
    public Dictionary? Dictionary { get; set; }
    public ICollection<FormFieldVisibility> Visibilities { get; set; } = new List<FormFieldVisibility>();
}