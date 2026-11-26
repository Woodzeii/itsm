using itsm.Application.Common.Interfaces;
using itsm.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace itsm.WebApi.Controllers;

[ApiController]
[Route("api/criticality-levels")]
[Authorize(Roles = "admin,tenant_owner")]
public sealed class CriticalityLevelsController(IServiceDeskSettingsService service) : ControllerBase
{
    [HttpGet]
    [EndpointSummary("Получить уровни критичности")]
    [ProducesResponseType(typeof(IReadOnlyList<CriticalityLevelDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CriticalityLevelDto>>> GetAll(CancellationToken ct)
        => Ok(await service.GetCriticalityLevelsAsync(ct));

    [HttpPost]
    [EndpointSummary("Создать уровень критичности")]
    [ProducesResponseType(typeof(CriticalityLevelDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CriticalityLevelDto>> Create(
        [FromBody] CreateCriticalityLevelRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        try
        {
            var created = await service.CreateCriticalityLevelAsync(request, ct);
            return CreatedAtAction(nameof(GetAll), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "Уровень критичности уже существует", Detail = ex.Message, Status = 409 });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Некорректный уровень критичности", Detail = ex.Message, Status = 400 });
        }
    }

    [HttpPut("{id:int}")]
    [EndpointSummary("Обновить уровень критичности")]
    [ProducesResponseType(typeof(CriticalityLevelDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CriticalityLevelDto>> Update(
        int id,
        [FromBody] UpdateCriticalityLevelRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        try
        {
            return Ok(await service.UpdateCriticalityLevelAsync(id, request, ct));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails { Title = "Уровень критичности не найден", Detail = ex.Message, Status = 404 });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "Изменение невозможно", Detail = ex.Message, Status = 409 });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Некорректный уровень критичности", Detail = ex.Message, Status = 400 });
        }
    }

    [HttpDelete("{id:int}")]
    [EndpointSummary("Удалить уровень критичности")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        try
        {
            await service.DeleteCriticalityLevelAsync(id, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails { Title = "Уровень критичности не найден", Detail = ex.Message, Status = 404 });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "Удаление невозможно", Detail = ex.Message, Status = 409 });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Удаление невозможно", Detail = ex.Message, Status = 400 });
        }
    }
}