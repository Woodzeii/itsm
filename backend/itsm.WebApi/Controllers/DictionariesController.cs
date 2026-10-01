using FluentValidation;
using itsm.Application.Common.Interfaces;
using itsm.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace itsm.WebApi.Controllers;

[ApiController]
[Route("api/dictionaries")]
[Authorize(Roles = "admin,agent")]
public class DictionariesController : ControllerBase
{
    private readonly IDictionaryService _service;

    public DictionariesController(IDictionaryService service)
    {
        _service = service;
    }

    // ================= СПРАВОЧНИКИ =================

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
        [FromBody] CreateDictionaryRequest request,
        [FromServices] IValidator<CreateDictionaryRequest> validator,
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
        [FromBody] UpdateDictionaryRequest request,
        [FromServices] IValidator<UpdateDictionaryRequest> validator,
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

    // ================= ЗНАЧЕНИЯ =================

    [HttpGet("{id:int}/values")]
    public async Task<IActionResult> GetValues(int id, [FromQuery] bool includeArchived = false, CancellationToken ct = default)
        => Ok(await _service.GetValuesAsync(id, includeArchived, ct));

    [HttpPost("{id:int}/values")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> CreateValue(
        int id,
        [FromBody] CreateDictionaryValueRequest request,
        [FromServices] IValidator<CreateDictionaryValueRequest> validator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(new { message = "Ошибка валидации", errors = validation.ToDictionary() });

        try
        {
            var created = await _service.CreateValueAsync(id, request, ct);
            return created is null ? NotFound() : Ok(created);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}/values/{valueId:int}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> UpdateValue(
        int id,
        int valueId,
        [FromBody] UpdateDictionaryValueRequest request,
        [FromServices] IValidator<UpdateDictionaryValueRequest> validator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(new { message = "Ошибка валидации", errors = validation.ToDictionary() });

        var updated = await _service.UpdateValueAsync(id, valueId, request, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:int}/values/{valueId:int}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> DeleteValue(int id, int valueId, CancellationToken ct)
    {
        try
        {
            var deleted = await _service.DeleteValueAsync(id, valueId, ct);
            return deleted ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("{id:int}/values/{valueId:int}/archive")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> ArchiveValue(int id, int valueId, CancellationToken ct)
    {
        var archived = await _service.ArchiveValueAsync(id, valueId, ct);
        return archived is null ? NotFound() : Ok(archived);
    }
}