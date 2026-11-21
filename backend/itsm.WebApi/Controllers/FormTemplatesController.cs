using itsm.Application.Common.Interfaces;
using itsm.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace itsm.WebApi.Controllers;

[ApiController]
[Route("api/form-templates")]
[Authorize(Roles = "admin,tenant_owner")]
public sealed class FormTemplatesController(IFormTemplateService service) : ControllerBase
{
    [HttpGet("{ticketTypeId:int}")]
    [EndpointSummary("Получить форму по типу заявки")]
    [ProducesResponseType(typeof(TicketFormTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketFormTemplateResponse>> Get(int ticketTypeId, CancellationToken ct)
    {
        var form = await service.GetFormTemplateAsync(ticketTypeId, ct);
        return form is null ? NotFound() : Ok(form);
    }
}
