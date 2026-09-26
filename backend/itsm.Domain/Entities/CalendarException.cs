namespace itsm.Domain.Entities;

public class CalendarException
{
    public int Id { get; set; }
    public DateOnly ExceptionDate { get; set; }
    public bool IsWorkDay { get; set; }
    public string? Description { get; set; }
}