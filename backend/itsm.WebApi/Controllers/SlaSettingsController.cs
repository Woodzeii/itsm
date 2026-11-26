using itsm.Application.Common.Interfaces;
using itsm.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace itsm.WebApi.Controllers;

[ApiController]
[Route("api/sla-settings")]
[Authorize(Roles = "admin,tenant_owner")]
public sealed class SlaSettingsController(IServiceDeskSettingsService service) : ControllerBase
{
    [HttpGet]
    [EndpointSummary("Получить общие настройки SLA")]
    [ProducesResponseType(typeof(SlaSettingsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SlaSettingsDto>> Get(CancellationToken ct)
        => Ok(await service.GetSlaSettingsAsync(ct));

    [HttpPut]
    [EndpointSummary("Сохранить общие настройки SLA")]
    [ProducesResponseType(typeof(SlaSettingsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SlaSettingsDto>> Update([FromBody] SlaSettingsDto request, CancellationToken ct)
        => Ok(await service.UpdateSlaSettingsAsync(request, ct));

    [HttpGet("portal-visibility")]
    [EndpointSummary("Получить видимость SLA на портале")]
    [ProducesResponseType(typeof(PortalSlaVisibilityDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PortalSlaVisibilityDto>> GetPortalVisibility(CancellationToken ct)
        => Ok(await service.GetPortalSlaVisibilityAsync(ct));

    [HttpPut("portal-visibility")]
    [EndpointSummary("Сохранить видимость SLA на портале")]
    [ProducesResponseType(typeof(PortalSlaVisibilityDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PortalSlaVisibilityDto>> UpdatePortalVisibility(
        [FromBody] PortalSlaVisibilityDto request,
        CancellationToken ct)
        => Ok(await service.UpdatePortalSlaVisibilityAsync(request, ct));

    [HttpPut("ticket-types/{ticketTypeId:int}")]
    [EndpointSummary("Включить или выключить SLA для типа заявки")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetTicketTypeSla(
        int ticketTypeId,
        [FromBody] TicketTypeSlaSettingRequest request,
        CancellationToken ct)
    {
        try
        {
            await service.SetTicketTypeSlaEnabledAsync(ticketTypeId, request.IsEnabled, ct);
            return Ok();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails { Title = "Тип заявки не найден", Detail = ex.Message, Status = 404 });
        }
    }

    [HttpGet("ticket-types/{ticketTypeId:int}/values")]
    [EndpointSummary("Получить нормативы SLA типа заявки по критичности")]
    [ProducesResponseType(typeof(IReadOnlyList<SlaValueDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SlaValueDto>>> GetTicketTypeValues(int ticketTypeId, CancellationToken ct)
    {
        try
        {
            return Ok(await service.GetSlaValuesAsync(ticketTypeId, ct));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails { Title = "Тип заявки не найден", Detail = ex.Message, Status = 404 });
        }
    }

    [HttpPut("ticket-types/{ticketTypeId:int}/values")]
    [EndpointSummary("Сохранить нормативы SLA типа заявки по критичности")]
    [ProducesResponseType(typeof(IReadOnlyList<SlaValueDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<SlaValueDto>>> SaveTicketTypeValues(
        int ticketTypeId,
        [FromBody] TicketTypeSlaValuesRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        try
        {
            return Ok(await service.SaveTicketTypeSlaValuesAsync(ticketTypeId, request.Items, ct));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails { Title = "Не найдено", Detail = ex.Message, Status = 404 });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Некорректные нормативы SLA", Detail = ex.Message, Status = 400 });
        }
    }
}

[ApiController]
[Route("api/sla-policies")]
[Authorize(Roles = "admin,tenant_owner")]
public sealed class SlaPoliciesController(IServiceDeskSettingsService service) : ControllerBase
{
    [HttpGet]
    [EndpointSummary("Получить нормативы SLA по типам заявок и критичности")]
    [ProducesResponseType(typeof(IReadOnlyList<SlaValueDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SlaValueDto>>> GetAll(CancellationToken ct)
        => Ok(await service.GetSlaValuesAsync(null, ct));

    [HttpPut]
    [EndpointSummary("Сохранить норматив SLA для пары тип/критичность")]
    [ProducesResponseType(typeof(SlaValueDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SlaValueDto>> Upsert([FromBody] SaveSlaValueRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        try
        {
            return Ok(await service.UpsertSlaValueAsync(request, ct));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails { Title = "Не найдено", Detail = ex.Message, Status = 404 });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Некорректный норматив SLA", Detail = ex.Message, Status = 400 });
        }
    }
}