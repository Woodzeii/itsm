namespace itsm.Application.Common.Interfaces;

public interface ITwoFactorService
{
    string GenerateCode(int userId, string login, string email);
    TwoFactorUserData? ValidateCode(int userId, string code);
    void InvalidateCode(int userId);
}
public class TwoFactorUserData
{
    public int UserId { get; set; }
    public string Login { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}