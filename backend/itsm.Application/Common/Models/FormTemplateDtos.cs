namespace itsm.Application.Common.Models;

public sealed record TicketTypeOptionResponse(
    int Id,
    string? Code,
    string Name,
    bool IsBuiltIn,
    bool IsPortalAvailable);

public sealed record TicketFormTemplateResponse(
    int TicketTypeId,
    string TicketTypeName,
    IReadOnlyList<TicketFormFieldResponse> Fields);

public sealed record TicketFormFieldResponse(
    string Id,
    string Code,
    string Name,
    string Type,
    bool Required,
    string? DefaultValue,
    string? Validation,
    string? Hint,
    bool VisibleToRequester,
    IReadOnlyList<string>? Options,
    bool Immutable,
    int SortOrder,
    string? ConditionFieldId = null,
    string? ConditionValue = null);
