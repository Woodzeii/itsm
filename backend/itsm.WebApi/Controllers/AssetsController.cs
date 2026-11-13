using System.Security.Claims;
using FluentValidation;
using itsm.Application.Common.Interfaces;
using itsm.Application.Common.Models;
using itsm.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace itsm.WebApi.Controllers;

[ApiController]
[Route("api/assets")]
[Authorize(Roles = "admin,tenant_owner,agent")]
public sealed class AssetsController(IAssetService service) : ControllerBase
{
    [HttpGet]
    [EndpointSummary("Получить список активов")]
    [ProducesResponseType(typeof(IReadOnlyList<AssetDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AssetDto>>> GetAll(
        [FromQuery] int? assetClassId,
        [FromQuery] string? lifecycleStage,
        [FromQuery] string? search,
        CancellationToken ct)
        => Ok(await service.GetAllAsync(assetClassId, lifecycleStage, search, ct));

    [HttpGet("{id:int}")]
    [EndpointSummary("Получить карточку актива")]
    [ProducesResponseType(typeof(AssetDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AssetDetailDto>> GetById(int id, CancellationToken ct)
    {
        var asset = await service.GetByIdAsync(id, ct);
        return asset is null ? NotFound() : Ok(asset);
    }

    [HttpPost]
    [Authorize(Roles = "admin,tenant_owner,agent")]
    [EndpointSummary("Создать актив")]
    [ProducesResponseType(typeof(AssetDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AssetDto>> Create(
        [FromBody] CreateAssetRequest request,
        [FromServices] IValidator<CreateAssetRequest> validator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ValidationProblem(ToModelState(validation));

        if (!TryGetUserId(out var actorId))
            return Unauthorized();

        try
        {
            var created = await service.CreateAsync(request, actorId, ct);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Некорректный актив", Detail = ex.Message, Status = 400 });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "Актив нельзя создать", Detail = ex.Message, Status = 409 });
        }
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "admin,tenant_owner,agent")]
    [EndpointSummary("Изменить карточку актива")]
    [ProducesResponseType(typeof(AssetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AssetDto>> Update(
        int id,
        [FromBody] UpdateAssetRequest request,
        [FromServices] IValidator<UpdateAssetRequest> validator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ValidationProblem(ToModelState(validation));

        if (!TryGetUserId(out var actorId))
            return Unauthorized();

        try
        {
            var updated = await service.UpdateAsync(id, request, actorId, ct);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Некорректные данные актива", Detail = ex.Message, Status = 400 });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "Актив нельзя изменить", Detail = ex.Message, Status = 409 });
        }
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "admin,tenant_owner")]
    [EndpointSummary("Удалить актив")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        if (!TryGetUserId(out var actorId))
            return Unauthorized();

        try
        {
            var deleted = await service.DeleteAsync(id, actorId, ct);
            return deleted ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "Актив нельзя удалить", Detail = ex.Message, Status = 409 });
        }
    }

    [HttpPost("{id:int}/movements")]
    [Authorize(Roles = "admin,tenant_owner,agent")]
    [EndpointSummary("Зарегистрировать движение актива")]
    [ProducesResponseType(typeof(AssetMovementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AssetMovementDto>> RegisterMovement(
        int id,
        [FromBody] RegisterAssetMovementRequest request,
        [FromServices] IValidator<RegisterAssetMovementRequest> validator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return ValidationProblem(ToModelState(validation));

        if (!TryGetUserId(out var actorId))
            return Unauthorized();

        try
        {
            var movement = await service.RegisterMovementAsync(id, request, actorId, ct);
            return movement is null ? NotFound() : Ok(movement);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Некорректное движение актива", Detail = ex.Message, Status = 400 });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails { Title = "Движение невозможно", Detail = ex.Message, Status = 409 });
        }
    }

    [HttpGet("{id:int}/movements")]
    [EndpointSummary("Получить движения актива")]
    [ProducesResponseType(typeof(IReadOnlyList<AssetMovementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<AssetMovementDto>>> GetMovements(int id, CancellationToken ct)
    {
        var movements = await service.GetMovementsAsync(id, ct);
        return movements is null ? NotFound() : Ok(movements);
    }

    [HttpGet("{id:int}/history")]
    [EndpointSummary("Получить историю актива")]
    [ProducesResponseType(typeof(IReadOnlyList<AssetHistoryItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<AssetHistoryItemDto>>> GetHistory(int id, CancellationToken ct)
    {
        var history = await service.GetHistoryAsync(id, ct);
        return history is null ? NotFound() : Ok(history);
    }

    [HttpGet("{id:int}/tickets")]
    [EndpointSummary("Получить заявки по активу")]
    [ProducesResponseType(typeof(IReadOnlyList<AssetTicketHistoryItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<AssetTicketHistoryItem>>> GetTickets(int id, CancellationToken ct)
    {
        var tickets = await service.GetTicketsAsync(id, ct);
        return tickets is null ? NotFound() : Ok(tickets);
    }

    private bool TryGetUserId(out int userId) =>
        int.TryParse(User.FindFirstValue("Id") ?? User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    private static ModelStateDictionary ToModelState(FluentValidation.Results.ValidationResult validation)
    {
        var modelState = new ModelStateDictionary();
        foreach (var failure in validation.Errors)
            modelState.AddModelError(failure.PropertyName, failure.ErrorMessage);
        return modelState;
    }
}
