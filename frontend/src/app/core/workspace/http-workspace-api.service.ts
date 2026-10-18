import { Injectable, inject } from '@angular/core';
import { forkJoin, map, Observable, of, switchMap, throwError } from 'rxjs';
import { HttpClient } from '@angular/common/http';
import { WorkspaceApi } from './workspace-api';
import { AuthService } from '../auth/auth.service';
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

interface ContractFormField {
    code: string;
    name: string;
    fieldType: string;
    isRequired: boolean;
    isEngineerOnly: boolean;
    defaultValue?: string | null;
    hint?: string | null;
    dependencies?: {
        dependsOnFieldCode: string;
        condition: string;
        value?: string | null;
    }[];
}

@Injectable()
export class HttpWorkspaceApiService extends WorkspaceApi {
    private readonly http = inject(HttpClient);
    private readonly auth = inject(AuthService);
    private readonly baseUrl = environment.apiBaseUrl;

    getOverview() {
        return this.getTickets().pipe(map(tickets => {
            const openTickets = tickets.filter(ticket => ticket.status !== 'Закрыта');
            const resolvedTickets = tickets.filter(ticket => ticket.status === 'Закрыта');
            const today = new Date();
            const week = Array.from({ length: 7 }, (_, index) => {
                const date = new Date(today);
                date.setDate(today.getDate() - (6 - index));
                const dateKey = date.toISOString().slice(0, 10);
                const incoming = tickets.filter(ticket => this.ticketDate(ticket) === dateKey).length;
                const resolved = resolvedTickets.filter(ticket => this.closedTicketDate(ticket) === dateKey).length;
                return { label: date.toLocaleDateString('ru-RU', { weekday: 'short' }), incoming, resolved };
            });
            const maximum = Math.max(1, ...week.flatMap(point => [point.incoming, point.resolved]));
            const series = week.map(point => ({
                label: point.label,
                incoming: Math.round(point.incoming / maximum * 100),
                resolved: Math.round(point.resolved / maximum * 100),
            }));
            const metrics = [
                { label: 'Всего заявок', value: String(tickets.length), change: '—' },
                { label: 'Открытые заявки', value: String(openTickets.length), change: '—' },
                { label: 'Закрытые заявки', value: String(resolvedTickets.length), change: '—' },
                { label: 'Заявки за неделю', value: String(week.reduce((sum, point) => sum + point.incoming, 0)), change: '—' },
            ];
            return { metrics, series, recentTickets: tickets.slice(0, 5), activeEscalations: null } satisfies OverviewData;
        }));
    }
    getTickets() { return this.http.get<Ticket[]>(`${this.baseUrl}/tickets`); }
    getTicket(ticketId: string) {
        const id = this.numericId(ticketId, 'Заявка');
        return forkJoin({
            ticket: this.http.get<Ticket>(`${this.baseUrl}/tickets/${id}`),
            comments: this.http.get<TicketComment[]>(`${this.baseUrl}/tickets/${id}/messages`),
        }).pipe(map(({ ticket, comments }) => ({ ...ticket, comments })));
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
    getEscalations() { return this.notSupported<Escalation[]>('Эскалации: в контракте нет метода получения списка активных эскалаций'); }
    createTicket(request: CreateTicketRequest) {
        return this.getTicketTypes().pipe(
            map(types => {
                if (!request.title.trim() || !request.description?.trim() || !request.priority?.trim()) {
                    throw new Error('Создание заявки требует темы, описания и критичности согласно API-контракту.');
                }
                const typeName = request.type ?? request.category;
                const type = types.find(item => item.name === typeName);
                if (!type) throw new Error(`Тип заявки «${typeName ?? ''}» отсутствует в API.`);
                return {
                    title: request.title,
                    description: request.description ?? '',
                    typeId: this.numericId(type.id, 'Тип заявки'),
                    priority: request.priority ?? '',
                    ...(request.assetId ? { assetId: this.numericId(request.assetId, 'Актив') } : {}),
                    ...(request.fields ? { customFieldsData: request.fields } : {}),
                };
            }),
            switchMap(body => this.http.post<Ticket>(`${this.baseUrl}/tickets`, body)),
        );
    }
    getTicketTypes() {
        return forkJoin({
            types: this.http.get<{ id: number; name: string; isPortalAvailable: boolean; isBuiltIn: boolean }[]>(`${this.baseUrl}/ticket-types`),
            policies: this.http.get<{ ticketTypeId: number; criticalityLevelId: number; reactionTimeMinutes: number; resolutionTimeMinutes: number; isDisabled: boolean }[]>(`${this.baseUrl}/sla-policies`),
            criticalities: this.http.get<{ id: number; name: string }[]>(`${this.baseUrl}/criticality-levels`),
        }).pipe(map(({ types, policies, criticalities }) => types.map(type => {
            const typePolicies = policies.filter(policy => policy.ticketTypeId === type.id);
            const slaByCriticality = Object.fromEntries(typePolicies.map(policy => {
                const criticality = criticalities.find(level => level.id === policy.criticalityLevelId);
                return criticality ? [criticality.name, {
                    reaction: this.formatSlaMinutes(policy.reactionTimeMinutes),
                    resolution: this.formatSlaMinutes(policy.resolutionTimeMinutes),
                }] : [];
            }));
            const representativePolicy = typePolicies.find(policy => !policy.isDisabled) ?? typePolicies[0];
            return {
                id: String(type.id),
                name: type.name,
                portalAvailable: type.isPortalAvailable,
                repair: type.isBuiltIn && /repair|ремонт/i.test(type.name),
                slaEnabled: typePolicies.some(policy => !policy.isDisabled),
                reactionSla: representativePolicy ? this.formatSlaMinutes(representativePolicy.reactionTimeMinutes) : '',
                resolutionSla: representativePolicy ? this.formatSlaMinutes(representativePolicy.resolutionTimeMinutes) : '',
                slaByCriticality,
            } satisfies TicketType;
        })),
        );
    }
    saveTicketTypes(ticketTypes: TicketType[]) {
        return this.http.get<{ id: number; name: string; sortOrder: number; isActive: boolean }[]>(`${this.baseUrl}/criticality-levels`).pipe(
            switchMap(criticalities => forkJoin(ticketTypes.map(type => {
                const existingId = Number(type.id);
                const typeRequest = Number.isSafeInteger(existingId) && existingId > 0
                    ? this.http.put<{ id: number }>(`${this.baseUrl}/ticket-types/${existingId}`, { name: type.name, isPortalAvailable: type.portalAvailable })
                    : this.http.post<{ id: number }>(`${this.baseUrl}/ticket-types`, {
                        code: this.apiCode(type.name),
                        name: type.name,
                        isPortalAvailable: type.portalAvailable,
                    });
                return typeRequest.pipe(switchMap(savedType => {
                    const typeId = savedType.id ?? existingId;
                    if (!Number.isSafeInteger(typeId) || typeId <= 0) throw new Error(`Тип «${type.name}» не вернул числовой ID.`);
                    const updates: Observable<unknown>[] = [
                        this.http.put(`${this.baseUrl}/sla-settings/ticket-types/${typeId}`, { isEnabled: type.slaEnabled }),
                    ];
                    if (type.slaEnabled) {
                        for (const criticality of criticalities.filter(level => level.isActive)) {
                            const value = type.slaByCriticality?.[criticality.name];
                            if (!value) throw new Error(`Не заданы SLA для критичности «${criticality.name}» типа «${type.name}».`);
                            updates.push(this.http.put(`${this.baseUrl}/sla-policies`, {
                                ticketTypeId: typeId,
                                criticalityLevelId: criticality.id,
                                reactionTimeMinutes: this.parseSlaMinutes(value.reaction),
                                resolutionTimeMinutes: this.parseSlaMinutes(value.resolution),
                                isDisabled: false,
                            }));
                        }
                    }
                    return forkJoin(updates).pipe(map(() => type));
                }));
            }))),
        );
    }
    changeTicketStatus(ticketId: string, status: string) {
        return this.http.get<{ id: number; name: string }[]>(`${this.baseUrl}/ticket-statuses`).pipe(
            map(statuses => {
                const target = statuses.find(item => item.name === status);
                if (!target) throw new Error(`Статус «${status}» отсутствует в API.`);
                return target.id;
            }),
            switchMap(statusId => this.http.put<Ticket>(`${this.baseUrl}/tickets/${this.numericId(ticketId, 'Заявка')}/status`, { statusId })),
        );
    }
    assignTicket(ticketId: string, assignee: string) {
        return this.http.get<{ id: number; username: string; fullName: string }[]>(`${this.baseUrl}/users`).pipe(
            map(users => {
                const user = users.find(item => item.fullName === assignee || item.username === assignee);
                if (!user) throw new Error(`Исполнитель «${assignee}» не найден в API.`);
                return user.id;
            }),
            switchMap(assigneeId => this.http.post<Ticket>(`${this.baseUrl}/tickets/${this.numericId(ticketId, 'Заявка')}/assign`, { assigneeId })),
        );
    }
    changeTicketPriority(ticketId: string, priority: string) {
        return this.http.put<Ticket>(`${this.baseUrl}/tickets/${this.numericId(ticketId, 'Заявка')}`, { priority });
    }
    addTicketComment(ticketId: string, comment: Omit<TicketComment, 'id' | 'createdAt'>) {
        return this.http.post<TicketComment>(`${this.baseUrl}/tickets/${this.numericId(ticketId, 'Заявка')}/messages`, {
            messageBody: comment.message,
            isInternal: comment.internal,
            attachments: comment.attachments,
        });
    }
    escalateTicket(ticketId: string, _initiator: string) {
        return this.http.post<{
            fromCriticalityLevelId: number;
            toCriticalityLevelId: number;
            initiatorUserId: number;
            notifiedUserIds: number[];
            escalatedAt: string;
        }>(`${this.baseUrl}/tickets/${this.numericId(ticketId, 'Заявка')}/escalate`, null).pipe(
            switchMap(escalation => forkJoin({
                criticalities: this.http.get<{ id: number; name: string }[]>(`${this.baseUrl}/criticality-levels`),
                users: this.http.get<{ id: number; fullName: string }[]>(`${this.baseUrl}/users`),
            }).pipe(map(({ criticalities, users }) => {
                const previousPriority = criticalities.find(level => level.id === escalation.fromCriticalityLevelId)?.name;
                const newPriority = criticalities.find(level => level.id === escalation.toCriticalityLevelId)?.name;
                const initiator = users.find(user => user.id === escalation.initiatorUserId)?.fullName;
                if (!previousPriority || !newPriority || !initiator) {
                    throw new Error('Ответ эскалации содержит неизвестного пользователя или уровень критичности.');
                }
                const recipients = escalation.notifiedUserIds.map(id => {
                    const recipient = users.find(user => user.id === id);
                    if (!recipient) throw new Error(`Получатель уведомления с ID ${id} не найден.`);
                    return recipient.fullName;
                });
                return {
                    ticketId,
                    initiator,
                    createdAt: escalation.escalatedAt,
                    previousPriority,
                    newPriority,
                    recipients,
                } satisfies EscalationHistoryEntry;
            }))),
        );
    }
    getAssetDetails(assetId: string) {
        const id = this.numericId(assetId, 'Актив');
        return forkJoin({
            asset: this.http.get<AssetDetails>(`${this.baseUrl}/assets/${id}`),
            history: this.http.get<{
                id: number;
                actionType: string;
                description: string;
                performedBy: string;
                createdAt: string;
                details?: Record<string, unknown>;
                comment?: string;
            }[]>(`${this.baseUrl}/assets/${id}/history`),
            relatedTickets: this.http.get<{
                id: number;
                typeName: string;
                title: string;
                creatorName: string;
                assigneeName?: string;
                statusName: string;
                criticalityName: string | null;
                createdAt: string;
                closedAt?: string;
            }[]>(`${this.baseUrl}/assets/${id}/tickets`),
        }).pipe(map(({ asset, history, relatedTickets }) => ({
            ...asset,
            recordId: String(id),
            history: history.map(item => ({
                id: String(item.id),
                event: item.actionType,
                actor: item.performedBy,
                createdAt: item.createdAt,
                details: [item.description, item.details ? JSON.stringify(item.details) : '', item.comment].filter(Boolean).join(' · '),
            })),
            relatedTickets: relatedTickets.map(ticket => ({
                id: String(ticket.id),
                title: ticket.title,
                requester: ticket.creatorName,
                team: ticket.assigneeName ?? '',
                priority: ticket.criticalityName ?? '',
                status: ticket.statusName,
                createdAt: ticket.createdAt,
                createdOn: ticket.createdAt,
                closedOn: ticket.closedAt,
                slaRemaining: '',
                type: ticket.typeName,
                assignee: ticket.assigneeName ?? undefined,
            })),
        } satisfies AssetDetails)));
    }
    createAsset(request: CreateAssetRequest) {
        return this.getAssetClasses().pipe(
            map(classes => {
                const assetClass = classes.find(item => item.name === request.className);
                if (!assetClass) throw new Error(`Класс актива «${request.className}» не найден в API.`);
                if ([request.owner, request.location, request.department].some(value => value?.trim())) {
                    throw new Error('API создания актива не принимает владельца, местоположение или подразделение; задайте эти данные через поддерживаемые атрибуты класса.');
                }
                const warehouseId = request.warehouse ? this.numericId(request.warehouse, 'Склад') : undefined;
                return {
                    ...(request.id ? { inventoryNumber: request.id } : {}),
                    name: request.name,
                    assetClassId: assetClass.id,
                    ...(warehouseId ? { warehouseId } : {}),
                    customAttributes: request.attributes ?? {},
                };
            }),
            switchMap(body => this.http.post<AssetDetails>(`${this.baseUrl}/assets`, body)),
        );
    }
    updateAsset(assetId: string, changes: Partial<CreateAssetRequest>) {
        if (changes.name === undefined) return this.notSupported<AssetDetails>('Обновление актива требует name согласно API-контракту');
        const url = `${this.baseUrl}/assets/${this.numericId(assetId, 'Актив')}`;
        return this.http.get<AssetDetails>(url).pipe(switchMap(current => {
            const unsupportedChanges = [
                changes.id !== undefined && changes.id !== current.id,
                changes.owner !== undefined && changes.owner !== current.owner,
                changes.warehouse !== undefined && changes.warehouse !== current.warehouse,
                changes.location !== undefined && changes.location !== current.location,
                changes.department !== undefined && changes.department !== current.department,
            ];
            if (unsupportedChanges.some(Boolean)) {
                return this.notSupported<AssetDetails>('Изменение инвентарного номера, владельца, склада, места или подразделения отсутствует в UpdateAssetRequest.');
            }
            return this.http.put<AssetDetails>(url, {
                name: changes.name,
                ...(changes.attributes !== undefined ? { customAttributes: changes.attributes } : {}),
            });
        }));
    }
    deleteAsset(assetId: string) { return this.http.delete<void>(`${this.baseUrl}/assets/${this.numericId(assetId, 'Актив')}`); }
    moveAsset(assetId: string, request: AssetMovementRequest) {
        const isUserAssignment = /сотруднику/i.test(request.kind);
        return this.http.post<AssetDetails>(`${this.baseUrl}/assets/${this.numericId(assetId, 'Актив')}/movements`, {
            movementType: request.kind,
            fromSourceId: this.numericId(request.from, 'Источник перемещения'),
            ...(isUserAssignment
                ? { assignedUserId: this.numericId(request.to, 'Исполнитель') }
                : { toDestinationId: this.numericId(request.to, 'Назначение перемещения') }),
        });
    }
    createRepairTicket(assetId: string, title: string) {
        return forkJoin({
            types: this.getTicketTypes(),
            criticalities: this.http.get<{ name: string; sortOrder: number; isActive: boolean }[]>(`${this.baseUrl}/criticality-levels`),
        }).pipe(
            map(({ types, criticalities }) => {
                const repairType = types.find(type => type.repair || /repair|ремонт/i.test(type.name));
                if (!repairType) throw new Error('В API не найден тип заявки на ремонт.');
                const priority = criticalities.filter(level => level.isActive).sort((left, right) => left.sortOrder - right.sortOrder)[0]?.name;
                if (!priority) throw new Error('В API не настроен активный уровень критичности.');
                return {
                    title,
                    description: title,
                    typeId: this.numericId(repairType.id, 'Тип заявки'),
                    priority,
                    assetId: this.numericId(assetId, 'Актив'),
                };
            }),
            switchMap(body => this.http.post<Ticket>(`${this.baseUrl}/tickets`, body)),
        );
    }
    closeRepairTicket(ticketId: string, request: RepairCloseRequest) {
        return this.http.get<{ id: number; name: string }[]>(`${this.baseUrl}/ticket-statuses`).pipe(
            map(statuses => {
                const closed = statuses.find(status => status.name === 'Закрыта');
                if (!closed) throw new Error('Статус «Закрыта» не найден в API.');
                return closed.id;
            }),
            switchMap(statusId => this.http.put<Ticket>(`${this.baseUrl}/tickets/${this.numericId(ticketId, 'Заявка')}/status`, {
                statusId,
                repairResolution: request.workDescription,
                repairOutcome: request.result,
            })),
        );
    }
    getForms() {
        return this.getTicketTypes().pipe(switchMap(types => {
            if (!types.length) return of([] as TicketFormDefinition[]);
            return forkJoin(types.map(type => this.http.get<{ fieldsSchema: { fields: ContractFormField[] } }>(
                `${this.baseUrl}/form-templates/${this.numericId(type.id, 'Тип заявки')}`,
            ).pipe(map(form => ({
                ticketTypeId: type.id,
                ticketTypeName: type.name,
                fields: form.fieldsSchema.fields.map(field => this.toFormField(field)),
            })))));
        }));
    }
    saveForm(form: TicketFormDefinition) {
        return this.http.put<TicketFormDefinition>(`${this.baseUrl}/form-templates/${this.numericId(form.ticketTypeId, 'Тип заявки')}`, {
            fieldsSchema: {
                fields: form.fields.map(field => ({
                    code: field.id,
                    name: field.name,
                    fieldType: field.type === 'textarea' ? 'text' : field.type === 'file' ? 'attachment' : field.type,
                    isRequired: field.required,
                    isEngineerOnly: !field.visibleToRequester,
                    defaultValue: field.defaultValue,
                    hint: field.hint,
                    sortOrder: 0,
                    visibilities: [],
                    dependencies: field.conditionFieldId && field.conditionValue !== undefined
                        ? [{ dependsOnFieldCode: field.conditionFieldId, condition: 'Equals', value: field.conditionValue }]
                        : [],
                })),
            },
        });
    }
    getEscalationPolicies() {
        return this.http.get<{ ticketTypeId: number; onSlaBreach: boolean; onManual: boolean; isActive: boolean }[]>(`${this.baseUrl}/escalation-rules`).pipe(
            switchMap(rules => this.getTicketTypes().pipe(map(types => rules.map(rule => ({
                ticketTypeId: String(rule.ticketTypeId),
                ticketTypeName: types.find(type => Number(type.id) === rule.ticketTypeId)?.name ?? String(rule.ticketTypeId),
                onResolutionBreach: rule.onSlaBreach,
                allowManualByRequester: rule.onManual,
                allowManualByAgent: rule.onManual,
                manager: '',
            }))))),
        );
    }
    saveEscalationPolicies(policies: EscalationPolicy[]) {
        if (policies.some(policy => policy.allowManualByRequester !== policy.allowManualByAgent)) {
            return this.notSupported<EscalationPolicy[]>('API поддерживает один флаг ручной эскалации, а форма задаёт отдельные права автора и агента.');
        }
        return forkJoin(policies.map(policy => this.http.put(
            `${this.baseUrl}/escalation-rules/${this.numericId(policy.ticketTypeId, 'Тип заявки')}`,
            { onSlaBreach: policy.onResolutionBreach, onManual: policy.allowManualByAgent, isActive: true },
        ))).pipe(map(() => policies));
    }
    getEscalationHistory() { return this.notSupported<EscalationHistoryEntry[]>('В API_CONTRACT.json отсутствует операция получения истории эскалаций'); }
    getPortalSettings() {
        return forkJoin({
            criticalities: this.http.get<{ name: string; sortOrder: number; isActive: boolean }[]>(`${this.baseUrl}/criticality-levels`),
            visibility: this.http.get<{ showReactionTime: boolean; showResolutionTime: boolean }>(`${this.baseUrl}/sla-settings/portal-visibility`),
        }).pipe(map(({ criticalities, visibility }) => ({
            criticalities: criticalities.filter(level => level.isActive).sort((left, right) => left.sortOrder - right.sortOrder).map(level => level.name),
            portalSlaFields: [
                ...(visibility.showReactionTime ? ['Время реакции'] : []),
                ...(visibility.showResolutionTime ? ['Время решения'] : []),
            ],
        })));
    }
    getAdminSettings() {
        return forkJoin({
            assignment: this.http.get<{ mode: 'AutomaticLeastLoaded' | 'Manual'; manualAssignerUserId?: number | null }>(`${this.baseUrl}/assignment-settings`),
            sla: this.http.get<{ isEnabled: boolean; activeWorkingScheduleId?: number | null }>(`${this.baseUrl}/sla-settings`),
            schedules: this.http.get<{ id: number; name: string; scheduleType: '24x7' | '8x5'; isActive: boolean; hours: { dayOfWeek: number; startTime: string; endTime: string }[] }[]>(`${this.baseUrl}/working-schedules`),
            exceptions: this.http.get<{ id: number; exceptionDate: string; isWorkDay: boolean }[]>(`${this.baseUrl}/calendar-exceptions`),
            criticalities: this.http.get<{ id: number; name: string; sortOrder: number; isActive: boolean }[]>(`${this.baseUrl}/criticality-levels`),
            statuses: this.http.get<{ id: number; name: string; isSlaPausing: boolean; sortOrder: number; isBuiltIn: boolean }[]>(`${this.baseUrl}/ticket-statuses`),
            transitions: this.http.get<{ fromStatusId: number; toStatusId: number }[]>(`${this.baseUrl}/ticket-status-transitions`),
            portalVisibility: this.http.get<{ showReactionTime: boolean; showResolutionTime: boolean }>(`${this.baseUrl}/sla-settings/portal-visibility`),
            reportAccess: this.reportAccessSettings(),
            passwordPolicy: this.http.get<{ minLength: number; requireUppercase: boolean; requireSpecial: boolean; expirationDays: number; maxFailedAttempts: number; lockoutMinutes: number }>(`${this.baseUrl}/security-settings`),
            serviceDeskHead: this.http.get<{ userId: number; login: string; fullName: string }>(`${this.baseUrl}/ticket-configuration/service-desk-head`),
            users: this.http.get<UserRecord[]>(`${this.baseUrl}/users`),
            roles: this.http.get<SystemRoleOption[]>(`${this.baseUrl}/roles`),
        }).pipe(map(data => {
            const schedule = data.schedules.find(item => item.id === data.sla.activeWorkingScheduleId)
                ?? data.schedules.find(item => item.isActive);
            const statusById = new Map(data.statuses.map(status => [status.id, status.name]));
            const statusTransitions: Record<string, string[]> = {};
            for (const transition of data.transitions) {
                const source = statusById.get(transition.fromStatusId);
                const target = statusById.get(transition.toStatusId);
                if (!source || !target) throw new Error('API вернул переход статуса со ссылкой на неизвестный статус.');
                statusTransitions[source] = [...new Set([...(statusTransitions[source] ?? []), target])];
            }
            const dayNames = ['Вс', 'Пн', 'Вт', 'Ср', 'Чт', 'Пт', 'Сб'];
            const engineerRoleIds = new Set(data.roles
                .filter(role => ['agent', 'engineer'].includes(role.code.toLocaleLowerCase()))
                .map(role => role.id));
            const head = data.users.find(user => user.id === data.serviceDeskHead.userId);
            const manager = head?.fullName ?? data.serviceDeskHead.fullName;
            return {
                assignmentMode: data.assignment.mode === 'Manual' ? 'manual' : 'automatic',
                manager,
                slaEnabled: data.sla.isEnabled,
                slaMode: schedule?.scheduleType === '8x5' ? 'working-hours' : '24/7',
                workingHours: [...new Set(schedule?.hours.map(hour => `${hour.startTime}-${hour.endTime}`) ?? [])].join(', '),
                workingDays: [...new Set(schedule?.hours.map(hour => dayNames[hour.dayOfWeek]).filter((day): day is string => !!day) ?? [])],
                holidays: data.exceptions.filter(exception => !exception.isWorkDay).map(exception => exception.exceptionDate),
                criticalities: data.criticalities.filter(level => level.isActive).sort((left, right) => left.sortOrder - right.sortOrder).map(level => level.name),
                statuses: data.statuses.sort((left, right) => left.sortOrder - right.sortOrder).map(status => ({
                    name: status.name,
                    pausesSla: status.isSlaPausing,
                    immutable: status.isBuiltIn,
                })),
                statusTransitions,
                portalSlaFields: [
                    ...(data.portalVisibility.showReactionTime ? ['Время реакции'] : []),
                    ...(data.portalVisibility.showResolutionTime ? ['Время решения'] : []),
                ],
                engineers: data.users.filter(user => user.roleIds.some(roleId => engineerRoleIds.has(roleId))).map(user => user.fullName),
                passwordPolicy: {
                    minLength: data.passwordPolicy.minLength,
                    requireUppercase: data.passwordPolicy.requireUppercase,
                    requireSpecial: data.passwordPolicy.requireSpecial,
                    expiryDays: data.passwordPolicy.expirationDays,
                    maxFailedAttempts: data.passwordPolicy.maxFailedAttempts,
                    lockoutMinutes: data.passwordPolicy.lockoutMinutes,
                },
                reportAccessRoles: [...new Set(data.reportAccess.flatMap(item => item.allowedRoleCodes))],
            } satisfies AdminSettings;
        }));
    }
    saveAdminSettings(settings: AdminSettings) {
        return forkJoin({
            sla: this.http.get<{ isEnabled: boolean; activeWorkingScheduleId?: number | null }>(`${this.baseUrl}/sla-settings`),
            assignment: this.http.get<{ mode: 'AutomaticLeastLoaded' | 'Manual'; manualAssignerUserId?: number | null }>(`${this.baseUrl}/assignment-settings`),
            schedules: this.http.get<{ id: number; name: string; scheduleType: '24x7' | '8x5'; isActive: boolean; hours: { dayOfWeek: number; startTime: string; endTime: string }[] }[]>(`${this.baseUrl}/working-schedules`),
            exceptions: this.http.get<{ id: number; exceptionDate: string; isWorkDay: boolean }[]>(`${this.baseUrl}/calendar-exceptions`),
            criticalities: this.http.get<{ id: number; name: string; sortOrder: number; isActive: boolean }[]>(`${this.baseUrl}/criticality-levels`),
            statuses: this.http.get<{ id: number; name: string; isSlaPausing: boolean; sortOrder: number; isBuiltIn: boolean }[]>(`${this.baseUrl}/ticket-statuses`),
            reportAccess: this.reportAccessSettings(),
            passwordPolicy: this.http.get<{ minLength: number; requireDigit: boolean; requireUppercase: boolean; requireLowercase: boolean; requireSpecial: boolean; expirationDays: number; maxFailedAttempts: number; lockoutMinutes: number }>(`${this.baseUrl}/security-settings`),
            users: this.http.get<UserRecord[]>(`${this.baseUrl}/users`),
        }).pipe(switchMap(current => {
            const manager = current.users.find(user => user.fullName === settings.manager || user.username === settings.manager);
            if (!manager) return this.notSupported<AdminSettings>(`Руководитель «${settings.manager}» не найден среди пользователей API.`);
            const distinctReportAccess = new Set(current.reportAccess.map(item => [...item.allowedRoleCodes].sort().join('|')));
            if (distinctReportAccess.size > 1) {
                return this.notSupported<AdminSettings>('API настраивает доступ отдельно для каждого отчёта, а текущая форма поддерживает только единый список ролей.');
            }
            const schedule = current.schedules.find(item => item.id === current.sla.activeWorkingScheduleId)
                ?? current.schedules.find(item => item.isActive);
            if (!schedule) return this.notSupported<AdminSettings>('В API нет графика, который можно обновить; POST /working-schedules не определён.');

            const mutations: Observable<unknown>[] = [
                this.http.put(`${this.baseUrl}/sla-settings`, { isEnabled: settings.slaEnabled, activeWorkingScheduleId: schedule.id }),
                this.http.put(`${this.baseUrl}/assignment-settings`, {
                    mode: settings.assignmentMode === 'manual' ? 'Manual' : 'AutomaticLeastLoaded',
                    manualAssignerUserId: manager.id,
                }),
                this.http.put(`${this.baseUrl}/ticket-configuration/service-desk-head`, { userId: manager.id }),
                this.http.put(`${this.baseUrl}/sla-settings/portal-visibility`, {
                    showReactionTime: settings.portalSlaFields.includes('Время реакции'),
                    showResolutionTime: settings.portalSlaFields.includes('Время решения'),
                }),
                this.http.put(`${this.baseUrl}/security-settings`, {
                    ...current.passwordPolicy,
                    minLength: settings.passwordPolicy?.minLength ?? current.passwordPolicy.minLength,
                    requireUppercase: settings.passwordPolicy?.requireUppercase ?? current.passwordPolicy.requireUppercase,
                    requireSpecial: settings.passwordPolicy?.requireSpecial ?? current.passwordPolicy.requireSpecial,
                    expirationDays: settings.passwordPolicy?.expiryDays ?? current.passwordPolicy.expirationDays,
                    maxFailedAttempts: settings.passwordPolicy?.maxFailedAttempts ?? current.passwordPolicy.maxFailedAttempts,
                    lockoutMinutes: settings.passwordPolicy?.lockoutMinutes ?? current.passwordPolicy.lockoutMinutes,
                }),
                this.http.put(`${this.baseUrl}/working-schedules/${schedule.id}`, {
                    name: schedule.name,
                    scheduleType: settings.slaMode === '24/7' ? '24x7' : '8x5',
                    isActive: true,
                    hours: this.scheduleHours(settings, schedule.hours),
                }),
                this.http.put(`${this.baseUrl}/report-access`, this.reportTypes().map(reportType => ({
                    reportType,
                    allowedRoleCodes: settings.reportAccessRoles ?? [],
                }))),
            ];

            const orderedStatuses = [...current.statuses].sort((left, right) => left.sortOrder - right.sortOrder);
            if (settings.statuses.length < orderedStatuses.length) {
                return this.notSupported<AdminSettings>('Удаление статусов не поддерживается API; используйте изменение или сохраните все существующие статусы.');
            }
            settings.statuses.forEach((status, index) => {
                const existing = orderedStatuses[index];
                if (existing) {
                    mutations.push(this.http.put(`${this.baseUrl}/ticket-statuses/${existing.id}`, {
                        name: status.name,
                        isSlaPausing: status.pausesSla,
                        sortOrder: index,
                    }));
                } else {
                    mutations.push(this.http.post(`${this.baseUrl}/ticket-statuses`, {
                        code: this.apiCode(status.name),
                        name: status.name,
                        isSlaPausing: status.pausesSla,
                        sortOrder: index,
                    }));
                }
            });

            const orderedCriticalities = [...current.criticalities].sort((left, right) => left.sortOrder - right.sortOrder);
            settings.criticalities.forEach((name, index) => {
                const existing = orderedCriticalities[index];
                if (existing) {
                    mutations.push(this.http.put(`${this.baseUrl}/criticality-levels/${existing.id}`, {
                        name,
                        sortOrder: index,
                        isActive: true,
                    }));
                } else {
                    mutations.push(this.http.post(`${this.baseUrl}/criticality-levels`, {
                        code: this.apiCode(name),
                        name,
                        sortOrder: index,
                    }));
                }
            });
            for (const removed of orderedCriticalities.slice(settings.criticalities.length)) {
                mutations.push(this.http.put(`${this.baseUrl}/criticality-levels/${removed.id}`, {
                    name: removed.name,
                    sortOrder: removed.sortOrder,
                    isActive: false,
                }));
            }

            const savedHolidayDates = new Set(settings.holidays);
            for (const exception of current.exceptions.filter(item => !item.isWorkDay && !savedHolidayDates.has(item.exceptionDate))) {
                mutations.push(this.http.delete(`${this.baseUrl}/calendar-exceptions/${exception.id}`));
            }
            const currentHolidayDates = new Set(current.exceptions.filter(item => !item.isWorkDay).map(item => item.exceptionDate));
            for (const date of settings.holidays.filter(value => !currentHolidayDates.has(value))) {
                mutations.push(this.http.post(`${this.baseUrl}/calendar-exceptions`, {
                    exceptionDate: date,
                    isWorkDay: false,
                    description: '',
                }));
            }

            return forkJoin(mutations).pipe(switchMap(() => this.http.get<{ id: number; name: string }[]>(`${this.baseUrl}/ticket-statuses`).pipe(
                switchMap(statuses => {
                    const statusIds = new Map(statuses.map(status => [status.name, status.id]));
                    const transitions = Object.entries(settings.statusTransitions).flatMap(([from, targets]) => {
                        const fromStatusId = statusIds.get(from);
                        if (!fromStatusId) throw new Error(`Статус «${from}» не найден после сохранения настроек.`);
                        return targets.map(target => {
                            const toStatusId = statusIds.get(target);
                            if (!toStatusId) throw new Error(`Статус «${target}» не найден после сохранения настроек.`);
                            return { fromStatusId, toStatusId };
                        });
                    });
                    return this.http.put(`${this.baseUrl}/ticket-status-transitions`, transitions).pipe(map(() => settings));
                }),
            )));
        }));
    }
    getReports(from?: string, to?: string) {
        return this.reportAccessSettings().pipe(
            map(settings => settings.filter(setting => setting.allowedRoleCodes.includes(this.auth.getRole())).map(setting => setting.reportType)),
            switchMap(reportTypes => reportTypes.length
                ? forkJoin(reportTypes.map(reportType => this.http.get<unknown>(`${this.baseUrl}/reports/${reportType}`, {
                    params: { ...(from ? { dateFrom: from } : {}), ...(to ? { dateTo: to } : {}) },
                }).pipe(map(value => ({ id: reportType, title: reportType, description: '', value: JSON.stringify(value) } satisfies ReportSummary)))))
                : of([] as ReportSummary[])),
        );
    }
    getPortalTickets() { return this.getTickets(); }
    createPortalTicket(request: PortalTicketRequest) {
        if (request.attachments?.length) return this.notSupported<Ticket>('Загрузка файлов не поддерживается контрактом; создайте заявку без вложений');
        return this.createTicket(request);
    }
    addPortalComment(ticketId: string, message: string, attachments: File[]) {
        if (attachments.length) return this.notSupported<TicketComment>('Загрузка файлов не поддерживается контрактом сообщений');
        return this.http.post<TicketComment>(`${this.baseUrl}/tickets/${this.numericId(ticketId, 'Заявка')}/messages`, {
            messageBody: message,
            isInternal: false,
            attachments: [],
        });
    }
    reviewPortalTicket(_ticketId: string, _accepted: boolean) { return this.notSupported<Ticket>('В контракте нет операции подтверждения или возврата заявки порталом'); }
    createPortalAccount(_request: CreatePortalAccountRequest) { return this.notSupported<string>('В контракте нет операции создания портальной учётной записи'); }
    getTenants() {
        return this.http.get<(Omit<TenantRecord, 'id'> & { id: number })[]>(`${this.baseUrl}/tenants`).pipe(
            map(tenants => tenants.map(tenant => ({ ...tenant, id: String(tenant.id) }))),
        );
    }
    createTenant(request: CreateTenantRequest) {
        return this.http.post<(Omit<TenantRecord, 'id'> & { id: number })>(`${this.baseUrl}/tenants`, request).pipe(
            map(tenant => ({ ...tenant, id: String(tenant.id) })),
        );
    }
    setTenantActive(tenantId: string, active: boolean) {
        return this.http.patch<(Omit<TenantRecord, 'id'> & { id: number })>(`${this.baseUrl}/tenants/${this.numericId(tenantId, 'Тенант')}/status`, { active }).pipe(
            map(tenant => ({ ...tenant, id: String(tenant.id) })),
        );
    }
    deleteTenant(tenantId: string) { return this.http.delete<void>(`${this.baseUrl}/tenants/${this.numericId(tenantId, 'Тенант')}`); }
    getUsers() { return this.http.get<UserRecord[]>(`${this.baseUrl}/users`); }
    getSystemRoles() { return this.http.get<SystemRoleOption[]>(`${this.baseUrl}/roles`); }
    createUser(request: CreateUserRequest) {
        return this.http.post<UserRecord>(`${this.baseUrl}/auth/admin-create-user`, {
            username: request.username,
            email: request.email,
            password: request.initialPassword,
            fullName: request.fullName,
            department: request.department,
            managerId: request.managerId,
            roleIds: request.roleIds,
            isActive: request.isActive,
        });
    }
    hasReportAccess(roleCode: string) {
        return this.reportAccessSettings().pipe(
            map(settings => settings.some(setting => setting.allowedRoleCodes.includes(roleCode))),
        );
    }

    private reportAccessSettings() {
        return this.http.get<{ reportType: string; allowedRoleCodes: string[] }[]>(`${this.baseUrl}/report-access`);
    }

    private reportTypes(): string[] {
        return ['TicketsByStatus', 'TicketsByTypeAndCriticality', 'SlaCompliance', 'AssigneeLoad', 'AssigneePerformance', 'InspectionReturns', 'RepairsSummary', 'AssetsByLifecycleStage', 'AssetMovements'];
    }

    private apiCode(value: string): string {
        const transliteration: Record<string, string> = {
            а: 'a', б: 'b', в: 'v', г: 'g', д: 'd', е: 'e', ё: 'e', ж: 'zh', з: 'z', и: 'i', й: 'y',
            к: 'k', л: 'l', м: 'm', н: 'n', о: 'o', п: 'p', р: 'r', с: 's', т: 't', у: 'u', ф: 'f',
            х: 'h', ц: 'ts', ч: 'ch', ш: 'sh', щ: 'shch', ъ: '', ы: 'y', ь: '', э: 'e', ю: 'yu', я: 'ya',
        };
        const code = [...value.trim().toLocaleLowerCase()]
            .map(character => transliteration[character] ?? character)
            .join('')
            .normalize('NFD')
            .replace(/[\u0300-\u036f]/g, '')
            .replace(/[^a-z0-9]+/g, '_')
            .replace(/^_+|_+$/g, '');
        if (!code) throw new Error(`Не удалось сформировать API code для «${value}».`);
        return code;
    }

    private formatSlaMinutes(minutes: number): string {
        return minutes % 60 === 0 ? `${minutes / 60} ч` : `${minutes} мин`;
    }

    private parseSlaMinutes(value: string): number {
        const normalized = value.trim().toLocaleLowerCase();
        const clock = normalized.match(/^(\d+):([0-5]\d)$/);
        if (clock) return Number(clock[1]) * 60 + Number(clock[2]);
        const hours = normalized.match(/^(\d+(?:[.,]\d+)?)\s*(?:ч|час(?:а|ов)?)$/);
        if (hours) return Math.round(Number(hours[1].replace(',', '.')) * 60);
        const minutes = normalized.match(/^(\d+)\s*(?:мин|минут(?:а|ы)?)$/);
        if (minutes) return Number(minutes[1]);
        throw new Error(`Значение SLA «${value}» должно быть указано в формате «30 мин», «2 ч» или «HH:mm».`);
    }

    private scheduleHours(settings: AdminSettings, current: { dayOfWeek: number; startTime: string; endTime: string }[]) {
        if (settings.slaMode === '24/7') return [];
        const range = settings.workingHours.split(',')[0]?.trim().match(/^(\d{2}:\d{2})\s*[-–]\s*(\d{2}:\d{2})$/);
        if (!range) throw new Error('Рабочее время должно быть задано одной парой HH:mm-HH:mm для выбранных рабочих дней.');
        const dayIndexes: Record<string, number> = { Вс: 0, Пн: 1, Вт: 2, Ср: 3, Чт: 4, Пт: 5, Сб: 6 };
        const selectedDays = settings.workingDays.map(day => dayIndexes[day]);
        if (selectedDays.some(day => day === undefined)) throw new Error('В графике указан неизвестный день недели.');
        if (!selectedDays.length) throw new Error('Для рабочего графика выберите хотя бы один рабочий день.');
        const ranges = [...new Set(settings.workingHours.split(',').map(value => value.trim()).filter(Boolean))];
        if (ranges.length !== 1) throw new Error('Текущая форма поддерживает одно общее рабочее время для выбранных дней.');
        return selectedDays.map(dayOfWeek => ({ dayOfWeek, startTime: range[1], endTime: range[2] }));
    }

    private toFormField(field: ContractFormField): FormFieldDefinition {
        const dependency = field.dependencies?.[0];
        if (dependency && dependency.condition !== 'Equals') {
            throw new Error(`Условие «${dependency.condition}» формы не поддерживается текущим UI.`);
        }
        return {
            id: field.code,
            name: field.name,
            type: this.formFieldType(field.fieldType),
            required: field.isRequired,
            defaultValue: field.defaultValue ?? undefined,
            hint: field.hint ?? undefined,
            visibleToRequester: !field.isEngineerOnly,
            conditionFieldId: dependency?.dependsOnFieldCode,
            conditionValue: dependency?.value ?? undefined,
        };
    }

    private formFieldType(value: string): FormFieldDefinition['type'] {
        switch (value) {
            case 'string': return 'string';
            case 'text': return 'textarea';
            case 'number': return 'number';
            case 'date': return 'date';
            case 'dictionary': return 'dictionary';
            case 'asset': return 'asset';
            case 'attachment': return 'file';
            case 'boolean': return 'boolean';
            default: throw new Error(`Тип поля «${value}» отсутствует в поддерживаемых значениях API.`);
        }
    }

    private numericId(value: string, label: string): number {
        const id = Number(value);
        if (!Number.isSafeInteger(id) || id <= 0) {
            throw new Error(`${label}: ожидался положительный числовой ID согласно API-контракту.`);
        }
        return id;
    }

    private ticketDate(ticket: Ticket): string {
        const value = ticket.createdOn ?? ticket.createdAt;
        const date = new Date(value);
        return Number.isNaN(date.getTime()) ? '' : date.toISOString().slice(0, 10);
    }

    private closedTicketDate(ticket: Ticket): string {
        if (!ticket.closedOn) return '';
        const date = new Date(ticket.closedOn);
        return Number.isNaN(date.getTime()) ? '' : date.toISOString().slice(0, 10);
    }

    private notSupported<T>(message: string): Observable<T> {
        return throwError(() => new Error(message));
    }
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