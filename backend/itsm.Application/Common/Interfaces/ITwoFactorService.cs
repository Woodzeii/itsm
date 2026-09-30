namespace itsm.Application.Common.Interfaces;

public interface ITwoFactorService
{
    string GenerateCode(int userId);
    bool ValidateCode(int userId, string code);
    void InvalidateCode(int userId);
}