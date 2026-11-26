using itsm.Application.Common.Interfaces;
using itsm.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace itsm.WebApi.Controllers;

[ApiController]
[Route("api/ticket-types")]
public sealed class TicketTypesController(IServiceDeskSettingsService service) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = "admin,tenant_owner,manager,agent")]
    [EndpointSummary("Получить список типов заявок")]
    [ProducesResponseType(typeof(IReadOnlyList<TicketTypeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TicketTypeDto>>> GetAll(CancellationToken ct)
        => Ok(await service.GetTicketTypesAsync(ct));

    [HttpPost]
    [Authorize(Roles = "admin,tenant_owner")]
    [EndpointSummary("Создать тип заявки")]
    [ProducesResponseType(typeof(TicketTypeDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TicketTypeDto>> Create(
        [FromBody] CreateTicketTypeRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        try
        {
            var created = await service.CreateTicketTypeAsync(request, ct);
            return CreatedAtAction(nameof(GetAll), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "Тип заявки уже существует", Detail = ex.Message, Status = 409 });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Некорректные данные типа заявки", Detail = ex.Message, Status = 400 });
        }
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "admin,tenant_owner")]
    [EndpointSummary("Обновить тип заявки")]
    [ProducesResponseType(typeof(TicketTypeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketTypeDto>> Update(
        int id,
        [FromBody] UpdateTicketTypeRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        try
        {
            return Ok(await service.UpdateTicketTypeAsync(id, request, ct));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails { Title = "Тип заявки не найден", Detail = ex.Message, Status = 404 });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "Не удалось обновить тип заявки", Detail = ex.Message, Status = 409 });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Некорректные данные типа заявки", Detail = ex.Message, Status = 400 });
        }
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "admin,tenant_owner")]
    [EndpointSummary("Удалить тип заявки")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        try
        {
            await service.DeleteTicketTypeAsync(id, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails { Title = "Тип заявки не найден", Detail = ex.Message, Status = 404 });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "Удаление невозможно", Detail = ex.Message, Status = 409 });
        }
    }
}