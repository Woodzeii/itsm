namespace itsm.Domain.Entities;

/// <summary>
/// Интерфейс для сущностей, которые привязаны к конкретному тенанту.
/// Применяется для автоматического разграничения данных на уровне БД через HasQueryFilter.
/// </summary>
public interface IMultiTenant
{
    int TenantId { get; set; }
}
