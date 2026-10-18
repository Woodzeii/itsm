using itsm.Application.Common.Interfaces;
using itsm.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace itsm.WebApi.Controllers;

[ApiController]
[Route("api/escalation-rules")]
[Authorize(Roles = "admin")]
public sealed class EscalationRulesController(ITicketService service) : ControllerBase
{
    [HttpGet]
    [EndpointSummary("Получить правила эскалации по типам заявок")]
    [ProducesResponseType(typeof(IReadOnlyList<EscalationRuleResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EscalationRuleResponse>>> GetAll(CancellationToken ct)
        => Ok(await service.GetEscalationRulesAsync(ct));

    [HttpPut("{ticketTypeId:int}")]
    [EndpointSummary("Настроить правило эскалации для типа заявки")]
    [ProducesResponseType(typeof(EscalationRuleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EscalationRuleResponse>> Update(
        int ticketTypeId,
        [FromBody] UpdateEscalationRuleRequest request,
        CancellationToken ct)
    {
        var updated = await service.SetEscalationRuleAsync(ticketTypeId, request, ct);
        return updated is null ? NotFound() : Ok(updated);
    }
}
