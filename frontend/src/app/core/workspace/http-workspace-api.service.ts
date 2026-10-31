import { Injectable, inject } from '@angular/core';
import { forkJoin, map, of, switchMap, throwError } from 'rxjs';
import { HttpClient } from '@angular/common/http';
import { WorkspaceApi } from './workspace-api';
import { environment } from '../../../environments/environment';
import {
    AdminSettings,
    AssetDetails,
    AssetClassOption,
    AssetMovementRequest,
    AssetRecord,
    CreateAssetRequest,
    CreateTenantRequest,
    CreatePortalAccountRequest,
    CreateTicketRequest,
    EscalationHistoryEntry,
    EscalationPolicy,
    Escalation,
    FormFieldDefinition,
    OverviewData,
    PortalTicketRequest,
    RepairCloseRequest,
    ReportSummary,
    TicketComment,
    TicketFormDefinition,
    Ticket,
    TicketType,
    TenantRecord,
    CreateUserRequest,
    SystemRoleOption,
    UserRecord,
} from './workspace.models';

@Injectable()
export class HttpWorkspaceApiService extends WorkspaceApi {
    private readonly http = inject(HttpClient);
    private readonly baseUrl = environment.apiBaseUrl;
    private readonly typeIds = new Map<string, number>();
    private readonly statusIds = new Map<string, number>();

