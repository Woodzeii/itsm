using itsm.Application.Common.Interfaces;
using itsm.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace itsm.WebApi.Controllers;

[ApiController]
[Route("api/escalations")]
[Authorize(Roles = "admin,tenant_owner,agent")]
public sealed class EscalationsController(ITicketService service) : ControllerBase
{
    [HttpGet]
    [EndpointSummary("Получить очередь эскалированных заявок, ожидающих принятия")]
    [ProducesResponseType(typeof(IReadOnlyList<PendingEscalationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PendingEscalationResponse>>> GetPending(CancellationToken ct)
        => Ok(await service.GetPendingEscalationsAsync(ct) ?? []);
}
