using itsm.Application.Common.Interfaces;
using itsm.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace itsm.WebApi.Controllers;

[ApiController]
[Route("api/service-desk-settings")]
public sealed class ServiceDeskSettingsController(IServiceDeskSettingsService service) : ControllerBase
{
    private bool CanManageAdminCreation => User.IsInRole("tenant_owner");

    [HttpGet]
    [Authorize(Roles = "admin,tenant_owner,manager,agent")]
    [EndpointSummary("Получить все настройки Service Desk")]
    [ProducesResponseType(typeof(ServiceDeskSettingsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ServiceDeskSettingsDto>> Get(CancellationToken ct)
        => Ok(await service.GetAsync(CanManageAdminCreation, ct));

    [HttpPut]
    [Authorize(Roles = "admin,tenant_owner")]
    [EndpointSummary("Сохранить настройки Service Desk")]
    [ProducesResponseType(typeof(ServiceDeskSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ServiceDeskSettingsDto>> Update(
        [FromBody] UpdateServiceDeskSettingsRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        try
        {
            return Ok(await service.UpdateAsync(request, CanManageAdminCreation, ct));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new ProblemDetails { Title = "Недостаточно прав", Detail = ex.Message, Status = 403 });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails { Title = "Не найдено", Detail = ex.Message, Status = 404 });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "Изменение невозможно", Detail = ex.Message, Status = 409 });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Некорректные настройки", Detail = ex.Message, Status = 400 });
        }
    }
}