    getOverview() {
        return this.getTickets().pipe(map(toOverview));
    }
    getTickets() {
        return this.http.get<BackendTicket[]>(`${this.baseUrl}/tickets`).pipe(
            map(tickets => {
                tickets.forEach(ticket => this.rememberLookups(ticket));
                return tickets.map(toTicket);
            }),
        );
    }
    getAssets() { return this.http.get<AssetRecord[]>(`${this.baseUrl}/assets`); }
    getAssetClasses() {
        return this.http.get<{ id: number; code: string; name: string; isActive: boolean }[]>(`${this.baseUrl}/asset-classes`).pipe(
            switchMap(classes => {
                if (!classes.length) return of([] as AssetClassOption[]);
                const requests = classes.map(assetClass => this.http
                    .get<{ code: string; name: string; dataType: string; isRequired: boolean; defaultValue: string | null; options: string | null }[]>(`${this.baseUrl}/asset-classes/${assetClass.id}/attributes`)
                    .pipe(map(attributes => ({
                        id: assetClass.id,
                        code: assetClass.code,
                        name: assetClass.name,
                        active: assetClass.isActive,
                        attributes: attributes.map(attribute => ({
                            code: attribute.code,
                            name: attribute.name,
                            dataType: attribute.dataType,
                            required: attribute.isRequired,
                            defaultValue: attribute.defaultValue ?? undefined,
                            options: parseOptions(attribute.options),
                        })),
                    } satisfies AssetClassOption))));
                return forkJoin(requests);
            }),
        );
    }
    getEscalations() {
        return this.getTickets().pipe(map(tickets => tickets
            .filter(ticket => !isClosedStatus(ticket.status))
            .filter(ticket => {
                const priority = ticket.priority.toLocaleLowerCase();
                return priority.includes('крит') || priority.includes('высок');
            })
            .map(ticket => ({
                id: ticket.id,
                title: ticket.title,
                team: ticket.type ?? '',
                priority: ticket.priority,
                slaRemaining: ticket.slaRemaining,
            } satisfies Escalation))));
    }
    createTicket(request: CreateTicketRequest) {
        const typeId = request.typeId
            ?? (request.type ? this.resolveId(request.type, this.typeIds) : undefined);
        if (!typeId) {
            return throwError(() => new Error(
                'Не удалось определить typeId: загрузите типы заявок или очередь перед созданием.',
            ));
        }

        const assetId = request.assetId ? Number(request.assetId) : null;
        const body = {
            title: request.title,
            description: request.description?.trim() || request.title,
            typeId,
            priority: request.priority?.trim() || 'Средний',
            assetId: assetId !== null && Number.isFinite(assetId) ? assetId : null,
            customFieldsData: request.fields ?? null,
        };

        return this.http.post<BackendTicket>(`${this.baseUrl}/tickets`, body).pipe(map(toTicket));
    }
    getTicketTypes() { return this.unsupported<TicketType[]>('ticket types'); }
    saveTicketTypes(_ticketTypes: TicketType[]) { return this.unsupported<TicketType[]>('ticket types'); }
    changeTicketStatus(ticketId: string, statusId: number) {
        return this.http
            .put<BackendTicket>(`${this.baseUrl}/tickets/${ticketId}/status`, { statusId })
            .pipe(map(toTicket));
    }
    assignTicket(ticketId: string, assigneeId: number) {
        return this.http
            .post<BackendTicket>(`${this.baseUrl}/tickets/${ticketId}/assign`, { assigneeId })
            .pipe(map(toTicket));
    }
    changeTicketPriority(ticketId: string, priority: string) {
        return this.http
            .put<BackendTicket>(`${this.baseUrl}/tickets/${ticketId}`, { priority })
            .pipe(map(toTicket));
    }
    addTicketComment(ticketId: string, comment: Omit<TicketComment, 'id' | 'createdAt'>) {
        return this.http
            .post<BackendTicketMessage>(`${this.baseUrl}/tickets/${ticketId}/messages`, {
                messageBody: comment.message,
                isInternal: comment.internal,
                attachments: comment.attachments,
            })
            .pipe(map(toTicketComment));
    }
    escalateTicket(ticketId: string, initiator: string) {
        return this.http
            .post<BackendTicketEscalation>(`${this.baseUrl}/tickets/${ticketId}/escalate`, null)
            .pipe(map(escalation => toEscalationHistory(escalation, initiator)));
    }
    getAssetDetails(assetId: string) { return this.http.get<AssetDetails>(`${this.baseUrl}/assets/${assetId}`); }
    createAsset(request: CreateAssetRequest) { return this.http.post<AssetDetails>(`${this.baseUrl}/assets`, request); }
    updateAsset(assetId: string, changes: Partial<CreateAssetRequest>) { return this.http.put<AssetDetails>(`${this.baseUrl}/assets/${assetId}`, changes); }
    deleteAsset(assetId: string) { return this.http.delete<void>(`${this.baseUrl}/assets/${assetId}`); }
    moveAsset(assetId: string, request: AssetMovementRequest) { return this.http.post<AssetDetails>(`${this.baseUrl}/assets/${assetId}/movements`, request); }
    createRepairTicket(assetId: string, title: string) {
        const typeId = this.typeIds.get('тикет на ремонт') ?? this.typeIds.get('ремонт');
        if (!typeId) {
            return throwError(() => new Error(
                'Не удалось определить typeId ремонтной заявки: загрузите очередь заявок.',
            ));
        }

        return this.createTicket({
            title,
            requester: '',
            typeId,
            priority: 'Средний',
            assetId,
        });
    }
    closeRepairTicket(ticketId: string, request: RepairCloseRequest) {
        const statusId = this.statusIds.get('закрыта')
            ?? [...this.statusIds.entries()].find(([name]) => name.includes('закрыт'))?.[1];
        if (!statusId) {
            return throwError(() => new Error(
                'Не удалось определить статус закрытия: загрузите очередь заявок.',
            ));
        }

        return this.http
            .put<BackendTicket>(`${this.baseUrl}/tickets/${ticketId}/status`, {
                statusId,
                repairResolution: request.workDescription,
                repairOutcome: repairOutcome(request.result),
            })
            .pipe(map(toTicket));
    }
    getForms() { return this.unsupported<TicketFormDefinition[]>('ticket forms'); }
    saveForm(_form: TicketFormDefinition) { return this.unsupported<TicketFormDefinition>('ticket forms'); }
    getEscalationPolicies() { return this.unsupported<EscalationPolicy[]>('escalation policies'); }
    saveEscalationPolicies(_policies: EscalationPolicy[]) { return this.unsupported<EscalationPolicy[]>('escalation policies'); }
    getEscalationHistory() {
        // Отдельного endpoint истории эскалаций в текущем backend нет.
        return of([] as EscalationHistoryEntry[]);
    }
    getAdminSettings() { return this.unsupported<AdminSettings>('admin settings'); }
    saveAdminSettings(_settings: AdminSettings) { return this.unsupported<AdminSettings>('admin settings'); }
    getReports(_from?: string, _to?: string) {
        // В контракте отчёты требуют reportType: /api/reports/{reportType}; текущий backend их не реализует.
        return this.unsupported<ReportSummary[]>('reports');
    }
    getPortalTickets() { return this.unsupported<Ticket[]>('portal tickets'); }
    createPortalTicket(_request: PortalTicketRequest) {
        return this.unsupported<Ticket>('portal ticket creation');
    }
    addPortalComment(ticketId: string, message: string, attachments: File[]) {
        const uploads = attachments.length
            ? forkJoin(attachments.map(file => this.uploadAttachment(ticketId, file)))
            : of([] as string[]);

        return uploads.pipe(
            switchMap(attachmentIds => this.http.post<BackendTicketMessage>(
                `${this.baseUrl}/tickets/${ticketId}/messages`,
                { messageBody: message, isInternal: false, attachments: attachmentIds },
            )),
            map(toTicketComment),
        );
    }

