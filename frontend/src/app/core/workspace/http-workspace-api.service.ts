import { Injectable, inject } from '@angular/core';
import { forkJoin, map, of, switchMap, throwError } from 'rxjs';
import { HttpClient } from '@angular/common/http';
import { WorkspaceApi } from './workspace-api';
import { AssetLifecycle } from './workspace.models';
import { environment } from '../../../environments/environment';
import {
    AdminSettings,
    AssetDetails,
    AssetClassOption,
    AssetMovementRequest,
    AssetRecord,
    CreateAssetRequest,
    CreateTenantRequest,
    UpdateTenantRequest,
    BlockTenantRequest,
    UnblockTenantRequest,
    CreatePortalAccountRequest,
    CreateTicketRequest,
    EscalationHistoryEntry,
    EscalationPolicy,
    Escalation,
    AssignmentSetting,
    AutoAssignResult,
    FormFieldDefinition,
    OverviewData,
    PortalTicketType,
    PortalTicketRequest,
    RequesterOption,
    RepairCloseRequest,
    ReportSummary,
    TicketComment,
    TicketHistory,
    TicketAttachment,
    TicketFormDefinition,
    Ticket,
    TicketType,
    TenantRecord,
    TenantAdminCreationPolicy,
    CreateUserRequest,
    SystemRoleOption,
    UserRecord,
} from './workspace.models';

@Injectable({ providedIn: 'root' })
export class HttpWorkspaceApiService extends WorkspaceApi {
    readonly supportsTicketTypeConfiguration = false;

