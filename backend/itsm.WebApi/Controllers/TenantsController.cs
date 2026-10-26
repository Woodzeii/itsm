using itsm.Application.Common.Models;
using itsm.Infrastructure.Persistence.Services;
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

    public TenantsController(ITenantService tenantService)
    {
        _tenantService = tenantService;
    }

    /// <summary>Получить список всех тенантов с информацией о лимитах и использовании.</summary>
    [HttpGet]
    [Authorize(Roles = "admin")]
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
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetTenant(int id, CancellationToken ct)
    {
        var tenant = await _tenantService.GetByIdAsync(id, ct);
        if (tenant is null)
            return NotFound(new { message = "Тенант не найден" });
        return Ok(tenant);
    }

    /// <summary>Создать новый тенант с указанными лимитами.</summary>
    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> CreateTenant([FromBody] CreateTenantRequest request, CancellationToken ct)
    {
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
    [Authorize(Roles = "admin")]
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

    /// <summary>Изменить статус активности тенанта (активен/неактивен).</summary>
    [HttpPatch("{id:int}/status")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> UpdateTenantStatus(int id, [FromBody] UpdateTenantStatusRequest request, CancellationToken ct)
    {
        var tenant = await _tenantService.UpdateStatusAsync(id, request.IsActive, ct);
        if (tenant is null)
            return NotFound(new { message = "Тенант не найден" });
        return Ok(tenant);
    }

    /// <summary>Заблокировать тенант (запретить доступ пользователям).</summary>
    [HttpPost("{id:int}/block")]
    [Authorize(Roles = "admin")]
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
    [Authorize(Roles = "admin")]
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
    [Authorize(Roles = "admin")]
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