    private uploadAttachment(ticketId: string, file: File) {
        const formData = new FormData();
        formData.append('file', file);
        return this.http
            .post<{ id: string }>(`${this.baseUrl}/tickets/${ticketId}/attachments`, formData)
            .pipe(map(upload => upload.id));
    }
    reviewPortalTicket(_ticketId: string, _accepted: boolean) {
        return this.unsupported<Ticket>('portal ticket review');
    }
    createPortalAccount(_request: CreatePortalAccountRequest) {
        return this.unsupported<string>('portal account creation');
    }
    getTenants() { return this.http.get<TenantRecord[]>(`${this.baseUrl}/tenants`); }
    createTenant(request: CreateTenantRequest) { return this.http.post<TenantRecord>(`${this.baseUrl}/tenants`, request); }
    setTenantActive(_tenantId: string, _active: boolean) {
        return throwError(() => new Error('Backend tenant status endpoint is not implemented.'));
    }
    deleteTenant(tenantId: string) { return this.http.delete<void>(`${this.baseUrl}/tenants/${tenantId}`); }
    getUsers() {
        return this.http.get<{ items: UserRecord[] }>(`${this.baseUrl}/users`).pipe(map(result => result.items));
    }
    getSystemRoles() { return this.http.get<SystemRoleOption[]>(`${this.baseUrl}/users/roles`); }
    createUser(request: CreateUserRequest) { return this.http.post<UserRecord>(`${this.baseUrl}/users`, request); }
    setUserActive(userId: number, isActive: boolean) {
        return this.http.put<UserRecord>(`${this.baseUrl}/users/${userId}/active`, { isActive });
    }

    private rememberLookups(ticket: BackendTicket): void {
        if (ticket.typeName && ticket.typeId != null) {
            this.typeIds.set(ticket.typeName.toLocaleLowerCase(), ticket.typeId);
        }
        if (ticket.statusName && ticket.statusId != null) {
            this.statusIds.set(ticket.statusName.toLocaleLowerCase(), ticket.statusId);
        }
    }

    private resolveId(value: string, source: Map<string, number>): number | undefined {
        const numeric = Number(value);
        if (Number.isInteger(numeric) && numeric > 0) return numeric;
        return source.get(value.trim().toLocaleLowerCase());
    }

    private unsupported<T>(feature: string) {
        return throwError(() => new Error(`Backend endpoint for ${feature} is not implemented.`));
    }
}

function toOverview(tickets: Ticket[]): OverviewData {
    const open = tickets.filter(ticket => !isClosedStatus(ticket.status));
    const critical = open.filter(ticket => {
        const priority = ticket.priority.toLocaleLowerCase();
        return priority.includes('крит') || priority.includes('высок');
    });
    const overdue = tickets.filter(ticket => ticket.slaRemaining === 'Просрочено');

    return {
        metrics: [
            { label: 'Активные заявки', value: String(open.length), change: '' },
            { label: 'Критические', value: String(critical.length), change: '' },
            { label: 'Просроченные SLA', value: String(overdue.length), change: '' },
            { label: 'Всего заявок', value: String(tickets.length), change: '' },
        ],
        series: buildWeeklySeries(tickets),
        recentTickets: tickets.slice(0, 5),
        activeEscalations: critical.length,
    };
}

function buildWeeklySeries(tickets: Ticket[]): OverviewData['series'] {
    const days = Array.from({ length: 7 }, (_, index) => {
        const date = new Date();
        date.setDate(date.getDate() - (6 - index));
        return date;
    });

    const incoming = days.map(day => tickets.filter(ticket => sameDay(ticket.createdAt, day)).length);
    const resolved = days.map(day => tickets.filter(ticket =>
        isClosedStatus(ticket.status) && sameDay(ticket.closedOn ?? ticket.createdAt, day),
    ).length);
    const max = Math.max(1, ...incoming, ...resolved);

    return days.map((day, index) => ({
        label: day.toLocaleDateString('ru-RU', { weekday: 'short' }),
        incoming: Math.round(incoming[index] / max * 100),
        resolved: Math.round(resolved[index] / max * 100),
    }));
}

function sameDay(value: string | undefined, day: Date): boolean {
    if (!value) return false;
    const date = new Date(value);
    return !Number.isNaN(date.getTime())
        && date.getFullYear() === day.getFullYear()
        && date.getMonth() === day.getMonth()
        && date.getDate() === day.getDate();
}

function isClosedStatus(status: string): boolean {
    const normalized = status.toLocaleLowerCase();
    return normalized.includes('закрыт') || normalized.includes('решён') || normalized.includes('решен');
}

function repairOutcome(result: RepairCloseRequest['result']): string {
    switch (result) {
        case 'На склад':
            return 'TransferToWarehouse';
        case 'Списать':
            return 'Retire';
        case 'В эксплуатацию':
        default:
            return 'ReturnToExploitation';
    }
}

