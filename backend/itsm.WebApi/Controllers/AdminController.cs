using itsm.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace itsm.WebApi.Controllers;

/// <summary>
/// ⚠️ ВРЕМЕННЫЙ КОНТРОЛЛЕР. Возвращает хардкод-данные для фронта,
/// пока реальный backend не реализован.
/// После реализации CRUD — заменить на сервисы.
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize]
public class AdminController : ControllerBase
{
    // ==================== Audit Log ====================
    // ⚠️ НЕ РЕАЛИЗОВАНО В BACKEND (хардкод)
    [HttpGet("audit-log")]
    public IActionResult GetAuditLog()
    {
        var items = new List<AuditLogEntryDto>
        {
            new()
            {
                Id = 1,
                UserId = 1,
                UserFullName = "Администратор",
                EntityType = "Asset",
                EntityId = 1,
                Action = "created",
                OldValues = null,
                NewValues = "{\"name\":\"Ноутбук Lenovo ThinkPad\"}",
                IpAddress = "127.0.0.1",
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-2)
            },
            new()
            {
                Id = 2,
                UserId = 1,
                UserFullName = "Администратор",
                EntityType = "Ticket",
                EntityId = 5,
                Action = "status_changed",
                OldValues = "{\"status\":\"open\"}",
                NewValues = "{\"status\":\"in_progress\"}",
                IpAddress = "127.0.0.1",
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-5)
            },
            new()
            {
                Id = 3,
                UserId = 2,
                UserFullName = "Иван Иванов",
                EntityType = "User",
                EntityId = 2,
                Action = "updated",
                OldValues = "{\"department\":\"Sales\"}",
                NewValues = "{\"department\":\"IT\"}",
                IpAddress = "10.0.0.15",
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-1)
            }
        };
        return Ok(items);
    }

    // ==================== Users ====================
    // ⚠️ НЕ РЕАЛИЗОВАНО В BACKEND (хардкод)
    [HttpGet("users")]
    public IActionResult GetUsers()
    {
        var users = new List<UserSummaryDto>
        {
            new() { Id = 1, Username = "admin", Email = "admin@itsm.local", FullName = "Администратор Системы", Department = "IT", ManagerId = null, RoleIds = new List<int> { 1 }, IsActive = true },
            new() { Id = 2, Username = "i.ivanov", Email = "i.ivanov@itsm.local", FullName = "Иван Иванов", Department = "IT", ManagerId = 1, RoleIds = new List<int> { 4 }, IsActive = true },
            new() { Id = 3, Username = "p.petrov", Email = "p.petrov@itsm.local", FullName = "Пётр Петров", Department = "IT", ManagerId = 2, RoleIds = new List<int> { 3 }, IsActive = true },
            new() { Id = 4, Username = "s.sidorov", Email = "s.sidorov@itsm.local", FullName = "Сидор Сидоров", Department = "Бухгалтерия", ManagerId = null, RoleIds = new List<int> { 5 }, IsActive = true },
            new() { Id = 5, Username = "a.anna", Email = "a.anna@itsm.local", FullName = "Анна Аннова", Department = "Бухгалтерия", ManagerId = null, RoleIds = new List<int> { 5 }, IsActive = false }
        };
        return Ok(users);
    }

    // ==================== Roles ====================
    // ⚠️ НЕ РЕАЛИЗОВАНО В BACKEND (хардкод)
    [HttpGet("roles")]
    public IActionResult GetRoles()
    {
        var roles = new List<RoleSummaryDto>
        {
            new() { Id = 1, Code = "tenant_admin", Name = "Администратор тенантов" },
            new() { Id = 2, Code = "admin", Name = "Администратор" },
            new() { Id = 3, Code = "agent", Name = "Инженер ТП" },
            new() { Id = 4, Code = "manager", Name = "Руководитель" },
            new() { Id = 5, Code = "portal_user", Name = "Пользователь портала" }
        };
        return Ok(roles);
    }
}