namespace itsm.Infrastructure.Persistence;

/// <summary>
/// Сервис для управления текущим тенантом в приложении.
/// Используется для установки TenantId, которое используется в HasQueryFilter.
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// Получает текущий TenantId.
    /// </summary>
    int GetCurrentTenantId();

    /// <summary>
    /// Устанавливает текущий TenantId.
    /// </summary>
    void SetCurrentTenantId(int tenantId);
}

/// <summary>
/// Реализация ITenantContext, использующая AsyncLocal для хранения текущего TenantId.
/// Безопасна для использования в асинхронных операциях.
/// </summary>
public class TenantContext : ITenantContext
{
    private const int DefaultTenantId = 1;
    private static readonly AsyncLocal<int?> _tenantId = new();

    public int GetCurrentTenantId()
    {
        return _tenantId.Value ?? DefaultTenantId;
    }

    public void SetCurrentTenantId(int tenantId)
    {
        _tenantId.Value = tenantId;
    }
}
