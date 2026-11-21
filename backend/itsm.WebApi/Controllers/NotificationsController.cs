using System.Security.Claims;
using itsm.Application.Common.Interfaces;
using itsm.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace itsm.WebApi.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public sealed class NotificationsController(INotificationService notificationService) : ControllerBase
{
    [HttpGet]
    [EndpointSummary("Получить уведомления текущего пользователя")]
    [ProducesResponseType(typeof(IReadOnlyList<NotificationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<NotificationResponse>>> Get(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 30,
        CancellationToken ct = default)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        if (skip < 0 || take is < 1 or > 100)
            return BadRequest();

        return Ok(await notificationService.GetAsync(userId, skip, take, ct));
    }

    [HttpPatch("{id:int}/read")]
    [EndpointSummary("Пометить уведомление как прочитанное")]
    [ProducesResponseType(typeof(NotificationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NotificationResponse>> MarkRead(int id, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var notification = await notificationService.MarkReadAsync(id, userId, ct);
        return notification is null ? NotFound() : Ok(notification);
    }

    [HttpPost("read-all")]
    [EndpointSummary("Пометить все уведомления как прочитанные")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var count = await notificationService.MarkAllReadAsync(userId, ct);
        return Ok(new { markedRead = count });
    }

    private bool TryGetUserId(out int userId) =>
        int.TryParse(User.FindFirstValue("Id") ?? User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
