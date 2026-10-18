using System.Security.Claims;
using FluentValidation;
using itsm.Application.Common.Interfaces;
using itsm.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace itsm.WebApi.Controllers;

[ApiController]
[Route("api/assets")]
[Authorize]
public class AssetsController : ControllerBase
{
    private readonly IAssetService _service;

    public AssetsController(IAssetService service)
    {
        _service = service;
    }

    // ==================== GET ALL ====================

    [HttpGet]
    [ProducesResponseType(typeof(List<AssetDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? classId,
        [FromQuery] string? status,
        CancellationToken ct)
    {
        var assets = await _service.GetAllAsync(classId, status, ct);
        return Ok(assets);
    }

    // ==================== GET BY ID ====================

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(AssetDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var asset = await _service.GetByIdAsync(id, ct);
        return asset is null ? NotFound() : Ok(asset);
    }

    // ==================== CREATE ====================

    [HttpPost]
    [ProducesResponseType(typeof(AssetDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateAssetRequest request, CancellationToken ct)
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
    [ProducesResponseType(typeof(AssetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateAssetRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var updated = await _service.UpdateAsync(id, request, userId.Value, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    // ==================== DELETE ====================

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        try
        {
            var deleted = await _service.DeleteAsync(id, ct);
            return deleted ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    // ==================== MOVEMENTS ====================

    [HttpPost("{id:int}/movements")]
    [ProducesResponseType(typeof(AssetMovementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RegisterMovement(
        int id,
        [FromBody] RegisterAssetMovementRequest request,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        try
        {
            var movement = await _service.RegisterMovementAsync(id, request, userId.Value, ct);
            return movement is null ? NotFound() : Ok(movement);
        }
        catch (NotImplementedException ex)
        {
            return StatusCode(StatusCodes.Status501NotImplemented, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ==================== HISTORY ====================

    [HttpGet("{id:int}/history")]
    [ProducesResponseType(typeof(List<AssetHistoryItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistory(int id, CancellationToken ct)
    {
        var history = await _service.GetHistoryAsync(id, ct);
        return Ok(history);
    }

    // ==================== TICKETS ====================

    [HttpGet("{id:int}/tickets")]
    [ProducesResponseType(typeof(List<AssetTicketHistoryItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTickets(int id, CancellationToken ct)
    {
        var tickets = await _service.GetTicketsAsync(id, ct);
        return Ok(tickets);
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