interface BackendTicket {
    id?: number;
    title?: string;
    description?: string | null;
    typeId?: number;
    typeName?: string;
    statusId?: number;
    statusName?: string;
    priority?: string | null;
    creatorId?: number;
    creatorName?: string;
    assigneeId?: number | null;
    assigneeName?: string | null;
    assetId?: number | null;
    customFieldsData?: Record<string, unknown> | null;
    createdAt?: string;
    updatedAt?: string;
    slaReactionDeadline?: string | null;
    slaResolutionDeadline?: string | null;
}

interface BackendTicketMessage {
    id?: number;
    ticketId?: number;
    authorId?: number;
    authorName?: string;
    messageBody?: string;
    isInternal?: boolean;
    attachments?: { id?: number; fileName?: string }[];
    createdAt?: string;
}

interface BackendTicketEscalation {
    id?: number;
    ticketId?: number;
    initiatorUserId?: number | null;
    reason?: string;
    fromCriticalityLevelId?: number | null;
    toCriticalityLevelId?: number | null;
    notifiedUserIds?: number[];
    escalatedAt?: string;
}

function toTicket(ticket: BackendTicket): Ticket {
    const fields = toScalarFields(ticket.customFieldsData);
    const isClosed = (ticket.statusName ?? '').toLocaleLowerCase().includes('закрыт');

    return {
        id: String(ticket.id ?? ''),
        title: ticket.title ?? '',
        requester: ticket.creatorName ?? '',
        team: '',
        priority: ticket.priority ?? '',
        status: ticket.statusName ?? '',
        createdAt: ticket.createdAt ?? '',
        slaRemaining: formatRemaining(ticket.slaResolutionDeadline),
        type: ticket.typeName,
        assignee: ticket.assigneeName ?? undefined,
        createdOn: ticket.createdAt,
        closedOn: isClosed ? ticket.updatedAt : undefined,
        reactionSla: formatRemaining(ticket.slaReactionDeadline),
        resolutionSla: formatRemaining(ticket.slaResolutionDeadline),
        assetId: ticket.assetId != null ? String(ticket.assetId) : undefined,
        fields,
    };
}

function toTicketComment(message: BackendTicketMessage): TicketComment {
    return {
        id: String(message.id ?? ''),
        author: message.authorName ?? '',
        message: message.messageBody ?? '',
        createdAt: message.createdAt ?? '',
        internal: message.isInternal === true,
        attachments: (message.attachments ?? [])
            .map(attachment => attachment.fileName ?? String(attachment.id ?? ''))
            .filter(Boolean),
    };
}

function toEscalationHistory(
    escalation: BackendTicketEscalation,
    initiatorFallback: string,
): EscalationHistoryEntry {
    return {
        ticketId: String(escalation.ticketId ?? ''),
        initiator: escalation.initiatorUserId != null
            ? String(escalation.initiatorUserId)
            : initiatorFallback,
        createdAt: escalation.escalatedAt ?? '',
        previousPriority: escalation.fromCriticalityLevelId != null
            ? String(escalation.fromCriticalityLevelId)
            : '',
        newPriority: escalation.toCriticalityLevelId != null
            ? String(escalation.toCriticalityLevelId)
            : '',
        recipients: (escalation.notifiedUserIds ?? []).map(String),
    };
}

function toScalarFields(value: Record<string, unknown> | null | undefined): Record<string, string | number | boolean | null> | undefined {
    if (!value || typeof value !== 'object' || Array.isArray(value)) return undefined;

    const fields: Record<string, string | number | boolean | null> = {};
    for (const [key, field] of Object.entries(value)) {
        if (field === null || ['string', 'number', 'boolean'].includes(typeof field)) {
            fields[key] = field as string | number | boolean | null;
        }
    }

    return Object.keys(fields).length ? fields : undefined;
}

function formatRemaining(deadline: string | null | undefined): string {
    if (!deadline) return '';
    const date = new Date(deadline);
    if (Number.isNaN(date.getTime())) return '';

    const diffMinutes = Math.floor((date.getTime() - Date.now()) / 60_000);
    if (diffMinutes <= 0) return 'Просрочено';

    const days = Math.floor(diffMinutes / 1440);
    const hours = Math.floor((diffMinutes % 1440) / 60);
    const minutes = diffMinutes % 60;
    if (days > 0) return `${days} д ${hours} ч`;
    if (hours > 0) return `${hours} ч ${minutes} мин`;
    return `${minutes} мин`;
}

function parseOptions(value: string | null): string[] {
    if (!value) return [];
    try {
        const parsed: unknown = JSON.parse(value);
        if (Array.isArray(parsed)) return parsed.map(String);
    } catch {
        return value.split(',').map(option => option.trim()).filter(Boolean);
    }
    return [];
}
