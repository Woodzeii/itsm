using itsm.Application.Common.Interfaces;
using itsm.Application.Common.Models;
using itsm.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace itsm.Infrastructure.Persistence.Services;

public class TicketService : ITicketService
{
    private const string StatusNew = "new";
    private const string StatusClosed = "closed";
    private const string TypeRepair = "repair";

    private readonly ItsmDbContext _db;
    private readonly ITicketHistoryService _history;

    public TicketService(ItsmDbContext db, ITicketHistoryService history)
    {
        _db = db;
        _history = history;
    }

    // ==================== GET ALL ====================

    public async Task<List<TicketDto>> GetAllAsync(
        int? typeId,
        int? assigneeId,
        int? statusId,
        int? creatorId,
        CancellationToken ct = default)
    {
        var query = _db.Tickets
            .Include(t => t.Type)
            .Include(t => t.Status)
            .Include(t => t.CriticalityLevel)
            .Include(t => t.Creator)
            .Include(t => t.Assignee)
            .Include(t => t.Asset)
            .AsQueryable();

        if (typeId.HasValue)
            query = query.Where(t => t.TypeId == typeId.Value);
        if (assigneeId.HasValue)
            query = query.Where(t => t.AssigneeId == assigneeId.Value);
        if (statusId.HasValue)
            query = query.Where(t => t.StatusId == statusId.Value);
        if (creatorId.HasValue)
            query = query.Where(t => t.CreatorId == creatorId.Value);

        var tickets = await query
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

        var ids = tickets.Select(t => t.Id).ToList();
        var messageCounts = await _db.TicketMessages
            .Where(m => ids.Contains(m.TicketId))
            .GroupBy(m => m.TicketId)
            .Select(g => new { TicketId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TicketId, x => x.Count, ct);

        return tickets.Select(t => MapToDto(t, messageCounts.GetValueOrDefault(t.Id, 0))).ToList();
    }

    // ==================== GET BY ID ====================

    public async Task<TicketDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var ticket = await _db.Tickets
            .Include(t => t.Type)
            .Include(t => t.Status)
            .Include(t => t.CriticalityLevel)
            .Include(t => t.Creator)
            .Include(t => t.Assignee)
            .Include(t => t.Asset)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        if (ticket is null) return null;

        var messageCount = await _db.TicketMessages
            .CountAsync(m => m.TicketId == id, ct);

        return MapToDto(ticket, messageCount);
    }

    // ==================== CREATE ====================

