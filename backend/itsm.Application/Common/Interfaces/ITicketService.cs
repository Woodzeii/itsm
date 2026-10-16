using itsm.Application.Common.Models;

namespace itsm.Application.Common.Interfaces;

public interface ITicketService
{
    Task<IReadOnlyList<TicketResponse>> GetQueueAsync(CancellationToken ct = default);
    Task<TicketResponse?> GetByIdAsync(int id, int userId, bool isStaff, CancellationToken ct = default);
    Task<TicketResponse> CreateAsync(CreateTicketRequest request, int creatorId, bool isStaff, bool isEngineer, CancellationToken ct = default);
    Task<TicketResponse?> UpdateAsync(int id, UpdateTicketRequest request, int actorId, CancellationToken ct = default);
    Task<TicketResponse?> ChangeStatusAsync(int id, ChangeTicketStatusRequest request, int actorId, IReadOnlyCollection<string> roleCodes, CancellationToken ct = default);
    Task<TicketResponse?> AssignAsync(int id, AssignTicketRequest request, int actorId, CancellationToken ct = default);
    Task<TicketEscalationResponse?> EscalateAsync(int id, int actorId, CancellationToken ct = default);
    Task<TicketAttachmentUploadResponse?> UploadAttachmentAsync(int ticketId, int userId, bool isStaff, string fileName, Stream content, long sizeBytes, CancellationToken ct = default);
    Task<TicketAttachmentFile?> OpenAttachmentAsync(int ticketId, int attachmentId, int userId, bool isStaff, CancellationToken ct = default);
    Task<IReadOnlyList<TicketMessageResponse>?> GetMessagesAsync(int id, int userId, bool isStaff, CancellationToken ct = default);
    Task<TicketMessageResponse?> CreateMessageAsync(int id, CreateTicketMessageRequest request, int authorId, bool isStaff, CancellationToken ct = default);
    Task<IReadOnlyList<AssetTicketHistoryItem>?> GetAssetTicketHistoryAsync(
        int assetId,
        int? typeId,
        int? statusId,
        int? criticalityLevelId,
        int? assigneeId,
        DateOnly? dateFrom,
        DateOnly? dateTo,
        CancellationToken ct = default);
    Task<ServiceDeskHeadSettingResponse?> GetServiceDeskHeadAsync(CancellationToken ct = default);
    Task<ServiceDeskHeadSettingResponse?> SetServiceDeskHeadAsync(SetServiceDeskHeadRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<EscalationRuleResponse>> GetEscalationRulesAsync(CancellationToken ct = default);
    Task<EscalationRuleResponse?> SetEscalationRuleAsync(int ticketTypeId, UpdateEscalationRuleRequest request, CancellationToken ct = default);
}
