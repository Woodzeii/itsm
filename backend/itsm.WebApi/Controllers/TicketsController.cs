using System.Security.Claims;
using itsm.Application.Common.Interfaces;
using itsm.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace itsm.WebApi.Controllers;

[ApiController]
[Route("api/tickets")]
[Authorize]
public sealed class TicketsController(ITicketService service) : ControllerBase
{
    private const long MaxAttachmentSizeBytes = 10 * 1024 * 1024;

    [HttpGet]
    [Authorize(Roles = "admin,agent")]
    [EndpointSummary("Получить очередь заявок")]
    [ProducesResponseType(typeof(IReadOnlyList<TicketResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TicketResponse>>> GetQueue(CancellationToken ct)
        => Ok(await service.GetQueueAsync(ct));

    [HttpGet("{id:int}")]
    [EndpointSummary("Получить заявку по идентификатору")]
    [ProducesResponseType(typeof(TicketResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketResponse>> GetById(int id, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var ticket = await service.GetByIdAsync(id, userId, IsStaff(), ct);
        return ticket is null ? NotFound() : Ok(ticket);
    }

    [HttpPost]
    [EndpointSummary("Создать заявку")]
    [ProducesResponseType(typeof(TicketResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TicketResponse>> Create(
        [FromBody] CreateTicketRequest request,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        try
        {
            var created = await service.CreateAsync(request, userId, IsStaff(), User.IsInRole("agent"), ct);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Некорректная заявка", Detail = ex.Message, Status = 400 });
        }
        catch (UnauthorizedAccessException ex)
        {
            return ForbidWithProblem(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "Заявку нельзя создать", Detail = ex.Message, Status = 409 });
        }
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "admin,agent")]
    [EndpointSummary("Изменить заявку")]
    [ProducesResponseType(typeof(TicketResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketResponse>> Update(
        int id,
        [FromBody] UpdateTicketRequest request,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        try
        {
            var updated = await service.UpdateAsync(id, request, userId, ct);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Некорректные данные заявки", Detail = ex.Message, Status = 400 });
        }
    }

    [HttpPut("{id:int}/status")]
    [Authorize(Roles = "admin,agent")]
    [EndpointSummary("Изменить статус заявки")]
    [ProducesResponseType(typeof(TicketResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TicketResponse>> ChangeStatus(
        int id,
        [FromBody] ChangeTicketStatusRequest request,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        try
        {
            var updated = await service.ChangeStatusAsync(id, request, userId, UserRoles(), ct);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Некорректный переход статуса", Detail = ex.Message, Status = 400 });
        }
        catch (UnauthorizedAccessException ex)
        {
            return ForbidWithProblem(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "Переход статуса невозможен", Detail = ex.Message, Status = 409 });
        }
    }

    [HttpPost("{id:int}/assign")]
    [Authorize(Roles = "admin,agent")]
    [EndpointSummary("Назначить исполнителя заявки")]
    [ProducesResponseType(typeof(TicketResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketResponse>> Assign(
        int id,
        [FromBody] AssignTicketRequest request,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var actorId))
            return Unauthorized();

        try
        {
            var updated = await service.AssignAsync(id, request, actorId, ct);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Некорректный исполнитель", Detail = ex.Message, Status = 400 });
        }
    }

    [HttpPost("{id:int}/escalate")]
    [EndpointSummary("Запустить ручную эскалацию заявки")]
    [ProducesResponseType(typeof(TicketEscalationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TicketEscalationResponse>> Escalate(int id, CancellationToken ct)
    {
        if (!TryGetUserId(out var actorId))
            return Unauthorized();

        try
        {
            var escalation = await service.EscalateAsync(id, actorId, ct);
            return escalation is null ? NotFound() : Ok(escalation);
        }
        catch (UnauthorizedAccessException ex)
        {
            return ForbidWithProblem(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "Эскалация невозможна", Detail = ex.Message, Status = 409 });
        }
    }

    [HttpGet("{id:int}/messages")]
    [EndpointSummary("Получить сообщения заявки")]
    [ProducesResponseType(typeof(IReadOnlyList<TicketMessageResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<TicketMessageResponse>>> GetMessages(int id, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var messages = await service.GetMessagesAsync(id, userId, IsStaff(), ct);
        return messages is null ? NotFound() : Ok(messages);
    }

    [HttpPost("{id:int}/messages")]
    [EndpointSummary("Добавить сообщение к заявке")]
    [ProducesResponseType(typeof(TicketMessageResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketMessageResponse>> CreateMessage(
        int id,
        [FromBody] CreateTicketMessageRequest request,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var authorId))
            return Unauthorized();

        try
        {
            var message = await service.CreateMessageAsync(id, request, authorId, IsStaff(), ct);
            return message is null
                ? NotFound()
                : CreatedAtAction(nameof(GetMessages), new { id }, message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Некорректное сообщение", Detail = ex.Message, Status = 400 });
        }
        catch (UnauthorizedAccessException ex)
        {
            return ForbidWithProblem(ex.Message);
        }
    }

    [HttpPost("{id:int}/attachments")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxAttachmentSizeBytes + 64 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxAttachmentSizeBytes + 64 * 1024)]
    [EndpointSummary("Загрузить вложение для сообщения заявки")]
    [ProducesResponseType(typeof(TicketAttachmentUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketAttachmentUploadResponse>> UploadAttachment(
        int id,
        [FromForm] IFormFile? file,
        CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();
        if (file is null)
            return BadRequest(new ProblemDetails { Title = "Файл не передан", Status = 400 });
        if (file.Length is <= 0 or > MaxAttachmentSizeBytes)
            return BadRequest(new ProblemDetails
            {
                Title = "Недопустимый размер файла",
                Detail = "Размер вложения должен быть от 1 байта до 10 МБ.",
                Status = 400
            });

        try
        {
            await using var content = file.OpenReadStream();
            var upload = await service.UploadAttachmentAsync(
                id, userId, IsStaff(), file.FileName, content, file.Length, ct);
            return upload is null ? NotFound() : Ok(upload);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Некорректное вложение", Detail = ex.Message, Status = 400 });
        }
    }

    [HttpGet("{id:int}/attachments/{attachmentId:int}")]
    [EndpointSummary("Скачать вложение из сообщения заявки")]
    [Produces("application/octet-stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadAttachment(int id, int attachmentId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var attachment = await service.OpenAttachmentAsync(id, attachmentId, userId, IsStaff(), ct);
        if (attachment is null)
            return NotFound();

        return File(attachment.Content, "application/octet-stream", attachment.FileName);
    }

    [HttpGet("/api/assets/{id:int}/tickets")]
    [Authorize(Roles = "admin,agent")]
    [EndpointSummary("Получить историю заявок по активу")]
    [ProducesResponseType(typeof(IReadOnlyList<AssetTicketHistoryItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<AssetTicketHistoryItem>>> GetAssetTicketHistory(
        int id,
        [FromQuery] int? typeId,
        [FromQuery] int? statusId,
        [FromQuery] int? criticalityLevelId,
        [FromQuery] int? assigneeId,
        [FromQuery] DateOnly? dateFrom,
        [FromQuery] DateOnly? dateTo,
        CancellationToken ct)
    {
        try
        {
            var tickets = await service.GetAssetTicketHistoryAsync(
                id, typeId, statusId, criticalityLevelId, assigneeId, dateFrom, dateTo, ct);
            return tickets is null ? NotFound() : Ok(tickets);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Некорректный период", Detail = ex.Message, Status = 400 });
        }
    }

    private bool TryGetUserId(out int userId) =>
        int.TryParse(User.FindFirstValue("Id") ?? User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    private bool IsStaff() =>
        User.IsInRole("admin") || User.IsInRole("agent");

    private IReadOnlyCollection<string> UserRoles() =>
        User.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray();

    private ObjectResult ForbidWithProblem(string detail) =>
        StatusCode(403, new ProblemDetails { Title = "Недостаточно прав", Detail = detail, Status = 403 });
}
