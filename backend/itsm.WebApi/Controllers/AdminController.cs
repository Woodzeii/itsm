using itsm.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace itsm.WebApi.Controllers;

/// <summary>Предоставляет временный журнал аудита для административного интерфейса.</summary>
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

}