    private readonly http = inject(HttpClient);
    private readonly baseUrl = environment.apiBaseUrl;
    private readonly typeIds = new Map<string, number>();
    private readonly statusIds = new Map<string, number>();
    private readonly assigneeIds = new Map<string, number>();
    private readonly assetClassIds = new Map<string, number>();
    private readonly assigneeNames = new Map<string, string>();

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
    getAssets() {
        return this.http.get<BackendAsset[]>(`${this.baseUrl}/assets`).pipe(
            map(assets => assets.map(asset => {
                if (asset.assetClassName) {
                    this.assetClassIds.set(asset.assetClassName.toLocaleLowerCase(), asset.assetClassId!);
                }
                return toAssetRecord(asset);
            })),
        );
    }
    getAssetClasses() {
        return this.http.get<{ id: number; code: string; name: string; isActive: boolean }[]>(`${this.baseUrl}/asset-classes`).pipe(
            switchMap(classes => {
                classes.forEach(assetClass => this.assetClassIds.set(assetClass.name.toLocaleLowerCase(), assetClass.id));
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
            requesterId: request.requesterId ?? null,
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
    autoAssignTicket(ticketId: string) {
        return this.http
            .post<{ ticketId?: number; assigneeId?: number; assigneeName?: string; openTicketsCount?: number; reason?: string }>(
                `${this.baseUrl}/tickets/${ticketId}/auto-assign`,
                null,
            )
            .pipe(map(result => ({
                ticketId: String(result.ticketId ?? ticketId),
                assigneeId: result.assigneeId ?? 0,
                assigneeName: result.assigneeName ?? '',
                openTicketsCount: result.openTicketsCount ?? 0,
                reason: result.reason ?? '',
            } satisfies AutoAssignResult)));
    }
    getAssignmentSetting() {
        return this.http
            .get<BackendAssignmentSetting>(`${this.baseUrl}/tickets/assignment-setting`)
            .pipe(map(toAssignmentSetting));
    }
    saveAssignmentSetting(mode: 'auto' | 'manual') {
        return this.http
            .put<BackendAssignmentSetting>(`${this.baseUrl}/tickets/assignment-setting`, { mode })
            .pipe(map(toAssignmentSetting));
    }
    changeTicketPriority(ticketId: string, priority: string) {
        return this.http
            .put<BackendTicket>(`${this.baseUrl}/tickets/${ticketId}`, { priority })
            .pipe(map(toTicket));
    }
    getTicketComments(ticketId: string) {
        return this.http.get<BackendTicketMessage[]>(`${this.baseUrl}/tickets/${ticketId}/messages`)
            .pipe(map(messages => messages.map(toTicketComment)));
    }
    getTicketCommentsPage(ticketId: string, skip: number, take: number) {
        return this.http.get<BackendTicketMessage[]>(`${this.baseUrl}/tickets/${ticketId}/messages`, { params: { skip, take } })
            .pipe(map(messages => messages.map(toTicketComment)));
    }
    getTicketHistory(ticketId: string) {
        return this.http.get<BackendTicketHistory[]>(`${this.baseUrl}/tickets/${ticketId}/history`)
            .pipe(map(history => history.map(item => ({
                id: String(item.id),
                event: item.event,
                actor: item.actor,
                createdAt: item.createdAt,
            }))));
    }
    getTicketHistoryPage(ticketId: string, skip: number, take: number) {
        return this.http.get<BackendTicketHistory[]>(`${this.baseUrl}/tickets/${ticketId}/history`, { params: { skip, take } })
            .pipe(map(history => history.map(item => ({
                id: String(item.id),
                event: item.event,
                actor: item.actor,
                createdAt: item.createdAt,
            }))));
    }
    addTicketComment(
        ticketId: string,
        comment: Omit<TicketComment, 'id' | 'createdAt' | 'attachments' | 'attachmentRecords'>,
        attachments: File[],
    ) {
        return this.postTicketComment(ticketId, comment.message, comment.internal, attachments);
    }
    downloadTicketAttachment(ticketId: string, attachment: TicketAttachment) {
        return this.http.get(`${this.baseUrl}/tickets/${ticketId}/attachments/${attachment.id}`, {
            responseType: 'blob',
        });
    }
    escalateTicket(ticketId: string, initiator: string) {
        return this.http
            .post<BackendTicketEscalation>(`${this.baseUrl}/tickets/${ticketId}/escalate`, null)
            .pipe(map(escalation => toEscalationHistory(escalation, initiator)));
    }
    getAssetDetails(assetId: string) {
        return this.http
            .get<BackendAssetDetail>(`${this.baseUrl}/assets/${assetId}`)
            .pipe(map(detail => {
                for (const ticket of detail.relatedTickets ?? []) {
                    if (ticket.typeName && ticket.typeId != null) {
                        this.typeIds.set(ticket.typeName.toLocaleLowerCase(), ticket.typeId);
                    }
                    if (ticket.statusName && ticket.statusId != null) {
                        this.statusIds.set(ticket.statusName.toLocaleLowerCase(), ticket.statusId);
                    }
                }
                return toAssetDetails(detail);
            }));
    }
    createAsset(request: CreateAssetRequest) {
        const assetClassId = request.assetClassId ?? this.resolveAssetClassId(request.className);
        if (!assetClassId) {
            return throwError(() => new Error('Не удалось определить класс актива. Обновите список классов.'));
        }

        const body = {
            name: request.name?.trim(),
            inventoryNumber: request.id?.trim() || null,
            serialNumber: request.serialNumber?.trim() || null,
            assetClassId,
            warehouseId: request.warehouseId ?? null,
            locationId: request.locationId ?? null,
            departmentId: request.departmentId ?? null,
            attributes: request.attributes ?? null,
        };

        return this.http.post<BackendAsset>(`${this.baseUrl}/assets`, body).pipe(
            switchMap(created => this.getAssetDetails(String(created.id))),
        );
    }
    updateAsset(assetId: string, changes: Partial<CreateAssetRequest>) {
        const body = {
            name: changes.name,
            inventoryNumber: changes.id,
            serialNumber: changes.serialNumber,
            assetClassId: changes.assetClassId,
            warehouseId: changes.warehouseId,
            locationId: changes.locationId,
            departmentId: changes.departmentId,
            attributes: changes.attributes ?? null,
        };

        return this.http.put<BackendAsset>(`${this.baseUrl}/assets/${assetId}`, body).pipe(
            switchMap(() => this.getAssetDetails(assetId)),
        );
    }
    deleteAsset(assetId: string) { return this.http.delete<void>(`${this.baseUrl}/assets/${assetId}`); }
    moveAsset(assetId: string, request: AssetMovementRequest) {
        const movementType = this.resolveMovementType(request.kind);
        if (!movementType) {
            return throwError(() => new Error(`Не удалось определить тип движения: ${request.kind}.`));
        }

        const body = {
            movementType,
            toUserId: request.toUserId ?? null,
            toWarehouseId: request.toWarehouseId ?? null,
            toLocationId: request.toLocationId ?? null,
            toDepartmentId: request.toDepartmentId ?? null,
            comment: request.comment?.trim()
                || (request.from.trim() && request.to.trim() ? `${request.from.trim()} → ${request.to.trim()}` : null),
        };

        return this.http.post(`${this.baseUrl}/assets/${assetId}/movements`, body).pipe(
            switchMap(() => this.getAssetDetails(assetId)),
        );
    }
    createRepairTicket(assetId: string, title: string) {
        return this.getTicketLookups().pipe(
            switchMap(lookups => {
                const repairType = lookups.types.find(type => type.code.toLocaleLowerCase() === 'repair');
                if (!repairType) {
                    return throwError(() => new Error('В backend не найден тип заявки на ремонт.'));
                }

                return this.createTicket({
                    title,
                    requester: '',
                    typeId: repairType.id,
                    priority: 'Средний',
                    assetId,
                });
            }),
        );
    }
    closeRepairTicket(ticketId: string, request: RepairCloseRequest) {
        return this.getTicketLookups().pipe(
            switchMap(lookups => {
                const closedStatus = lookups.statuses.find(status => status.code.toLocaleLowerCase() === 'closed');
                if (!closedStatus) {
                    return throwError(() => new Error('В backend не найден закрытый статус заявки.'));
                }

                return this.http
                    .put<BackendTicket>(`${this.baseUrl}/tickets/${ticketId}/status`, {
                        statusId: closedStatus.id,
                        repairResolution: request.workDescription,
                        repairOutcome: repairOutcome(request.result),
                        repairToUserId: request.result === 'В эксплуатацию' ? request.repairToUserId ?? null : null,
                        repairToWarehouseId: request.result === 'На склад' ? request.repairToWarehouseId ?? null : null,
                    })
                    .pipe(map(toTicket));
            }),
        );
    }
    getForms() { return this.unsupported<TicketFormDefinition[]>('ticket forms'); }
    saveForm(_form: TicketFormDefinition) { return this.unsupported<TicketFormDefinition>('ticket forms'); }
    getEscalationPolicies() { return this.unsupported<EscalationPolicy[]>('escalation policies'); }
    saveEscalationPolicies(_policies: EscalationPolicy[]) { return this.unsupported<EscalationPolicy[]>('escalation policies'); }
    getEscalationHistory() {
        // Отдельного endpoint истории эскалаций в текущем backend нет.
        return of([] as EscalationHistoryEntry[]);
    }
    getAdminSettings() {
        return this.getTenantAdminCreationPolicy().pipe(
            map(settings => toAdminSettings(settings)),
        );
    }
    saveAdminSettings(settings: AdminSettings) {
        return this.saveTenantAdminCreationPolicy(settings.allowAdminsCreateAdmins ?? true).pipe(
            map(saved => toAdminSettings(saved)),
        );
    }
    getTenantAdminCreationPolicy() {
        return this.http.get<TenantAdminCreationPolicy>(`${this.baseUrl}/tenants/settings`).pipe(
            map(normalizeTenantAdminCreationPolicy),
        );
    }
    saveTenantAdminCreationPolicy(allowAdminsCreateAdmins: boolean) {
        return this.http.put<TenantAdminCreationPolicy>(`${this.baseUrl}/tenants/settings`, {
            allowAdminsCreateAdmins,
        }).pipe(map(normalizeTenantAdminCreationPolicy));
    }
    getReports(_from?: string, _to?: string) {
        // В контракте отчёты требуют reportType: /api/reports/{reportType}; текущий backend их не реализует.
        return this.unsupported<ReportSummary[]>('reports');
    }
    getPortalTickets() {
        return this.http.get<BackendTicket[]>(`${this.baseUrl}/tickets/mine`).pipe(
            map(tickets => tickets.map(toTicket)),
        );
    }
    getPortalTicketTypes() {
        return this.http.get<PortalTicketType[]>(`${this.baseUrl}/tickets/portal-types`);
    }
    getRequesters() {
        return this.http.get<RequesterOption[]>(`${this.baseUrl}/tickets/requesters`);
    }
    createPortalTicket(request: PortalTicketRequest) {
        return this.createTicket(request);
    }
    addPortalComment(ticketId: string, message: string, attachments: File[]) {
        return this.postTicketComment(ticketId, message, false, attachments);
    }

    private postTicketComment(ticketId: string, message: string, internal: boolean, attachments: File[]) {
        const uploads = attachments.length
            ? forkJoin(attachments.map(file => this.uploadAttachment(ticketId, file)))
            : of([] as string[]);

        return uploads.pipe(
            switchMap(attachmentIds => this.http.post<BackendTicketMessage>(
                `${this.baseUrl}/tickets/${ticketId}/messages`,
                { messageBody: message, isInternal: internal, attachments: attachmentIds },
            )),
            map(toTicketComment),
        );
    }

    private getTicketLookups() {
        return this.http.get<BackendTicketLookups>(`${this.baseUrl}/tickets/lookups`).pipe(
            map(lookups => {
                for (const type of lookups.types) {
                    this.typeIds.set(type.name.toLocaleLowerCase(), type.id);
                }
                for (const status of lookups.statuses) {
                    this.statusIds.set(status.name.toLocaleLowerCase(), status.id);
                }
                return lookups;
            }),
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
    getTenant(tenantId: number) { return this.http.get<TenantRecord>(`${this.baseUrl}/tenants/${tenantId}`); }
    createTenant(request: CreateTenantRequest) { return this.http.post<TenantRecord>(`${this.baseUrl}/tenants`, request); }
    updateTenant(tenantId: number, request: UpdateTenantRequest) { return this.http.put<TenantRecord>(`${this.baseUrl}/tenants/${tenantId}`, request); }
    setTenantActive(tenantId: number, isActive: boolean) { return this.http.patch<TenantRecord>(`${this.baseUrl}/tenants/${tenantId}/status`, { isActive }); }
    blockTenant(tenantId: number, request: BlockTenantRequest) { return this.http.post<TenantRecord>(`${this.baseUrl}/tenants/${tenantId}/block`, request); }
    unblockTenant(tenantId: number) { return this.http.post<TenantRecord>(`${this.baseUrl}/tenants/${tenantId}/unblock`, {}); }
    deleteTenant(tenantId: number) { return this.http.delete<void>(`${this.baseUrl}/tenants/${tenantId}`); }
    getUsers() {
        return this.http.get<{ items: UserRecord[] }>(`${this.baseUrl}/users`)
            .pipe(map(response => response.items));
    }
    getSystemRoles() { return this.http.get<SystemRoleOption[]>(`${this.baseUrl}/users/roles`); }
    createUser(request: CreateUserRequest) { return this.http.post<UserRecord>(`${this.baseUrl}/users`, request); }
    setUserActive(userId: number, isActive: boolean) {
        return this.http.put<UserRecord>(`${this.baseUrl}/users/${userId}/active`, { isActive });
    }

    /** ID статуса по имени из уже загруженной очереди. */
    resolveStatusId(name: string): number | undefined {
        const key = name.trim().toLocaleLowerCase();
        const exact = this.statusIds.get(key);
        if (exact != null) return exact;

        return [...this.statusIds.entries()]
            .find(([statusName]) => statusName.includes(key) || key.includes(statusName))?.[1];
    }

    /** ID исполнителя по имени из уже загруженной очереди. */
    resolveAssigneeId(value: string): number | undefined {
        const numeric = Number(value);
        if (Number.isInteger(numeric) && numeric > 0) return numeric;
        return this.assigneeIds.get(value.trim().toLocaleLowerCase());
    }

    knownAssigneeNames(): string[] {
        return [...this.assigneeNames.values()].sort((left, right) => left.localeCompare(right, 'ru'));
    }

    private rememberLookups(ticket: BackendTicket): void {
        if (ticket.typeName && ticket.typeId != null) {
            this.typeIds.set(ticket.typeName.toLocaleLowerCase(), ticket.typeId);
        }
        if (ticket.statusName && ticket.statusId != null) {
            this.statusIds.set(ticket.statusName.toLocaleLowerCase(), ticket.statusId);
        }
        if (ticket.assigneeName && ticket.assigneeId != null) {
            const key = ticket.assigneeName.toLocaleLowerCase();
            this.assigneeIds.set(key, ticket.assigneeId);
            this.assigneeNames.set(key, ticket.assigneeName);
        }
    }

    private resolveId(value: string, source: Map<string, number>): number | undefined {
        const numeric = Number(value);
        if (Number.isInteger(numeric) && numeric > 0) return numeric;
        return source.get(value.trim().toLocaleLowerCase());
    }

    private resolveAssetClassId(className: string | undefined): number | undefined {
        if (!className) return undefined;
        return this.resolveId(className, this.assetClassIds);
    }

    private resolveMovementType(kind: string): string | undefined {
        return ASSET_MOVEMENT_TYPES[kind.trim()];
    }

    private unsupported<T>(feature: string) {
        return throwError(() => new Error(`Backend endpoint for ${feature} is not implemented.`));
    }
}

function toAdminSettings(settings?: Partial<BackendTenantSettings> | null): AdminSettings {
    const defaults: AdminSettings = {
        assignmentMode: 'automatic',
        manager: '',
        slaEnabled: true,
        slaMode: 'working-hours',
        workingHours: '09:00-18:00',
        workingDays: ['Пн', 'Вт', 'Ср', 'Чт', 'Пт'],
        holidays: [],
        criticalities: ['Критический', 'Высокий', 'Средний', 'Низкий'],
        statuses: [
            { name: 'Открыта', pausesSla: false, immutable: true },
            { name: 'В работе', pausesSla: false, immutable: false },
            { name: 'Проверка', pausesSla: false, immutable: false },
            { name: 'Закрыта', pausesSla: false, immutable: true },
        ],
        statusTransitions: {
            Открыта: ['В работе', 'Проверка', 'Закрыта'],
            'В работе': ['Проверка', 'Закрыта'],
            Проверка: ['В работе', 'Закрыта'],
            Закрыта: [],
        },
        portalSlaFields: [],
        portalAccounts: [],
        engineers: [],
        tenantMode: 'saas',
        passwordPolicy: {
            minLength: 12,
            requireUppercase: true,
            requireSpecial: true,
            expiryDays: 90,
            maxFailedAttempts: 5,
            lockoutMinutes: 15,
        },
        reportAccessRoles: ['admin', 'manager'],
        allowAdminsCreateAdmins: true,
    };

    return {
        ...defaults,
        ...settings,
        workingDays: settings?.workingDays?.length ? [...settings.workingDays] : defaults.workingDays,
        holidays: settings?.holidays?.length ? [...settings.holidays] : defaults.holidays,
        criticalities: settings?.criticalities?.length ? [...settings.criticalities] : defaults.criticalities,
        statuses: settings?.statuses?.length ? settings.statuses.map(status => ({ ...status })) : defaults.statuses,
        statusTransitions: settings?.statusTransitions ? { ...settings.statusTransitions } : defaults.statusTransitions,
        portalSlaFields: settings?.portalSlaFields?.length ? [...settings.portalSlaFields] : defaults.portalSlaFields,
        portalAccounts: settings?.portalAccounts?.length ? [...settings.portalAccounts] : defaults.portalAccounts,
        engineers: settings?.engineers?.length ? [...settings.engineers] : defaults.engineers,
        reportAccessRoles: settings?.reportAccessRoles?.length ? [...settings.reportAccessRoles] : defaults.reportAccessRoles,
        passwordPolicy: settings?.passwordPolicy ? { ...settings.passwordPolicy } : defaults.passwordPolicy,
    };
}

function normalizeTenantAdminCreationPolicy(policy: TenantAdminCreationPolicy): TenantAdminCreationPolicy {
    if (!Number.isInteger(policy.tenantId) || typeof policy.allowAdminsCreateAdmins !== 'boolean') {
        throw new Error('Backend returned an invalid tenant admin-creation policy.');
    }

    return policy;
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

interface BackendTenantSettings {
    tenantId?: number;
    allowAdminsCreateAdmins?: boolean;
    assignmentMode?: AdminSettings['assignmentMode'];
    manager?: string;
    slaEnabled?: boolean;
    slaMode?: AdminSettings['slaMode'];
    workingHours?: string;
    workingDays?: string[];
    holidays?: string[];
    criticalities?: string[];
    statuses?: { name: string; pausesSla: boolean; immutable: boolean }[];
    statusTransitions?: Record<string, string[]>;
    portalSlaFields?: string[];
    portalAccounts?: string[];
    engineers?: string[];
    tenantMode?: AdminSettings['tenantMode'];
    passwordPolicy?: AdminSettings['passwordPolicy'];
    reportAccessRoles?: string[];
}

interface BackendAssignmentSetting {
    mode?: string;
    manualAssignerUserId?: number | null;
    manualAssignerName?: string | null;
    activeAgentsCount?: number;
}

function toAssignmentSetting(setting: BackendAssignmentSetting): AssignmentSetting {
    return {
        mode: setting.mode === 'manual' ? 'manual' : 'auto',
        manualAssignerUserId: setting.manualAssignerUserId ?? null,
        manualAssignerName: setting.manualAssignerName ?? null,
        activeAgentsCount: setting.activeAgentsCount ?? 0,
    };
}

interface BackendAsset {
    id: number;
    inventoryNumber?: string | null;
    serialNumber?: string | null;
    name?: string;
    assetClassId?: number | null;
    assetClassName?: string | null;
    category?: string | null;
    lifecycleStage?: string;
    lifecycleStageName?: string;
    assignedUserId?: number | null;
    assignedUserName?: string | null;
    warehouseId?: number | null;
    warehouseName?: string | null;
    locationId?: number | null;
    locationName?: string | null;
    departmentId?: number | null;
    departmentName?: string | null;
    licenseExpirationDate?: string | null;
    createdAt?: string;
    attributes?: Record<string, string> | null;
}

interface BackendAssetDetail {
    asset: BackendAsset;
    history?: {
        id: number;
        actionType?: string;
        actionTypeName?: string;
        performedBy?: string;
        details?: string | null;
        ticketId?: number | null;
        comment?: string | null;
        createdAt?: string;
    }[];
    movements?: {
        id: number;
        movementType?: string;
        movementTypeName?: string;
        fromUserId?: number | null;
        fromUserName?: string | null;
        toUserId?: number | null;
        toUserName?: string | null;
        fromWarehouseId?: number | null;
        toWarehouseId?: number | null;
        fromLocationId?: number | null;
        toLocationId?: number | null;
        fromDepartmentId?: number | null;
        toDepartmentId?: number | null;
        performedByName?: string;
        performedAt?: string;
        comment?: string | null;
    }[];
    relatedTickets?: BackendAssetTicket[];
}

interface BackendAssetTicket {
    id: number;
    typeId: number;
    typeName: string;
    title: string;
    creatorId: number;
    creatorName: string;
    requesterId?: number | null;
    requesterName?: string | null;
    assigneeId?: number | null;
    assigneeName?: string | null;
    statusId: number;
    statusName: string;
    criticalityLevelId?: number | null;
    criticalityName?: string | null;
    createdAt: string;
    closedAt?: string | null;
}

interface BackendTicket {
    id?: number;
    ticketNumber?: string;
    title?: string;
    description?: string | null;
    typeId?: number;
    typeName?: string;
    statusId?: number;
    statusName?: string;
    priority?: string | null;
    creatorId?: number;
    creatorName?: string;
    requesterId?: number | null;
    requesterName?: string | null;
    assigneeId?: number | null;
    assigneeName?: string | null;
    assetId?: number | null;
    customFieldsData?: Record<string, unknown> | null;
    createdAt?: string;
    updatedAt?: string;
    slaReactionDeadline?: string | null;
    slaResolutionDeadline?: string | null;
}

interface BackendTicketLookups {
    types: { id: number; code: string; name: string }[];
    statuses: { id: number; code: string; name: string }[];
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

interface BackendTicketHistory {
    id: number;
    event: string;
    actor: string;
    createdAt: string;
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

const ASSET_MOVEMENT_TYPES: Record<string, string> = {
    'Перемещение между складами': 'relocate',
    'Выдача со склада': 'issue',
    'Возврат на склад': 'return',
};

const ASSET_LIFECYCLE_LABELS: Record<string, AssetLifecycle> = {
    Purchased: 'Закуплен',
    InStock: 'На складе',
    InUse: 'В эксплуатации',
    InRepair: 'В ремонте',
    Decommissioned: 'Списан',
};

function toAssetRecord(asset: BackendAsset): AssetRecord {
    const stage = asset.lifecycleStage ?? 'Purchased';

    return {
        id: asset.inventoryNumber ?? '',
        recordId: String(asset.id),
        name: asset.name ?? '',
        owner: asset.assignedUserName ?? (asset.assignedUserId != null ? `Сотрудник #${asset.assignedUserId}` : '—'),
        status: asset.lifecycleStageName ?? stage,
        type: asset.assetClassName ?? asset.category ?? '',
        className: asset.assetClassName ?? undefined,
        lifecycle: ASSET_LIFECYCLE_LABELS[stage] ?? (stage as AssetLifecycle),
        warehouse: asset.warehouseName ?? (asset.warehouseId != null ? `Склад #${asset.warehouseId}` : undefined),
        location: asset.locationName ?? (asset.locationId != null ? `Место #${asset.locationId}` : undefined),
        department: asset.departmentName ?? (asset.departmentId != null ? `Подразделение #${asset.departmentId}` : undefined),
        inventoryNumber: asset.inventoryNumber ?? undefined,
        serialNumber: asset.serialNumber ?? undefined,
        assetClassId: asset.assetClassId ?? undefined,
        warehouseId: asset.warehouseId ?? null,
        locationId: asset.locationId ?? null,
        departmentId: asset.departmentId ?? null,
        assignedUserId: asset.assignedUserId ?? null,
        attributes: asset.attributes ?? {},
    };
}

function toAssetDetails(detail: BackendAssetDetail): AssetDetails {
    return {
        ...toAssetRecord(detail.asset),
        history: (detail.history ?? []).map(entry => ({
            id: String(entry.id),
            event: entry.comment ?? entry.actionTypeName ?? entry.actionType ?? '',
            actor: entry.performedBy ?? '',
            details: entry.details ?? '',
            createdAt: entry.createdAt ?? '',
        })),
        movements: (detail.movements ?? []).map(movement => {
            const [fromComment, toComment] = movement.comment?.includes(' → ')
                ? movement.comment.split(' → ', 2)
                : [];
            return {
                id: String(movement.id),
                kind: movement.movementTypeName ?? movement.movementType ?? '',
                movementType: movement.movementType,
                from: fromComment ?? movementTarget(
                    movement.fromUserName,
                    movement.fromUserId,
                    movement.fromWarehouseId,
                    movement.fromLocationId,
                    movement.fromDepartmentId,
                ),
                to: toComment ?? movementTarget(
                    movement.toUserName,
                    movement.toUserId,
                    movement.toWarehouseId,
                    movement.toLocationId,
                    movement.toDepartmentId,
                ),
                actor: movement.performedByName ?? '',
                createdAt: movement.performedAt ?? '',
                fromUserId: movement.fromUserId,
                toUserId: movement.toUserId,
                fromWarehouseId: movement.fromWarehouseId,
                toWarehouseId: movement.toWarehouseId,
                fromLocationId: movement.fromLocationId,
                toLocationId: movement.toLocationId,
                fromDepartmentId: movement.fromDepartmentId,
                toDepartmentId: movement.toDepartmentId,
            };
        }),
        relatedTickets: (detail.relatedTickets ?? []).map(toAssetTicket),
    };
}

function movementTarget(
    userName?: string | null,
    userId?: number | null,
    warehouseId?: number | null,
    locationId?: number | null,
    departmentId?: number | null,
): string {
    if (userName) return userName;
    if (userId != null) return `Сотрудник #${userId}`;
    if (warehouseId != null) return `Склад #${warehouseId}`;
    if (locationId != null) return `Место #${locationId}`;
    if (departmentId != null) return `Подразделение #${departmentId}`;
    return '—';
}

function toAssetTicket(ticket: BackendAssetTicket): Ticket {
    return {
        id: String(ticket.id),
        title: ticket.title,
        requester: ticket.requesterName ?? ticket.creatorName,
        team: '',
        priority: ticket.criticalityName ?? '',
        status: ticket.statusName,
        createdAt: ticket.createdAt,
        slaRemaining: '',
        type: ticket.typeName,
        typeId: ticket.typeId,
        statusId: ticket.statusId,
        assignee: ticket.assigneeName ?? undefined,
        assigneeId: ticket.assigneeId ?? undefined,
        createdOn: ticket.createdAt,
        closedOn: ticket.closedAt ?? undefined,
        requesterId: ticket.requesterId ?? undefined,
        creatorName: ticket.creatorName,
    };
}

function toTicket(ticket: BackendTicket): Ticket {
    const fields = toScalarFields(ticket.customFieldsData);
    const isClosed = (ticket.statusName ?? '').toLocaleLowerCase().includes('закрыт');

    return {
        id: String(ticket.id ?? ''),
        ticketNumber: ticket.ticketNumber ?? undefined,
        title: ticket.title ?? '',
        requester: ticket.requesterName ?? ticket.creatorName ?? '',
        team: '',
        priority: ticket.priority ?? '',
        status: ticket.statusName ?? '',
        createdAt: ticket.createdAt ?? '',
        slaRemaining: formatRemaining(ticket.slaResolutionDeadline),
        type: ticket.typeName,
        typeId: ticket.typeId,
        statusId: ticket.statusId,
        assignee: ticket.assigneeName ?? undefined,
        assigneeId: ticket.assigneeId ?? undefined,
        createdOn: ticket.createdAt,
        closedOn: isClosed ? ticket.updatedAt : undefined,
        reactionSla: formatRemaining(ticket.slaReactionDeadline),
        resolutionSla: formatRemaining(ticket.slaResolutionDeadline),
        assetId: ticket.assetId != null ? String(ticket.assetId) : undefined,
        requesterId: ticket.requesterId ?? undefined,
        creatorName: ticket.creatorName,
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
        attachmentRecords: (message.attachments ?? [])
            .filter((attachment): attachment is { id: number; fileName: string } =>
                attachment.id != null && !!attachment.fileName,
            )
            .map(attachment => ({ id: String(attachment.id), fileName: attachment.fileName })),
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