    public async Task<TicketDto> CreateAsync(CreateTicketRequest request, int creatorId, CancellationToken ct = default)
    {
        var ticketType = await _db.TicketTypes.FirstOrDefaultAsync(t => t.Id == request.TypeId, ct);
        if (ticketType is null)
            throw new InvalidOperationException($"Тип заявки #{request.TypeId} не найден");

        var isRepair = ticketType.Code == TypeRepair;
        if (isRepair && (request.AssetId is null || request.AssetId <= 0))
            throw new InvalidOperationException("Для «Тикет на ремонт» обязателен AssetId (HIST-03)");

        if (request.AssetId.HasValue)
        {
            var assetExists = await _db.Assets.AnyAsync(a => a.Id == request.AssetId.Value, ct);
            if (!assetExists)
                throw new InvalidOperationException($"Актив #{request.AssetId} не найден");
        }

        var initialStatus = await _db.TicketStatuses
            .OrderBy(s => s.SortOrder).ThenBy(s => s.Id)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("В системе не настроены статусы заявок");

        var ticket = new Ticket
        {
            Title = request.Title.Trim(),
            Description = request.Description,
            TypeId = request.TypeId,
            StatusId = initialStatus.Id,
            CriticalityLevelId = request.CriticalityLevelId,
            CreatorId = creatorId,
            AssigneeId = null,
            AssetId = request.AssetId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _db.Tickets.Add(ticket);
        await _db.SaveChangesAsync(ct);

        await _history.LogAsync(ticket.Id, creatorId, "Created", null, ticket.Title, ct);

        return await GetByIdAsync(ticket.Id, ct)
            ?? throw new InvalidOperationException("Не удалось загрузить созданную заявку");
    }

    // ==================== UPDATE ====================

    public async Task<TicketDto?> UpdateAsync(int id, UpdateTicketRequest request, int performedById, CancellationToken ct = default)
    {
        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (ticket is null) return null;

        if (request.CriticalityLevelId.HasValue && request.CriticalityLevelId != ticket.CriticalityLevelId)
        {
            var old = ticket.CriticalityLevelId?.ToString();
            ticket.CriticalityLevelId = request.CriticalityLevelId;
            await _history.LogAsync(ticket.Id, performedById, "CriticalityLevelId", old, request.CriticalityLevelId.Value.ToString(), ct);
        }

        if (request.AssigneeId != ticket.AssigneeId)
        {
            var old = ticket.AssigneeId?.ToString();
            ticket.AssigneeId = request.AssigneeId;
            await _history.LogAsync(ticket.Id, performedById, "AssigneeId", old, request.AssigneeId?.ToString(), ct);
        }

        ticket.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        return await GetByIdAsync(id, ct);
    }

    // ==================== CHANGE STATUS ====================

    public async Task<TicketDto?> ChangeStatusAsync(int id, ChangeTicketStatusRequest request, int performedById, CancellationToken ct = default)
    {
        var ticket = await _db.Tickets
            .Include(t => t.Type)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
        if (ticket is null) return null;

        var newStatus = await _db.TicketStatuses.FirstOrDefaultAsync(s => s.Id == request.StatusId, ct)
            ?? throw new InvalidOperationException($"Статус #{request.StatusId} не найден");

        // Проверка разрешённого перехода
        var allowed = await _db.TicketStatusTransitions
            .AnyAsync(tr => tr.FromStatusId == ticket.StatusId && tr.ToStatusId == request.StatusId, ct);
        if (!allowed)
            throw new InvalidOperationException(
                $"Переход из статуса {ticket.StatusId} в {request.StatusId} не разрешён.");

        // Для ремонта при закрытии — обязателен RepairResolution (АКТ-16)
        if (ticket.Type?.Code == TypeRepair && newStatus.Code == StatusClosed)
        {
            if (string.IsNullOrWhiteSpace(request.RepairResolution))
                throw new InvalidOperationException("При закрытии тикета на ремонт обязателен RepairResolution (АКТ-16).");
        }

        var oldStatus = ticket.StatusId;
        ticket.StatusId = request.StatusId;
        ticket.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        await _history.LogAsync(ticket.Id, performedById, "StatusId", oldStatus.ToString(), request.StatusId.ToString(), ct);

        // Логируем комментарий по ремонту в историю (как описание)
        if (!string.IsNullOrWhiteSpace(request.RepairResolution))
        {
            await _history.LogAsync(ticket.Id, performedById, "RepairResolution", null, request.RepairResolution, ct);
        }

        return await GetByIdAsync(id, ct);
    }

    // ==================== ESCALATE ====================

    public async Task<TicketEscalationDto?> EscalateAsync(int id, EscalateTicketRequest request, int initiatorId, CancellationToken ct = default)
    {
        var ticket = await _db.Tickets
            .Include(t => t.CriticalityLevel)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
        if (ticket is null) return null;

        // Поднимаем на следующий уровень критичности
        CriticalityLevel? newLevel = null;
        if (ticket.CriticalityLevelId.HasValue)
        {
            var currentOrder = ticket.CriticalityLevel?.SortOrder ?? 0;
            newLevel = await _db.CriticalityLevels
                .Where(c => c.IsActive && c.SortOrder > currentOrder)
                .OrderBy(c => c.SortOrder)
                .FirstOrDefaultAsync(ct);
        }

        var fromLevelId = ticket.CriticalityLevelId;
		var fromLevelName = ticket.CriticalityLevel?.Name;
        if (newLevel is not null)
            ticket.CriticalityLevelId = newLevel.Id;

        var escalation = new TicketEscalation
        {
            TicketId = ticket.Id,
            InitiatorUserId = initiatorId,
            Reason = request.Reason ?? "Ручная эскалация",
            FromCriticalityLevelId = fromLevelId,
            ToCriticalityLevelId = ticket.CriticalityLevelId,
            EscalatedAt = DateTimeOffset.UtcNow,
        };

        _db.TicketEscalations.Add(escalation);
        ticket.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        await _history.LogAsync(ticket.Id, initiatorId, "Escalation",
            fromLevelId?.ToString(), ticket.CriticalityLevelId?.ToString(), ct);

        var initiator = await _db.Users.FirstOrDefaultAsync(u => u.Id == initiatorId, ct);

        return new TicketEscalationDto
        {
            Id = escalation.Id,
            TicketId = escalation.TicketId,
            InitiatorUserId = escalation.InitiatorUserId,
            InitiatorName = initiator?.FullName,
            Reason = escalation.Reason,
            FromCriticalityLevelId = fromLevelId,
            FromCriticalityLevelName = fromLevelName,
            ToCriticalityLevelId = ticket.CriticalityLevelId,
            ToCriticalityLevelName = newLevel?.Name,
            EscalatedAt = escalation.EscalatedAt,
        };
    }

    // ==================== MESSAGES ====================

    public async Task<List<TicketMessageDto>> GetMessagesAsync(int ticketId, bool includeInternal, CancellationToken ct = default)
    {
        var query = _db.TicketMessages
            .Include(m => m.Author)
            .Include(m => m.Attachments)
            .Where(m => m.TicketId == ticketId);

        if (!includeInternal)
            query = query.Where(m => m.IsInternal == false);

        var messages = await query
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);

        return messages.Select(m => new TicketMessageDto
        {
            Id = m.Id,
            TicketId = m.TicketId,
            AuthorId = m.AuthorId,
            AuthorName = m.Author?.FullName,
            MessageBody = m.MessageBody,
            IsInternal = m.IsInternal ?? false,
            CreatedAt = m.CreatedAt,
            Attachments = m.Attachments.Select(a => new AttachmentDto
            {
                Id = a.Id,
                FileName = a.FileName,
                FilePath = a.FilePath,
                ContentType = a.ContentType,
                SizeBytes = a.SizeBytes,
                UploadedAt = a.UploadedAt,
            }).ToList(),
        }).ToList();
    }

