using FluentValidation;
using itsm.Application.Common.Interfaces;
using itsm.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace itsm.WebApi.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = "admin,tenant_admin")]
public class UsersController : ControllerBase
{
    private readonly IUserAdminService _service;

    public UsersController(IUserAdminService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] UserListQuery query,
        [FromServices] IValidator<UserListQuery> validator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(query, ct);
        if (!validation.IsValid) return ValidationError(validation);
        return Ok(await _service.GetListAsync(query, ct));
    }

    [HttpGet("roles")]
    public async Task<IActionResult> GetRoles(CancellationToken ct)
        => Ok(await _service.GetRolesAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var result = await _service.GetByIdAsync(id, ct);
        return result is null ? NotFound(new { message = "Пользователь не найден" }) : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateUserRequest request,
        [FromServices] IValidator<CreateUserRequest> validator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid) return ValidationError(validation);

        return await Run(async () =>
        {
            var created = await _service.CreateAsync(request, ActorId, Ip, ct);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateUserRequest request,
        [FromServices] IValidator<UpdateUserRequest> validator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid) return ValidationError(validation);

        return await Run(async () => Ok(await _service.UpdateAsync(id, request, ActorId, Ip, ct)));
    }

    [HttpDelete("{id:int}")]
    public Task<IActionResult> Delete(int id, CancellationToken ct)
        => Run(async () =>
        {
            await _service.DeleteAsync(id, ActorId, Ip, ct);
            return NoContent();
        });

    [HttpPut("{id:int}/active")]
    public Task<IActionResult> SetActive(int id, [FromBody] SetUserActiveRequest request, CancellationToken ct)
        => Run(async () => Ok(await _service.SetActiveAsync(id, request.IsActive, ActorId, Ip, ct)));

    [HttpPut("{id:int}/roles")]
    public async Task<IActionResult> SetRoles(
        int id,
        [FromBody] SetUserRolesRequest request,
        [FromServices] IValidator<SetUserRolesRequest> validator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid) return ValidationError(validation);

        return await Run(async () => Ok(await _service.SetRolesAsync(id, request, ActorId, Ip, ct)));
    }

    [HttpPost("{id:int}/mfa/reset")]
    public Task<IActionResult> ResetMfa(int id, CancellationToken ct)
        => Run(async () => Ok(await _service.ResetMfaAsync(id, ActorId, Ip, ct)));

    [HttpPost("{id:int}/unlock")]
    public Task<IActionResult> Unlock(int id, CancellationToken ct)
        => Run(async () => Ok(await _service.UnlockAsync(id, ActorId, Ip, ct)));

    private int ActorId => int.TryParse(User.FindFirst("Id")?.Value, out var id) ? id : 0;
    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();

    private IActionResult ValidationError(FluentValidation.Results.ValidationResult validation)
        => BadRequest(new { message = "Ошибка валидации", errors = validation.ToDictionary() });

    private async Task<IActionResult> Run(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (UserAdminException ex)
        {
            var body = new { message = ex.Message };
            return ex.Kind switch
            {
                UserAdminErrorKind.NotFound => NotFound(body),
                UserAdminErrorKind.Conflict => Conflict(body),
                _ => BadRequest(body)
            };
        }
    }
}
