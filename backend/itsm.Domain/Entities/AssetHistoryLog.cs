namespace itsm.Domain.Entities;

public class AssetHistoryLog
{
    public int Id { get; set; }
    public int AssetId { get; set; }

    /// <summary>
    /// Пользователь, выполнивший действие. Для системных действий — null.
    /// </summary>
    public int? UserId { get; set; }

    /// <summary>
    /// Тип действия (АКТ-15).
    /// Допустимые значения — см. AssetHistoryActionTypes.
    /// </summary>
    public string ActionType { get; set; } = string.Empty;

    /// <summary>
    /// Детали изменения в формате JSON (jsonb в PostgreSQL).
    /// Пример: {"field":"name","old":"Старое","new":"Новое"}
    /// Для movement: {"movementType":"transfer","fromUserId":1,"toUserId":2}
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// Ссылка на тикет (для ticket_created / ticket_closed).
    /// </summary>
    public int? TicketId { get; set; }

    /// <summary>
    /// Legacy-поля: оставлены для совместимости со старыми записями.
    /// Новый код должен использовать ActionType + Details.
    /// </summary>
    public string? OldStatus { get; set; }
    public string? NewStatus { get; set; }
    public int? OldAssignedUserId { get; set; }
    public int? NewAssignedUserId { get; set; }

    public string? Comment { get; set; }
    public DateTimeOffset ChangedAt { get; set; } = DateTimeOffset.UtcNow;

    public Asset Asset { get; set; } = null!;
    public User? User { get; set; }
    public User? OldAssignedUser { get; set; }
    public User? NewAssignedUser { get; set; }
}

/// <summary>
/// Константы типов действий для истории по активу (АКТ-15).
/// </summary>
public static class AssetHistoryActionTypes
{
    public const string EntityCreated = "entity_created";
    public const string AttributeChanged = "attribute_changed";
    public const string Movement = "movement";
    public const string StageChanged = "stage_changed";
    public const string AssignedChanged = "assigned_changed";
    public const string TicketCreated = "ticket_created";
    public const string TicketClosed = "ticket_closed";
}