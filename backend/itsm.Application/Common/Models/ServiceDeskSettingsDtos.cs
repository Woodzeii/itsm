using System.ComponentModel.DataAnnotations;

namespace itsm.Application.Common.Models;

public sealed class PasswordPolicyDto
{
    public int MinLength { get; set; } = 8;
    public bool RequireDigit { get; set; } = true;
    public bool RequireUppercase { get; set; }
    public bool RequireLowercase { get; set; } = true;
    public bool RequireSpecial { get; set; }
    public int? ExpirationDays { get; set; }
    public int MaxFailedAttempts { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class UpdatePasswordPolicyRequest
{
    [Range(8, 128, ErrorMessage = "Минимальная длина пароля не может быть меньше 8.")]
    public int MinLength { get; set; } = 8;

    public bool RequireDigit { get; set; } = true;
    public bool RequireUppercase { get; set; }
    public bool RequireLowercase { get; set; } = true;
    public bool RequireSpecial { get; set; }

    [Range(1, 3650, ErrorMessage = "Срок действия пароля должен быть положительным.")]
    public int? ExpirationDays { get; set; }

    [Range(1, 100, ErrorMessage = "Число неудачных попыток должно быть положительным.")]
    public int MaxFailedAttempts { get; set; } = 5;

    [Range(1, 1440, ErrorMessage = "Время блокировки должно быть положительным.")]
    public int LockoutMinutes { get; set; } = 15;
}

public sealed class CriticalityLevelDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public int AssignedTicketCount { get; set; }
    public bool CanDelete => AssignedTicketCount == 0;
}

public sealed class CreateCriticalityLevelRequest
{
    [Required]
    [StringLength(64)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(128)]
    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }
}

public sealed class UpdateCriticalityLevelRequest
{
    [Required]
    [StringLength(128)]
    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class SlaValueDto
{
    public int TicketTypeId { get; set; }
    public int CriticalityLevelId { get; set; }
    public int? ReactionTimeMinutes { get; set; }
    public int? ResolutionTimeMinutes { get; set; }
    public bool IsDisabled { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class SaveSlaValueRequest
{
    [Range(1, int.MaxValue)]
    public int TicketTypeId { get; set; }

    [Range(1, int.MaxValue)]
    public int CriticalityLevelId { get; set; }

    [Range(0, int.MaxValue)]
    public int? ReactionTimeMinutes { get; set; }

    [Range(0, int.MaxValue)]
    public int? ResolutionTimeMinutes { get; set; }

    public bool IsDisabled { get; set; }
}

public sealed class TicketTypeSlaValueItem
{
    public int CriticalityLevelId { get; set; }
    public string? CriticalityName { get; set; }

    [Range(0, int.MaxValue)]
    public int? ReactionTimeMinutes { get; set; }

    [Range(0, int.MaxValue)]
    public int? ResolutionTimeMinutes { get; set; }

    public bool IsDisabled { get; set; }
}

public sealed class TicketTypeDto
{
    public int Id { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsBuiltIn { get; set; }
    public bool IsPortalAvailable { get; set; }
    public bool IsSlaEnabled { get; set; }
    public int? AssignmentGroupId { get; set; }
    public int ActiveTicketCount { get; set; }
    public IReadOnlyList<SlaValueDto> SlaValues { get; set; } = [];
}

public sealed class CreateTicketTypeRequest
{
    [Required]
    [StringLength(64)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(128)]
    public string Name { get; set; } = string.Empty;

    public bool IsPortalAvailable { get; set; } = true;
    public bool IsSlaEnabled { get; set; } = true;
}

public sealed class UpdateTicketTypeRequest
{
    [Required]
    [StringLength(128)]
    public string Name { get; set; } = string.Empty;

    public bool IsPortalAvailable { get; set; } = true;
    public bool? IsSlaEnabled { get; set; }
}

public sealed class SlaSettingsDto
{
    public bool IsEnabled { get; set; }
    public int? ActiveWorkingScheduleId { get; set; }
}

public sealed class PortalSlaVisibilityDto
{
    public bool ShowReactionTime { get; set; }
    public bool ShowResolutionTime { get; set; }
}

public sealed class TicketTypeSlaSettingRequest
{
    public bool IsEnabled { get; set; }
}

public sealed class TicketTypeSlaValuesRequest
{
    public IReadOnlyList<TicketTypeSlaValueItem> Items { get; set; } = [];
}

public sealed class CriticalitySettingsItem
{
    public int Id { get; set; }

    [StringLength(64)]
    public string? Code { get; set; }

    [Required]
    [StringLength(128)]
    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class TicketTypeSettingsItem
{
    public int Id { get; set; }

    [StringLength(64)]
    public string? Code { get; set; }

    [Required]
    [StringLength(128)]
    public string Name { get; set; } = string.Empty;

    public bool IsPortalAvailable { get; set; } = true;
    public bool IsSlaEnabled { get; set; } = true;
    public IReadOnlyList<TicketTypeSlaValueItem>? SlaValues { get; set; }
}

public sealed class ServiceDeskSettingsDto
{
    public bool AllowAdminsCreateAdmins { get; set; }
    public bool CanManageAdminCreation { get; set; }
    public bool SlaEnabled { get; set; }
    public string AssignmentMode { get; set; } = "auto";
    public int? ManualAssignerUserId { get; set; }
    public string? ManualAssignerName { get; set; }
    public PortalSlaVisibilityDto PortalSlaFields { get; set; } = new();
    public PasswordPolicyDto PasswordPolicy { get; set; } = new();
    public int MaxCriticalityLevels { get; set; } = 5;
    public int MinCriticalityLevels { get; set; } = 3;
    public IReadOnlyList<CriticalityLevelDto> Criticalities { get; set; } = [];
    public IReadOnlyList<TicketTypeDto> TicketTypes { get; set; } = [];
    public IReadOnlyList<string> PortalAccounts { get; set; } = [];
}

public sealed class UpdateServiceDeskSettingsRequest
{
    public bool? AllowAdminsCreateAdmins { get; set; }
    public bool? SlaEnabled { get; set; }
    public string? AssignmentMode { get; set; }
    public int? ManualAssignerUserId { get; set; }
    public PortalSlaVisibilityDto? PortalSlaFields { get; set; }
    public UpdatePasswordPolicyRequest? PasswordPolicy { get; set; }
    public IReadOnlyList<CriticalitySettingsItem>? Criticalities { get; set; }
    public IReadOnlyList<TicketTypeSettingsItem>? TicketTypes { get; set; }
}