using System.Security.Claims;
using itsm.Application.Common.Interfaces;
using itsm.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace itsm.WebApi.Controllers;

[ApiController]
[Route("api/tickets")]
[Authorize]
public class TicketsController : ControllerBase
{
    private readonly ITicketService _service;

    public TicketsController(ITicketService service)
    {
        _service = service;
    }

    // ==================== GET ALL ====================

    [HttpGet]
    [ProducesResponseType(typeof(List<TicketDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? typeId,
        [FromQuery] int? assigneeId,
        [FromQuery] int? statusId,
        [FromQuery] int? creatorId,
        CancellationToken ct)
    {
        var tickets = await _service.GetAllAsync(typeId, assigneeId, statusId, creatorId, ct);
        return Ok(tickets);
    }

    // ==================== GET BY ID ====================

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(TicketDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var ticket = await _service.GetByIdAsync(id, ct);
        return ticket is null ? NotFound() : Ok(ticket);
    }

    // ==================== CREATE ====================

    [HttpPost]
    [ProducesResponseType(typeof(TicketDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateTicketRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        try
        {
            var created = await _service.CreateAsync(request, userId.Value, ct);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    // ==================== UPDATE ====================

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(TicketDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTicketRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var updated = await _service.UpdateAsync(id, request, userId.Value, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    // ==================== CHANGE STATUS ====================

    [HttpPut("{id:int}/status")]
    [ProducesResponseType(typeof(TicketDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangeStatus(int id, [FromBody] ChangeTicketStatusRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        try
        {
            var updated = await _service.ChangeStatusAsync(id, request, userId.Value, ct);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ==================== ESCALATE ====================

    [HttpPost("{id:int}/escalate")]
    [ProducesResponseType(typeof(TicketEscalationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Escalate(int id, [FromBody] EscalateTicketRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var escalation = await _service.EscalateAsync(id, request, userId.Value, ct);
        return escalation is null ? NotFound() : Ok(escalation);
    }

    // ==================== MESSAGES ====================

    [HttpGet("{id:int}/messages")]
    [ProducesResponseType(typeof(List<TicketMessageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMessages(int id, [FromQuery] bool includeInternal, CancellationToken ct)
    {
        var messages = await _service.GetMessagesAsync(id, includeInternal, ct);
        return Ok(messages);
    }

    [HttpPost("{id:int}/messages")]
    [ProducesResponseType(typeof(TicketMessageDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddMessage(int id, [FromBody] CreateTicketMessageRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var message = await _service.AddMessageAsync(id, request, userId.Value, ct);
        return message is null
            ? NotFound()
            : CreatedAtAction(nameof(GetMessages), new { id }, message);
    }

    // ==================== HELPERS ====================

    private int? GetCurrentUserId()
    {
        var claim = User.FindFirst("Id")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

        return int.TryParse(claim, out var id) ? id : null;
    }
}
