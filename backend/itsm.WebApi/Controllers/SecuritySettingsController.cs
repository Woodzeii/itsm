using itsm.Application.Common.Interfaces;
using itsm.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace itsm.WebApi.Controllers;

[ApiController]
[Route("api/security-settings")]
public sealed class SecuritySettingsController(IServiceDeskSettingsService service) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = "admin,tenant_owner")]
    [EndpointSummary("Получить политику паролей")]
    [ProducesResponseType(typeof(PasswordPolicyDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PasswordPolicyDto>> Get(CancellationToken ct)
        => Ok(await service.GetPasswordPolicyAsync(ct));

    [HttpPut]
    [Authorize(Roles = "tenant_owner")]
    [EndpointSummary("Сохранить политику паролей")]
    [ProducesResponseType(typeof(PasswordPolicyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PasswordPolicyDto>> Update(
        [FromBody] UpdatePasswordPolicyRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        try
        {
            return Ok(await service.UpdatePasswordPolicyAsync(request, ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Некорректная политика паролей", Detail = ex.Message, Status = 400 });
        }
    }
}