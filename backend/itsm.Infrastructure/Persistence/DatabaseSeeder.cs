using itsm.Application.Common.Interfaces;
using itsm.Domain.Constants;
using itsm.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace itsm.Infrastructure.Persistence;

/// <summary>
/// Наполняет БД тестовыми пользователями для dev-окружения.
/// Пропускает создание, если пользователи уже есть.
/// </summary>
public class DatabaseSeeder : IDatabaseSeeder
{
    private readonly ItsmDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(ItsmDbContext db, IPasswordHasher passwordHasher, ILogger<DatabaseSeeder> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await _db.Users.AnyAsync(ct))
        {
            _logger.LogInformation("DatabaseSeeder: пользователи уже существуют, пропускаем");
            return;
        }

        _logger.LogInformation("DatabaseSeeder: создаём тестовых пользователей");

        // Роли (уже есть благодаря HasData в миграции)
        var adminRoleId = await _db.SystemRoles.Where(r => r.Code == "admin").Select(r => r.Id).FirstAsync(ct);
        var managerRoleId = await _db.SystemRoles.Where(r => r.Code == "manager").Select(r => r.Id).FirstAsync(ct);
        var agentRoleId = await _db.SystemRoles.Where(r => r.Code == "agent").Select(r => r.Id).FirstAsync(ct);
        var portalRoleId = await _db.SystemRoles.Where(r => r.Code == "portal_user").Select(r => r.Id).FirstAsync(ct);

        var admin = NewUser("admin", "admin@itsm.local", "Администратор Системы", "IT", "admin123");
        var ivanov = NewUser("i.ivanov", "i.ivanov@itsm.local", "Иван Иванов", "IT", "pass123");
        var petrov = NewUser("p.petrov", "p.petrov@itsm.local", "Пётр Петров", "IT", "pass123");
        var sidorov = NewUser("s.sidorov", "s.sidorov@itsm.local", "Сидор Сидоров", "Бухгалтерия", "pass123");
        var anna = NewUser("a.anna", "a.anna@itsm.local", "Анна Аннова", "Бухгалтерия", "pass123");
        var maria = NewUser("m.maria", "m.maria@itsm.local", "Мария Марьина", "Отдел продаж", "pass123");

        _db.Users.AddRange(admin, ivanov, petrov, sidorov, anna, maria);
        await _db.SaveChangesAsync(ct);

        // Иерархия: Петров → Иванов → Админ
        petrov.ManagerId = ivanov.Id;
        ivanov.ManagerId = admin.Id;
        await _db.SaveChangesAsync(ct);

        // Роли
        _db.UserRoleMappings.AddRange(
            new UserRoleMapping { UserId = admin.Id, RoleId = adminRoleId },
            new UserRoleMapping { UserId = ivanov.Id, RoleId = managerRoleId },
            new UserRoleMapping { UserId = petrov.Id, RoleId = agentRoleId },
            new UserRoleMapping { UserId = sidorov.Id, RoleId = portalRoleId },
            new UserRoleMapping { UserId = anna.Id, RoleId = portalRoleId },
            new UserRoleMapping { UserId = maria.Id, RoleId = portalRoleId }
        );
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("DatabaseSeeder: создано 6 пользователей");
    }

    private User NewUser(string username, string email, string fullName, string department, string password) => new()
    {
        Username = username,
        Email = email,
        FullName = fullName,
        Department = department,
        Status = UserStatuses.Verified, // тестовые — уже подтверждённые
        PasswordHash = _passwordHasher.Hash(password),
        IsActive = true,
        TenantId = 1,
        CreatedAt = DateTimeOffset.UtcNow
    };
}