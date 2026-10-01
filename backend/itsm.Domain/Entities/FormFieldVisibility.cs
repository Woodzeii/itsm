namespace itsm.Domain.Entities;
public class FormFieldVisibility
{
    public int Id { get; set; }
    public int FormFieldId { get; set; }
    public string RoleCode { get; set; } = string.Empty;
    public bool IsVisible { get; set; } = true;
    public bool IsEditable { get; set; } = true;

    public FormField FormField { get; set; } = null!;
}