using itsm.Application.Common.Interfaces;
using itsm.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace itsm.WebApi.Controllers;

[ApiController]
[Route("api/ticket-types")]
[Authorize(Roles = "admin,tenant_owner")]
public sealed class TicketTypesController(IFormTemplateService service) : ControllerBase
{
    [HttpGet]
    [EndpointSummary("Получить типы заявок")]
    [ProducesResponseType(typeof(IReadOnlyList<TicketTypeOptionResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TicketTypeOptionResponse>>> Get(CancellationToken ct) =>
        Ok(await service.GetTicketTypesAsync(ct));
}
