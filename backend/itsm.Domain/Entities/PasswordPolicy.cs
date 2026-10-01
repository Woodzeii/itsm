namespace itsm.Domain.Entities;

public class PasswordPolicy
{
    public int Id { get; set; }
    public int MinLength { get; set; } = 8;
    public bool RequireDigit { get; set; } = true;
    public bool RequireUppercase { get; set; } = false;
    public bool RequireLowercase { get; set; } = true;
    public bool RequireSpecial { get; set; } = false;
    public int? ExpirationDays { get; set; }
    public int MaxFailedAttempts { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}