namespace itsm.Application.Common.Interfaces;

public interface IJwtService
{
    string GenerateToken(int userId, string login, string email);
}