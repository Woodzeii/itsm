using itsm.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace itsm.Infrastructure.Identity;
public class MockLdapService : ILdapService
{
    private readonly ILogger<MockLdapService> _logger;
    private static readonly Dictionary<string, (string Password, LdapUser User)> Users = new()
    {
        ["admin"] = ("admin123", new LdapUser
        {
            Username = "admin",
            Email = "admin@itsm.local",
            FullName = "Администратор Системы",
            Department = "IT",
            ObjectSid = "S-1-5-21-1001"
        }),
        ["i.ivanov"] = ("pass123", new LdapUser
        {
            Username = "i.ivanov",
            Email = "i.ivanov@itsm.local",
            FullName = "Иван Иванов",
            Department = "IT",
            ObjectSid = "S-1-5-21-1002"
        }),
        ["p.petrov"] = ("pass123", new LdapUser
        {
            Username = "p.petrov",
            Email = "p.petrov@itsm.local",
            FullName = "Пётр Петров",
            Department = "IT",
            ObjectSid = "S-1-5-21-1003"
        }),
        ["s.sidorov"] = ("pass123", new LdapUser
        {
            Username = "s.sidorov",
            Email = "s.sidorov@itsm.local",
            FullName = "Сидор Сидоров",
            Department = "Бухгалтерия",
            ObjectSid = "S-1-5-21-1004"
        }),
        ["a.anna"] = ("pass123", new LdapUser
        {
            Username = "a.anna",
            Email = "a.anna@itsm.local",
            FullName = "Анна Аннова",
            Department = "Бухгалтерия",
            ObjectSid = "S-1-5-21-1005"
        }),
        ["m.maria"] = ("pass123", new LdapUser
        {
            Username = "m.maria",
            Email = "m.maria@itsm.local",
            FullName = "Мария Марьина",
            Department = "Отдел продаж",
            ObjectSid = "S-1-5-21-1006"
        }),
    };

    public MockLdapService(ILogger<MockLdapService> logger)
    {
        _logger = logger;
    }

    public Task<bool> ValidateCredentialsAsync(string username, string password)
    {
        _logger.LogWarning(
            "MockLdapService: проверка пароля для '{Username}' (заглушка, реальный AD не используется)",
            username);

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return Task.FromResult(false);

        if (!Users.TryGetValue(username, out var entry))
        {
            _logger.LogInformation("MockLdapService: пользователь '{Username}' не найден", username);
            return Task.FromResult(false);
        }

        return Task.FromResult(entry.Password == password);
    }

    public Task<LdapUser?> FindUserAsync(string username)
	{
		if (string.IsNullOrWhiteSpace(username))
			return Task.FromResult<LdapUser?>(null);

		if (!Users.TryGetValue(username, out var entry))
			return Task.FromResult<LdapUser?>(null);

		return Task.FromResult<LdapUser?>(entry.User);
	}

    public Task<bool> TestConnectionAsync()
    {
        _logger.LogInformation("MockLdapService: TestConnection — всегда true (заглушка)");
        return Task.FromResult(true);
    }
}