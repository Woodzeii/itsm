using itsm.Application.Common.Models;
using itsm.Infrastructure.Persistence.Services;
using FluentValidation;
using itsm.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace itsm.WebApi.Controllers;

/// <summary>Управление тенантами (пространствами), лимитами и блокировками.</summary>
[ApiController]
[Route("api/tenants")]
[Authorize]
public class TenantsController : ControllerBase
{
    private readonly ITenantService _tenantService;
    private readonly ITenantContext _tenantContext;

    public TenantsController(ITenantService tenantService, ITenantContext tenantContext)
    {
        _tenantService = tenantService;
        _tenantContext = tenantContext;
    }

    /// <summary>Получить квоту активных пользователей текущего тенанта.</summary>
    [HttpGet("me/user-quota")]
    [Authorize(Roles = "admin,tenant_owner")]
    [ProducesResponseType(typeof(TenantUserQuotaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCurrentUserQuota(CancellationToken ct)
    {
        var quota = await _tenantService.GetUserQuotaAsync(_tenantContext.GetCurrentTenantId(), ct);
        return quota is null ? NotFound(new { message = "Тенант не найден" }) : Ok(quota);
    }

    /// <summary>Получить список всех тенантов с информацией о лимитах и использовании.</summary>
    [HttpGet]
    [Authorize(Roles = "tenant_admin")]
    public async Task<IActionResult> GetAllTenants(CancellationToken ct)
    {
        try
        {
            var tenants = await _tenantService.GetAllAsync(ct);
            return Ok(tenants);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Получить информацию о конкретном тенанте.</summary>
    [HttpGet("{id:int}")]
    [Authorize(Roles = "tenant_admin")]
    public async Task<IActionResult> GetTenant(int id, CancellationToken ct)
    {
        var tenant = await _tenantService.GetByIdAsync(id, ct);
        if (tenant is null)
            return NotFound(new { message = "Тенант не найден" });
        return Ok(tenant);
    }

    /// <summary>Создать новый тенант с указанными лимитами.</summary>
    [HttpPost]
    [Authorize(Roles = "tenant_admin")]
    public async Task<IActionResult> CreateTenant(
        [FromBody] CreateTenantRequest request,
        [FromServices] IValidator<CreateTenantRequest> validator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(new { message = "Ошибка валидации", errors = validation.ToDictionary() });

        try
        {
            var tenant = await _tenantService.CreateAsync(request, ct);
            return CreatedAtAction(nameof(GetTenant), new { id = tenant.Id }, tenant);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    /// <summary>Обновить параметры тенанта (лимиты, название).</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = "tenant_admin")]
    public async Task<IActionResult> UpdateTenant(int id, [FromBody] UpdateTenantRequest request, CancellationToken ct)
    {
        try
        {
            var tenant = await _tenantService.UpdateAsync(id, request, ct);
            if (tenant is null)
                return NotFound(new { message = "Тенант не найден" });
            return Ok(tenant);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Получить настройки текущего тенанта.</summary>
    [HttpGet("settings")]
    [Authorize(Roles = "admin,tenant_owner,manager,agent,portal_user")]
    public async Task<IActionResult> GetTenantSettings(CancellationToken ct)
    {
        var tenantId = _tenantContext.GetCurrentTenantId();
        if (tenantId <= 0)
            return BadRequest(new { message = "Текущий тенант не определён" });

        var settings = await _tenantService.GetSettingsAsync(tenantId, ct);
        return settings is null ? NotFound(new { message = "Тенант не найден" }) : Ok(settings);
    }

    /// <summary>Изменить настройки текущего тенанта.</summary>
    [HttpPut("settings")]
    [Authorize(Roles = "tenant_owner")]
    public async Task<IActionResult> UpdateTenantSettings([FromBody] UpdateTenantSettingsRequest request, CancellationToken ct)
    {
        var tenantId = _tenantContext.GetCurrentTenantId();
        if (tenantId <= 0)
            return BadRequest(new { message = "Текущий тенант не определён" });

        var settings = await _tenantService.UpdateSettingsAsync(tenantId, request, ct);
        return settings is null ? NotFound(new { message = "Тенант не найден" }) : Ok(settings);
    }

    /// <summary>Получить настройки указанного тенанта (глобальный администратор).</summary>
    [HttpGet("{id:int}/settings")]
    [Authorize(Roles = "tenant_admin")]
    public async Task<IActionResult> GetTenantSettings(int id, CancellationToken ct)
    {
        var settings = await _tenantService.GetSettingsAsync(id, ct);
        return settings is null ? NotFound(new { message = "Тенант не найден" }) : Ok(settings);
    }

    /// <summary>Изменить настройки указанного тенанта (глобальный администратор).</summary>
    [HttpPut("{id:int}/settings")]
    [Authorize(Roles = "tenant_admin")]
    public async Task<IActionResult> UpdateTenantSettings(int id, [FromBody] UpdateTenantSettingsRequest request, CancellationToken ct)
    {
        var settings = await _tenantService.UpdateSettingsAsync(id, request, ct);
        return settings is null ? NotFound(new { message = "Тенант не найден" }) : Ok(settings);
    }

    /// <summary>Изменить статус активности тенанта (активен/неактивен).</summary>
    [HttpPatch("{id:int}/status")]
    [Authorize(Roles = "tenant_admin")]
    public async Task<IActionResult> UpdateTenantStatus(int id, [FromBody] UpdateTenantStatusRequest request, CancellationToken ct)
    {
        var tenant = await _tenantService.UpdateStatusAsync(id, request.IsActive, ct);
        if (tenant is null)
            return NotFound(new { message = "Тенант не найден" });
        return Ok(tenant);
    }

    /// <summary>Заблокировать тенант (запретить доступ пользователям).</summary>
    [HttpPost("{id:int}/block")]
    [Authorize(Roles = "tenant_admin")]
    public async Task<IActionResult> BlockTenant(int id, [FromBody] BlockTenantRequest request, CancellationToken ct)
    {
        try
        {
            var tenant = await _tenantService.BlockAsync(id, request, ct);
            if (tenant is null)
                return NotFound(new { message = "Тенант не найден" });
            return Ok(tenant);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Разблокировать тенант (восстановить доступ пользователям).</summary>
    [HttpPost("{id:int}/unblock")]
    [Authorize(Roles = "tenant_admin")]
    public async Task<IActionResult> UnblockTenant(int id, CancellationToken ct)
    {
        try
        {
            var tenant = await _tenantService.UnblockAsync(id, ct);
            if (tenant is null)
                return NotFound(new { message = "Тенант не найден" });
            return Ok(tenant);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Удалить тенант (только если в нём нет пользователей).</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "tenant_admin")]
    public async Task<IActionResult> DeleteTenant(int id, CancellationToken ct)
    {
        try
        {
            var deleted = await _tenantService.DeleteAsync(id, ct);
            if (!deleted)
                return NotFound(new { message = "Тенант не найден" });
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }
}
