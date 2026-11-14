using FluentValidation;
using itsm.Application.Common.Interfaces;
using itsm.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace itsm.WebApi.Controllers;

[ApiController]
[Route("api/dictionaries")]
[Authorize(Roles = "admin,tenant_owner,agent")]
public class DictionariesController : ControllerBase
{
    private readonly IDictionaryService _service;

    public DictionariesController(IDictionaryService service)
    {
        _service = service;
    }

    // ================= СПРАВОЧНИКИ =================

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool includeArchived = false, CancellationToken ct = default)
        => Ok(await _service.GetAllAsync(includeArchived, ct));

    [HttpGet("{code}")]
    public async Task<IActionResult> GetByCode(string code, [FromQuery] bool includeArchived = false, CancellationToken ct = default)
    {
        var result = await _service.GetByCodeAsync(code, includeArchived, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "admin,tenant_owner")]
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
            return CreatedAtAction(nameof(GetByCode), new { code = created.Code }, created);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPut("{code}")]
    [Authorize(Roles = "admin,tenant_owner")]
    public async Task<IActionResult> Update(
        string code,
        [FromBody] UpdateDictionaryRequest request,
        [FromServices] IValidator<UpdateDictionaryRequest> validator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(new { message = "Ошибка валидации", errors = validation.ToDictionary() });

        var updated = await _service.UpdateAsync(code, request, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{code}")]
    [Authorize(Roles = "admin,tenant_owner")]
    public async Task<IActionResult> Delete(string code, CancellationToken ct)
    {
        try
        {
            var deleted = await _service.DeleteAsync(code, ct);
            return deleted ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("{code}/archive")]
    [Authorize(Roles = "admin,tenant_owner")]
    public async Task<IActionResult> Archive(string code, CancellationToken ct)
    {
        var archived = await _service.ArchiveAsync(code, ct);
        return archived is null ? NotFound() : Ok(archived);
    }

    [HttpPost("{code}/restore")]
    [Authorize(Roles = "admin,tenant_owner")]
    public async Task<IActionResult> Restore(string code, CancellationToken ct)
    {
        var restored = await _service.RestoreAsync(code, ct);
        return restored is null ? NotFound() : Ok(restored);
    }

    // ================= ЗНАЧЕНИЯ =================

    [HttpGet("{code}/values")]
    public async Task<IActionResult> GetValues(string code, [FromQuery] bool includeArchived = false, CancellationToken ct = default)
    {
        var values = await _service.GetValuesAsync(code, includeArchived, ct);
        return values is null ? NotFound() : Ok(values);
    }

    [HttpPost("{code}/values")]
    [Authorize(Roles = "admin,tenant_owner")]
    public async Task<IActionResult> CreateValue(
        string code,
        [FromBody] CreateDictionaryValueRequest request,
        [FromServices] IValidator<CreateDictionaryValueRequest> validator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(new { message = "Ошибка валидации", errors = validation.ToDictionary() });

        try
        {
            var created = await _service.CreateValueAsync(code, request, ct);
            return created is null ? NotFound() : Ok(created);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPut("{code}/values/{valueCode}")]
    [Authorize(Roles = "admin,tenant_owner")]
    public async Task<IActionResult> UpdateValue(
        string code,
        string valueCode,
        [FromBody] UpdateDictionaryValueRequest request,
        [FromServices] IValidator<UpdateDictionaryValueRequest> validator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(new { message = "Ошибка валидации", errors = validation.ToDictionary() });

        var updated = await _service.UpdateValueAsync(code, valueCode, request, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{code}/values/{valueCode}")]
    [Authorize(Roles = "admin,tenant_owner")]
    public async Task<IActionResult> DeleteValue(string code, string valueCode, CancellationToken ct)
    {
        try
        {
            var deleted = await _service.DeleteValueAsync(code, valueCode, ct);
            return deleted ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("{code}/values/{valueCode}/archive")]
    [Authorize(Roles = "admin,tenant_owner")]
    public async Task<IActionResult> ArchiveValue(string code, string valueCode, CancellationToken ct)
    {
        var archived = await _service.ArchiveValueAsync(code, valueCode, ct);
        return archived is null ? NotFound() : Ok(archived);
    }

    [HttpPost("{code}/values/{valueCode}/restore")]
    [Authorize(Roles = "admin,tenant_owner")]
    public async Task<IActionResult> RestoreValue(string code, string valueCode, CancellationToken ct)
    {
        try
        {
            var restored = await _service.RestoreValueAsync(code, valueCode, ct);
            return restored is null ? NotFound() : Ok(restored);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }
}