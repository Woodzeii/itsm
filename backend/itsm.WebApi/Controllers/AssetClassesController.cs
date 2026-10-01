using FluentValidation;
using itsm.Application.Common.Interfaces;
using itsm.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace itsm.WebApi.Controllers;

[ApiController]
[Route("api/asset-classes")]
[Authorize(Roles = "admin,agent")]
public class AssetClassesController : ControllerBase
{
    private readonly IAssetClassService _service;

    public AssetClassesController(IAssetClassService service)
    {
        _service = service;
    }

    // ==================== AssetClass ====================

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await _service.GetAllAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var result = await _service.GetByIdAsync(id, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Create(
        [FromBody] CreateAssetClassRequest request,
        [FromServices] IValidator<CreateAssetClassRequest> validator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(new { message = "Ошибка валидации", errors = validation.ToDictionary() });

        try
        {
            var created = await _service.CreateAsync(request, ct);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateAssetClassRequest request,
        [FromServices] IValidator<UpdateAssetClassRequest> validator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(new { message = "Ошибка валидации", errors = validation.ToDictionary() });

        var updated = await _service.UpdateAsync(id, request, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        try
        {
            var deleted = await _service.DeleteAsync(id, ct);
            return deleted ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("{id:int}/deactivate")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken ct)
    {
        var result = await _service.DeactivateAsync(id, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{id:int}/activate")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Activate(int id, CancellationToken ct)
    {
        var result = await _service.ActivateAsync(id, ct);
        return result is null ? NotFound() : Ok(result);
    }

    // ==================== AssetClassAttribute ====================

    [HttpGet("{id:int}/attributes")]
    public async Task<IActionResult> GetAttributes(int id, CancellationToken ct)
        => Ok(await _service.GetAttributesAsync(id, ct));

    [HttpGet("{id:int}/attributes/{attributeId:int}")]
    public async Task<IActionResult> GetAttribute(int id, int attributeId, CancellationToken ct)
    {
        var result = await _service.GetAttributeAsync(id, attributeId, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{id:int}/attributes")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> CreateAttribute(
        int id,
        [FromBody] CreateAssetClassAttributeRequest request,
        [FromServices] IValidator<CreateAssetClassAttributeRequest> validator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(new { message = "Ошибка валидации", errors = validation.ToDictionary() });

        try
        {
            var created = await _service.CreateAttributeAsync(id, request, ct);
            if (created is null) return NotFound();

            return CreatedAtAction(nameof(GetAttribute),
                new { id, attributeId = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}/attributes/{attributeId:int}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> UpdateAttribute(
        int id,
        int attributeId,
        [FromBody] UpdateAssetClassAttributeRequest request,
        [FromServices] IValidator<UpdateAssetClassAttributeRequest> validator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(new { message = "Ошибка валидации", errors = validation.ToDictionary() });

        var updated = await _service.UpdateAttributeAsync(id, attributeId, request, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:int}/attributes/{attributeId:int}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> DeleteAttribute(int id, int attributeId, CancellationToken ct)
    {
        var deleted = await _service.DeleteAttributeAsync(id, attributeId, ct);
        return deleted ? NoContent() : NotFound();
    }
}