using itsm.Application.Common.Interfaces;
using itsm.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace itsm.WebApi.Controllers;

[ApiController]
[Route("api/ticket-configuration/service-desk-head")]
[Authorize(Roles = "admin")]
public sealed class TicketConfigurationController(ITicketService service) : ControllerBase
{
    [HttpGet]
    [EndpointSummary("Получить руководителя службы поддержки")]
    [ProducesResponseType(typeof(ServiceDeskHeadSettingResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ServiceDeskHeadSettingResponse>> GetServiceDeskHead(CancellationToken ct)
    {
        var setting = await service.GetServiceDeskHeadAsync(ct);
        return Ok(setting ?? new ServiceDeskHeadSettingResponse(null, null, null));
    }

    [HttpPut]
    [EndpointSummary("Назначить руководителя службы поддержки")]
    [ProducesResponseType(typeof(ServiceDeskHeadSettingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ServiceDeskHeadSettingResponse>> SetServiceDeskHead(
        [FromBody] SetServiceDeskHeadRequest request,
        CancellationToken ct)
    {
        try
        {
            var setting = await service.SetServiceDeskHeadAsync(request, ct);
            return setting is null ? NotFound() : Ok(setting);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Некорректный руководитель", Detail = ex.Message, Status = 400 });
        }
    }
}