    public async Task<TicketMessageDto?> AddMessageAsync(int ticketId, CreateTicketMessageRequest request, int authorId, CancellationToken ct = default)
    {
        var ticketExists = await _db.Tickets.AnyAsync(t => t.Id == ticketId, ct);
        if (!ticketExists) return null;

        var message = new TicketMessage
        {
            TicketId = ticketId,
            AuthorId = authorId,
            MessageBody = request.MessageBody,
            IsInternal = request.IsInternal,
            SourceType = "web",
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _db.TicketMessages.Add(message);

        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId, ct);
        if (ticket is not null) ticket.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        await _history.LogAsync(ticketId, authorId, "Message",
            null, request.IsInternal ? "internal" : "public", ct);

        return new TicketMessageDto
        {
            Id = message.Id,
            TicketId = message.TicketId,
            AuthorId = message.AuthorId,
            AuthorName = (await _db.Users.FirstOrDefaultAsync(u => u.Id == authorId, ct))?.FullName,
            MessageBody = message.MessageBody,
            IsInternal = message.IsInternal ?? false,
            CreatedAt = message.CreatedAt,
            Attachments = new List<AttachmentDto>(),
        };
    }

    // ==================== HELPERS ====================

    private static TicketDto MapToDto(Ticket t, int messageCount) => new()
    {
        Id = t.Id,
        Title = t.Title,
        Description = t.Description,
        TypeId = t.TypeId,
        TypeName = t.Type?.Name,
        StatusId = t.StatusId,
        StatusName = t.Status?.Name,
        CriticalityLevelId = t.CriticalityLevelId,
        CriticalityLevelName = t.CriticalityLevel?.Name,
        CreatorId = t.CreatorId,
        CreatorName = t.Creator?.FullName,
        AssigneeId = t.AssigneeId,
        AssigneeName = t.Assignee?.FullName,
        AssetId = t.AssetId,
        AssetName = t.Asset?.Name,
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt,
        ClosedAt = t.Status?.Code == StatusClosed ? t.UpdatedAt : null,
        MessageCount = messageCount,
    };
}
