using itsm.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace itsm.WebApi.Controllers;

[ApiController]
[Route("api/ticket-types")]
[Authorize(Roles = "admin,tenant_owner")]
public sealed class TicketTypesController(ItsmDbContext db) : ControllerBase
{
    [HttpGet]
    [EndpointSummary("Получить типы заявок")]
    [ProducesResponseType(typeof(IReadOnlyList<TicketTypeResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TicketTypeResponse>>> GetAll(CancellationToken ct)
    {
        var types = await db.TicketTypes
            .AsNoTracking()
            .OrderBy(type => type.Id)
            .Select(type => new TicketTypeResponse(
                type.Id,
                type.Code,
                type.Name,
                type.IsBuiltIn,
                type.IsPortalAvailable))
            .ToArrayAsync(ct);

        return Ok(types);
    }

    public sealed record TicketTypeResponse(
        int Id,
        string? Code,
        string Name,
        bool IsBuiltIn,
        bool IsPortalAvailable);
}
