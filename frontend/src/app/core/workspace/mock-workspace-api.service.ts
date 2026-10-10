import { Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';
import { WorkspaceApi } from './workspace-api';
import {
    AdminSettings,
    CreatePortalAccountRequest,
    AssetDetails,
    AssetHistoryEntry,
    AssetMovement,
    AssetMovementRequest,
    AssetRecord,
    AssetLifecycle,
    CreateAssetRequest,
    CreateTenantRequest,
    CreateTicketRequest,
    EscalationHistoryEntry,
    EscalationPolicy,
    Escalation,
    FormFieldDefinition,
    OverviewData,
    PortalTicketRequest,
    RepairCloseRequest,
    ReportSummary,
    ServiceCategory,
    TicketComment,
    TicketFormDefinition,
    Ticket,
    TicketType,
    TenantRecord,
} from './workspace.models';

@Injectable()
export class MockWorkspaceApiService extends WorkspaceApi {
    private tickets: Ticket[] = [
        { id: 'INC-1048', title: 'Не подключается VPN после обновления', requester: 'Анна Белова', team: 'Сетевая команда', priority: 'Высокий', status: 'В работе', createdAt: '10:42', slaRemaining: '01:18', type: 'Инцидент', assignee: 'Иван Петров', reactionSla: '00:08 / 00:30', resolutionSla: '01:18 / 04:00', assetId: 'LT-00231', createdOn: '2026-10-10', history: [{ id: 'h1', event: 'Заявка создана', actor: 'Анна Белова', createdAt: '10:12' }, { id: 'h2', event: 'Назначен исполнитель Иван Петров', actor: 'Диспетчер', createdAt: '10:20' }], comments: [{ id: 'c1', author: 'Иван Петров', message: 'Проверяю параметры подключения.', createdAt: '10:42', internal: false, attachments: [] }] },
        { id: 'SR-1047', title: 'Выдать доступ к корпоративному диску', requester: 'Пользователь портала', team: 'Сервис-деск', priority: 'Средний', status: 'Открыта', createdAt: '10:19', slaRemaining: '03:42', type: 'Запрос на обслуживание', assignee: 'Иван Петров', requesterLogin: 'portal.user', reactionSla: '00:11 / 00:30', resolutionSla: '03:42 / 08:00', createdOn: '2026-10-10', history: [{ id: 'h3', event: 'Заявка создана', actor: 'Пользователь портала', createdAt: '10:19' }], comments: [] },
        { id: 'INC-1046', title: 'Пропал доступ к принтеру на 3 этаже', requester: 'Елена Смирнова', team: 'Сервис-деск', priority: 'Средний', status: 'Ожидает выполнения', createdAt: '09:56', slaRemaining: '05:26', type: 'Инцидент', assignee: 'Сергей Волков', reactionSla: '00:15 / 00:30', resolutionSla: '05:26 / 08:00', createdOn: '2026-10-10', history: [], comments: [] },
        { id: 'SR-1045', title: 'Установка лицензии Figma', requester: 'Дмитрий Волков', team: 'ИТ-отдел', priority: 'Низкий', status: 'Проверка', createdAt: '09:41', slaRemaining: '07:10', type: 'Запрос на обслуживание', assignee: 'Иван Петров', reactionSla: '00:10 / 00:30', resolutionSla: '07:10 / 16:00', createdOn: '2026-10-09', history: [], comments: [] },
        { id: 'INC-1044', title: 'Медленная работа CRM', requester: 'Ольга Кузнецова', team: 'Приложения', priority: 'Высокий', status: 'В работе', createdAt: '09:12', slaRemaining: '00:34', type: 'Инцидент', assignee: 'Сергей Волков', reactionSla: '00:12 / 00:30', resolutionSla: '00:34 / 04:00', createdOn: '2026-10-09', history: [], comments: [] },
        { id: 'SR-1031', title: 'Подключение нового офиса', requester: 'Мария Соколова', team: 'Сетевая команда', priority: 'Средний', status: 'В работе', createdAt: 'Вчера', slaRemaining: '02:18', type: 'Инцидент', assignee: 'Иван Петров', reactionSla: '00:18 / 01:00', resolutionSla: '02:18 / 08:00', createdOn: '2026-10-08', history: [], comments: [] },
        { id: 'RPR-1008', title: 'Диагностика ноутбука', requester: 'Иван Петров', team: 'Сервис-деск', priority: 'Средний', status: 'В работе', createdAt: 'Вчера', slaRemaining: '04:20', type: 'Тикет на ремонт', assignee: 'Иван Петров', assetId: 'LT-00231', createdOn: '2026-10-09', history: [], comments: [] },
    ];

    private readonly assets: AssetDetails[] = [
        { id: 'LT-00231', name: 'Ноутбук Lenovo ThinkPad', owner: 'Анна Белова', status: 'В эксплуатации', lifecycle: 'В эксплуатации', className: 'Ноутбук', type: 'Ноутбук', warehouse: 'Основной склад', location: 'Офис, этаж 3', department: 'Сервис-деск', attributes: { Производитель: 'Lenovo', Модель: 'ThinkPad T14' }, history: [{ id: 'ah1', event: 'Выдан со склада', actor: 'Иван Петров', createdAt: '2026-09-28 09:10', details: 'Основной склад → Анна Белова' }], movements: [], relatedTickets: [] },
        { id: 'SRV-00084', name: 'Сервер приложений CRM', owner: 'ИТ-инфраструктура', status: 'В эксплуатации', lifecycle: 'В эксплуатации', className: 'Сервер', type: 'Сервер', warehouse: 'Основной склад', location: 'Серверная 1', department: 'ИТ-инфраструктура', attributes: { Производитель: 'Dell', Модель: 'PowerEdge R650' }, history: [], movements: [], relatedTickets: [] },
        { id: 'MON-00142', name: 'Монитор Dell U2722D', owner: 'Анна Белова', status: 'В эксплуатации', lifecycle: 'В эксплуатации', className: 'Монитор', type: 'Монитор', warehouse: 'Основной склад', location: 'Офис, этаж 3', department: 'Дизайн', attributes: { Производитель: 'Dell', Модель: 'U2722D' }, history: [], movements: [], relatedTickets: [] },
        { id: 'LIC-00097', name: 'Лицензия Figma Professional', owner: 'Дизайн-команда', status: 'Истекает через 14 дней', lifecycle: 'В эксплуатации', className: 'Лицензия', type: 'Лицензия', warehouse: '—', location: 'Облачная лицензия', department: 'Дизайн', attributes: { Поставщик: 'Figma' }, history: [], movements: [], relatedTickets: [] },
        { id: 'LT-00244', name: 'Ноутбук для выдачи', owner: 'Склад', status: 'На складе', lifecycle: 'На складе', className: 'Ноутбук', type: 'Ноутбук', warehouse: 'Основной склад', location: 'Ячейка B-14', department: '—', attributes: { Производитель: 'Dell', Модель: 'Latitude 5440' }, history: [], movements: [], relatedTickets: [] },
    ];

    private readonly categories: ServiceCategory[] = [
        { id: 'access', title: 'Доступы и учетные записи', description: 'Создание, изменение и блокировка доступов', serviceCount: 8 },
        { id: 'workplace', title: 'Рабочее место', description: 'Оборудование, ПО и подключение сотрудников', serviceCount: 12 },
        { id: 'infrastructure', title: 'Инфраструктура', description: 'Сеть, VPN, серверы и хранилища', serviceCount: 6 },
        { id: 'business-apps', title: 'Бизнес-приложения', description: 'CRM, ERP и корпоративные сервисы', serviceCount: 14 },
    ];

    private readonly escalations: Escalation[] = [
        { id: 'INC-1044', title: 'Медленная работа CRM', team: 'Приложения', priority: 'Высокий', slaRemaining: '00:34' },
        { id: 'INC-1039', title: 'Недоступен файловый сервер', team: 'Инфраструктура', priority: 'Критический', slaRemaining: '00:52' },
        { id: 'SR-1031', title: 'Подключение нового офиса', team: 'Сетевая команда', priority: 'Средний', slaRemaining: '02:18' },
    ];

    private readonly ticketTypes: TicketType[] = [
        { id: 'incident', name: 'Инцидент', portalAvailable: true, repair: false, slaEnabled: true, reactionSla: '30 мин', resolutionSla: '4 ч', slaByCriticality: { Низкий: { reaction: '1 ч', resolution: '16 ч' }, Средний: { reaction: '30 мин', resolution: '8 ч' }, Высокий: { reaction: '15 мин', resolution: '4 ч' } } },
        { id: 'service-request', name: 'Запрос на обслуживание', portalAvailable: true, repair: false, slaEnabled: true, reactionSla: '1 ч', resolutionSla: '16 ч', slaByCriticality: { Низкий: { reaction: '4 ч', resolution: '24 ч' }, Средний: { reaction: '1 ч', resolution: '16 ч' }, Высокий: { reaction: '30 мин', resolution: '8 ч' } } },
        { id: 'repair', name: 'Тикет на ремонт', portalAvailable: false, repair: true, slaEnabled: true, reactionSla: '30 мин', resolutionSla: '8 ч', slaByCriticality: { Низкий: { reaction: '2 ч', resolution: '16 ч' }, Средний: { reaction: '30 мин', resolution: '8 ч' }, Высокий: { reaction: '15 мин', resolution: '4 ч' } } },
    ];

    private readonly assetHistory = new Map<string, AssetHistoryEntry[]>();
    private readonly assetMovements = new Map<string, AssetMovement[]>();
    private readonly escalationHistory: EscalationHistoryEntry[] = [];
    private readonly portalLogin = 'portal.user';
    private tenants: TenantRecord[] = [
        { id: 'tenant-nocode', name: 'Nocode', firstAdminLogin: 'admin.nocode', active: true },
        { id: 'tenant-demo', name: 'Демонстрационный тенант', firstAdminLogin: 'admin.demo', active: true },
    ];

    private settings: AdminSettings = {
        assignmentMode: 'automatic',
        manager: 'Иван Петров',
        slaEnabled: true,
        slaMode: 'working-hours',
        workingHours: 'Пн–Пт, 09:00–18:00',
        criticalities: ['Низкий', 'Средний', 'Высокий'],
        statuses: [
            { name: 'Открыта', pausesSla: false, immutable: true },
            { name: 'Ожидает выполнения', pausesSla: false, immutable: true },
            { name: 'В работе', pausesSla: false, immutable: true },
            { name: 'Проверка', pausesSla: true, immutable: true },
            { name: 'Закрыта', pausesSla: true, immutable: true },
            { name: 'Ожидание пользователя', pausesSla: true, immutable: false },
        ],
        portalSlaFields: ['Время реакции', 'Время решения'],
        portalAccounts: ['portal.user'],
        engineers: ['Иван Петров', 'Сергей Волков'],
        tenantMode: 'on-premise',
        passwordPolicy: { minLength: 12, requireUppercase: true, requireSpecial: true, expiryDays: 90, maxFailedAttempts: 5, lockoutMinutes: 15 },
        reportAccessRoles: ['admin', 'manager'],
    };

    private forms: TicketFormDefinition[] = this.ticketTypes.map(type => ({
        ticketTypeId: type.id,
        ticketTypeName: type.name,
        fields: [
            { id: `${type.id}-title`, name: 'Тема', type: 'string', required: true, visibleToRequester: true, immutable: true },
            { id: `${type.id}-description`, name: 'Описание', type: 'textarea', required: true, visibleToRequester: true, immutable: true },
            { id: `${type.id}-priority`, name: 'Критичность', type: 'dictionary', required: true, visibleToRequester: true, options: [...this.settings.criticalities], immutable: true },
            ...(type.repair ? [{ id: 'repair-asset', name: 'Актив', type: 'asset' as const, required: true, visibleToRequester: false, immutable: true }] : []),
            { id: `${type.id}-asset`, name: 'Связанный актив', type: 'asset', required: false, visibleToRequester: true },
            { id: `${type.id}-details`, name: 'Дополнительные сведения', type: 'textarea', required: false, visibleToRequester: true, hint: 'Укажите данные, которые помогут обработать заявку.' },
        ],
    }));

    private policies: EscalationPolicy[] = this.ticketTypes.map(type => ({
        ticketTypeId: type.id,
        ticketTypeName: type.name,
        onResolutionBreach: !type.repair,
        allowManualByRequester: type.portalAvailable,
        allowManualByAgent: true,
        manager: this.settings.manager,
    }));

    getOverview(): Observable<OverviewData> {
        return of({
            metrics: [
                { label: 'Открытые заявки', value: '91', change: '+12%' },
                { label: 'Активы в учете', value: '486', change: '+18' },
                { label: 'Доступность сервисов', value: '99,8%', change: '+0,4%' },
                { label: 'Эскалации', value: String(this.escalations.length), change: '-2' },
            ],
            series: [
                { label: 'Пн', resolved: 42, incoming: 68 },
                { label: 'Вт', resolved: 58, incoming: 78 },
                { label: 'Ср', resolved: 49, incoming: 65 },
                { label: 'Чт', resolved: 76, incoming: 82 },
                { label: 'Пт', resolved: 68, incoming: 72 },
                { label: 'Сб', resolved: 86, incoming: 94 },
                { label: 'Вс', resolved: 72, incoming: 82 },
            ],
            recentTickets: this.tickets.slice(0, 5),
            activeEscalations: this.escalations.length,
        });
    }

    getTickets(): Observable<Ticket[]> { return of([...this.tickets]); }
    getAssets(): Observable<AssetRecord[]> { return of(this.assets.map(({ history, movements, relatedTickets, ...asset }) => asset)); }
    getServiceCategories(): Observable<ServiceCategory[]> { return of([...this.categories]); }
    getEscalations(): Observable<Escalation[]> { return of([...this.escalations]); }

    createTicket(request: CreateTicketRequest): Observable<Ticket> {
        const ticket: Ticket = {
            id: this.nextTicketId('SR'),
            title: request.title,
            requester: request.requester,
            team: request.category || 'Сервис-деск',
            priority: request.priority || 'Средний',
            status: 'Открыта',
            createdAt: new Intl.DateTimeFormat('ru-RU', { hour: '2-digit', minute: '2-digit' }).format(new Date()),
            slaRemaining: '—',
            type: request.type || 'Запрос на обслуживание',
            assignee: this.settings.assignmentMode === 'automatic' ? this.leastLoadedEngineer() : undefined,
            reactionSla: '00:00 / 00:30',
            resolutionSla: '00:00 / 04:00',
            assetId: request.assetId,
            createdOn: new Date().toISOString().slice(0, 10),
            fields: request.fields ?? {},
            requesterLogin: request.requesterLogin,
            history: [{ id: this.id('history'), event: 'Заявка создана', actor: request.requester, createdAt: this.now() }],
            comments: request.description ? [{ id: this.id('comment'), author: request.requester, message: request.description, createdAt: this.now(), internal: false, attachments: [] }] : [],
        };
        this.tickets = [ticket, ...this.tickets];
        if (ticket.assetId) this.findAsset(ticket.assetId).relatedTickets.unshift(ticket);
        return of(ticket);
    }

    getTicketTypes(): Observable<TicketType[]> { return of(this.ticketTypes.map(type => ({ ...type }))); }
    saveTicketTypes(ticketTypes: TicketType[]): Observable<TicketType[]> {
        if (!ticketTypes.some(type => type.repair && type.id === 'repair')) return this.fail('Встроенный тип «Тикет на ремонт» нельзя удалить.');
        this.ticketTypes.splice(0, this.ticketTypes.length, ...ticketTypes.map(type => ({ ...type })));
        return this.getTicketTypes();
    }

    changeTicketStatus(ticketId: string, status: string): Observable<Ticket> {
        const ticket = this.findTicket(ticketId);
        const transitions: Record<string, string[]> = {
            'Открыта': ['Ожидает выполнения'],
            'Ожидает выполнения': ['В работе'],
            'В работе': ['Проверка'],
            'Проверка': ['Закрыта', 'Ожидает выполнения'],
        };
        if (!transitions[ticket.status]?.includes(status)) return this.fail(`Недопустимый переход: ${ticket.status} → ${status}`);
        ticket.status = status;
        if (status === 'Закрыта') ticket.closedOn = new Date().toISOString().slice(0, 10);
        this.recordTicketHistory(ticket, `Статус изменен: ${status}`, 'Инженер ТП');
        return of(ticket);
    }

    assignTicket(ticketId: string, assignee: string): Observable<Ticket> {
        if (this.settings.assignmentMode !== 'manual') return this.fail('Ручное назначение отключено настройками.');
        if (!this.settings.engineers?.includes(assignee)) return this.fail('Исполнитель должен быть инженером ТП.');
        const ticket = this.findTicket(ticketId);
        ticket.assignee = assignee;
        this.recordTicketHistory(ticket, `Назначен исполнитель: ${assignee}`, 'Администратор');
        return of(ticket);
    }

    changeTicketPriority(ticketId: string, priority: string): Observable<Ticket> {
        if (!this.settings.criticalities.includes(priority)) return this.fail('Выберите критичность из настроенной шкалы.');
        const ticket = this.findTicket(ticketId);
        const previous = ticket.priority;
        ticket.priority = priority;
        this.recordTicketHistory(ticket, `Критичность изменена: ${previous} → ${priority}`, 'Инженер ТП');
        return of(ticket);
    }

    addTicketComment(ticketId: string, comment: Omit<TicketComment, 'id' | 'createdAt'>): Observable<TicketComment> {
        const ticket = this.findTicket(ticketId);
        const added: TicketComment = { ...comment, id: this.id('comment'), createdAt: this.now() };
        ticket.comments = [...(ticket.comments ?? []), added];
        this.recordTicketHistory(ticket, added.internal ? 'Добавлен внутренний комментарий' : 'Добавлен комментарий', added.author);
        return of(added);
    }

    escalateTicket(ticketId: string, initiator: string): Observable<EscalationHistoryEntry> {
        const ticket = this.findTicket(ticketId);
        const policy = this.policies.find(item => item.ticketTypeName === ticket.type);
        if (!policy) return this.fail('Для типа заявки не настроена эскалация.');
        if (initiator === 'Автор заявки' && !policy.allowManualByRequester) return this.fail('Ручная эскалация автором отключена.');
        if (initiator !== 'Автор заявки' && !policy.allowManualByAgent) return this.fail('Ручная эскалация исполнителем отключена.');
        const previousPriority = ticket.priority;
        const current = this.settings.criticalities.indexOf(previousPriority);
        const nextPriority = this.settings.criticalities[Math.min(current + 1, this.settings.criticalities.length - 1)] ?? previousPriority;
        ticket.priority = nextPriority;
        ticket.status = 'В работе';
        const recipients = current >= this.settings.criticalities.length - 1 ? [policy.manager, 'Администратор'] : [policy.manager];
        const entry: EscalationHistoryEntry = { ticketId, initiator, createdAt: this.now(), previousPriority, newPriority: nextPriority, recipients };
        this.escalationHistory.unshift(entry);
        this.escalations.unshift({ id: ticket.id, title: ticket.title, team: ticket.team, priority: ticket.priority, slaRemaining: ticket.slaRemaining });
        this.recordTicketHistory(ticket, `Эскалация: ${previousPriority} → ${nextPriority}; уведомлены ${recipients.join(', ')}`, initiator);
        return of(entry);
    }

    getAssetDetails(assetId: string): Observable<AssetDetails> {
        const asset = this.findAsset(assetId);
        return of({ ...asset, history: [...asset.history], movements: [...asset.movements], relatedTickets: this.tickets.filter(ticket => ticket.assetId === asset.id) });
    }

    updateAsset(assetId: string, changes: Partial<CreateAssetRequest>): Observable<AssetDetails> {
        const asset = this.findAsset(assetId);
        const oldValues = { name: asset.name, owner: asset.owner, location: asset.location, department: asset.department };
        if (changes.id && changes.id !== asset.id && this.assets.some(item => item.id === changes.id)) return this.fail('Идентификатор актива должен быть уникальным.');
        if (changes.id && !asset.id && !changes.warehouse?.trim()) return this.fail('При присвоении идентификатора обязательно указать склад.');
        Object.assign(asset, changes);
        if (changes.id && asset.lifecycle === 'Закуплен') {
            asset.lifecycle = 'На складе';
            asset.status = 'На складе';
            this.addAssetHistory(asset, 'Присвоен идентификатор, актив принят на склад', 'Инженер ТП', `${changes.id} · ${changes.warehouse}`);
        }
        for (const [key, value] of Object.entries(changes)) {
            const previous = oldValues[key as keyof typeof oldValues];
            if (previous !== undefined && previous !== value) this.addAssetHistory(asset, `Изменен атрибут «${key}»`, 'Инженер ТП', `${previous} → ${value}`);
        }
        return of({ ...asset, history: [...asset.history], movements: [...asset.movements], relatedTickets: [...asset.relatedTickets] });
    }

    deleteAsset(assetId: string): Observable<void> {
        const asset = this.findAsset(assetId);
        if (asset.lifecycle !== 'Закуплен') return this.fail('Удалить можно только актив на этапе «Закуплен».');
        this.assets.splice(this.assets.indexOf(asset), 1);
        return of(void 0);
    }

    createAsset(request: CreateAssetRequest): Observable<AssetDetails> {
        const lifecycle: AssetLifecycle = request.id ? 'На складе' : 'Закуплен';
        const asset: AssetDetails = {
            recordId: request.id || this.id('asset'), id: request.id || '', name: request.name, type: request.type, className: request.className,
            owner: request.owner || '—', status: lifecycle, lifecycle, warehouse: request.warehouse || '—',
            location: request.location || '—', department: request.department || '—', attributes: request.attributes ?? {},
            history: [], movements: [], relatedTickets: [],
        };
        if (asset.id && this.assets.some(item => item.id === asset.id)) return this.fail('Идентификатор актива должен быть уникальным.');
        if (asset.id && !request.warehouse) return this.fail('Для актива с идентификатором обязательно указать склад.');
        this.assets.unshift(asset);
        this.addAssetHistory(asset, `Создан актив на этапе «${lifecycle}»`, asset.id || 'Закупка', `${asset.className}: ${asset.name}`);
        return of({ ...asset });
    }

    moveAsset(assetId: string, request: AssetMovementRequest): Observable<AssetDetails> {
        const asset = this.findAsset(assetId);
        if (asset.lifecycle === 'Списан') return this.fail('Списанный актив нельзя перемещать.');
        if (request.kind === 'Выдача со склада') {
            if (asset.lifecycle !== 'На складе') return this.fail('Выдать можно только актив со склада.');
            asset.lifecycle = 'В эксплуатации';
            asset.owner = request.to;
        } else if (request.kind === 'Возврат на склад') {
            if (asset.lifecycle !== 'В эксплуатации') return this.fail('Вернуть на склад можно только актив в эксплуатации.');
            asset.lifecycle = 'На складе';
            asset.owner = 'Склад';
        } else if (request.kind === 'Передача сотруднику') {
            asset.owner = request.to;
        } else if (request.kind === 'Перемещение') {
            asset.location = request.to;
        }
        asset.status = asset.lifecycle ?? asset.status;
        const movement: AssetMovement = { id: this.id('movement'), ...request, actor: 'Инженер ТП', createdAt: this.now() };
        asset.movements.unshift(movement);
        this.addAssetHistory(asset, request.kind, movement.actor, `${request.from} → ${request.to}`);
        return of({ ...asset, history: [...asset.history], movements: [...asset.movements], relatedTickets: [...asset.relatedTickets] });
    }

    createRepairTicket(assetId: string, title: string): Observable<Ticket> {
        const asset = this.findAsset(assetId);
        if (!['На складе', 'В эксплуатации'].includes(asset.lifecycle ?? '')) return this.fail('Ремонт доступен для активов на складе или в эксплуатации.');
        const ticket = this.createTicket({ title, requester: 'Инженер ТП', type: 'Тикет на ремонт', priority: 'Средний', assetId }).pipe();
        const created = this.tickets[0];
        asset.lifecycle = 'В ремонте';
        asset.status = 'В ремонте';
        this.addAssetHistory(asset, 'Создан тикет на ремонт', 'Инженер ТП', `${created.id}: ${title}`);
        return ticket;
    }

    closeRepairTicket(ticketId: string, request: RepairCloseRequest): Observable<Ticket> {
        if (!request.workDescription.trim()) return this.fail('Опишите, что ремонтировалось и что было заменено.');
        const ticket = this.findTicket(ticketId);
        if (ticket.type !== 'Тикет на ремонт' || !ticket.assetId) return this.fail('Тикет не связан с активом для ремонта.');
        if (ticket.status === 'Закрыта') return this.fail('Закрытая заявка не может быть открыта повторно.');
        const asset = this.findAsset(ticket.assetId);
        const lifecycle: AssetLifecycle = request.result === 'В эксплуатацию' ? 'В эксплуатации' : request.result === 'На склад' ? 'На складе' : 'Списан';
        asset.lifecycle = lifecycle;
        asset.status = lifecycle;
        ticket.status = 'Закрыта';
        ticket.closedOn = new Date().toISOString().slice(0, 10);
        this.recordTicketHistory(ticket, `Ремонт закрыт: ${request.workDescription}`, 'Инженер ТП');
        this.addAssetHistory(asset, 'Тикет на ремонт закрыт', 'Инженер ТП', `${request.workDescription}; этап: ${lifecycle}`);
        return of(ticket);
    }

    getForms(): Observable<TicketFormDefinition[]> { return of(this.forms.map(form => ({ ...form, fields: form.fields.map(field => ({ ...field, options: field.options ? [...field.options] : undefined })) }))); }

    saveForm(form: TicketFormDefinition): Observable<TicketFormDefinition> {
        const index = this.forms.findIndex(item => item.ticketTypeId === form.ticketTypeId);
        const oldForm = this.forms[index];
        if (!this.ticketTypes.some(type => type.id === form.ticketTypeId)) return this.fail('Тип заявки не найден.');
        for (const required of ['Тема', 'Описание', 'Критичность']) {
            if (!form.fields.some(field => field.name === required && field.immutable)) return this.fail(`Встроенное поле «${required}» нельзя удалить или изменить.`);
        }
        const oldFields = new Set((oldForm?.fields ?? []).map(field => field.id));
        const nextFields = new Set(form.fields.map(field => field.id));
        const removed = [...oldFields].filter(id => !nextFields.has(id));
        if (removed.some(id => oldForm?.fields.find(field => field.id === id)?.type === 'asset' && this.tickets.some(ticket => ticket.type === form.ticketTypeName && ticket.assetId))) return this.fail('Нельзя удалить поле выбора актива, использованное в заявках.');
        for (const ticket of this.tickets) {
            for (const fieldId of removed) if (ticket.fields) delete ticket.fields[fieldId];
        }
        const saved = { ...form, fields: form.fields.map(field => ({ ...field })) };
        if (index < 0) this.forms.push(saved); else this.forms[index] = saved;
        return of(saved);
    }

    getEscalationPolicies(): Observable<EscalationPolicy[]> { return of(this.policies.map(policy => ({ ...policy }))); }
    saveEscalationPolicies(policies: EscalationPolicy[]): Observable<EscalationPolicy[]> { this.policies = policies.map(policy => ({ ...policy })); return of(this.policies); }
    getEscalationHistory(): Observable<EscalationHistoryEntry[]> { return of(this.escalationHistory.map(entry => ({ ...entry, recipients: [...entry.recipients] }))); }
    getAdminSettings(): Observable<AdminSettings> { return of({ ...this.settings, criticalities: [...this.settings.criticalities], statuses: this.settings.statuses.map(status => ({ ...status })), portalSlaFields: [...this.settings.portalSlaFields], portalAccounts: [...(this.settings.portalAccounts ?? [])], engineers: [...(this.settings.engineers ?? [])], passwordPolicy: this.settings.passwordPolicy ? { ...this.settings.passwordPolicy } : undefined, reportAccessRoles: [...(this.settings.reportAccessRoles ?? [])] }); }

    saveAdminSettings(settings: AdminSettings): Observable<AdminSettings> {
        if (settings.criticalities.length < 3 || settings.criticalities.length > 5) return this.fail('Шкала критичности должна содержать от 3 до 5 уровней.');
        for (const required of ['Открыта', 'Ожидает выполнения', 'В работе', 'Проверка', 'Закрыта']) {
            if (!settings.statuses.some(status => status.name === required && status.immutable)) return this.fail(`Стандартный статус «${required}» нельзя удалить.`);
        }
        this.settings = { ...settings, criticalities: [...settings.criticalities], statuses: settings.statuses.map(status => ({ ...status })), portalSlaFields: [...settings.portalSlaFields], portalAccounts: [...(settings.portalAccounts ?? [])], engineers: [...(settings.engineers ?? [])], passwordPolicy: settings.passwordPolicy ? { ...settings.passwordPolicy } : undefined, reportAccessRoles: [...(settings.reportAccessRoles ?? [])] };
        return this.getAdminSettings();
    }

    getReports(_from?: string, _to?: string): Observable<ReportSummary[]> {
        const open = this.tickets.filter(ticket => ticket.status !== 'Закрыта').length;
        return of([
            { id: 'status', title: 'Заявки по статусам', description: 'Текущий срез общей очереди', value: `${open} открытых` },
            { id: 'type-priority', title: 'Заявки по типам и критичности', description: 'Распределение за выбранный период', value: `${this.tickets.length} заявок` },
            { id: 'sla', title: 'Соблюдение SLA', description: 'Реакция и решение по типам', value: '94%' },
            { id: 'load', title: 'Нагрузка исполнителей', description: 'Открытые заявки по инженерам', value: 'Иван Петров · 3' },
            { id: 'performance', title: 'Результативность исполнителей', description: 'Закрытия и среднее время', value: 'Экспорт отчета' },
            { id: 'returns', title: 'Возвраты с проверки', description: 'Заявки, возвращенные авторами', value: '2 возврата' },
            { id: 'repairs', title: 'Ремонты', description: 'Итоги по активам и классам', value: '1 в работе' },
            { id: 'lifecycle', title: 'Активы по этапам', description: 'Распределение по классам', value: `${this.assets.length} активов` },
            { id: 'movements', title: 'Движения активов', description: 'Перемещения за период', value: `${this.assets.reduce((count, asset) => count + asset.movements.length, 0)} движений` },
        ]);
    }

    getPortalTickets(): Observable<Ticket[]> { return of(this.tickets.filter(ticket => ticket.requesterLogin === this.portalLogin).map(ticket => ({ ...ticket }))); }

    createPortalTicket(request: PortalTicketRequest): Observable<Ticket> {
        const type = this.ticketTypes.find(item => item.name === request.type);
        if (!type?.portalAvailable || type.repair) return this.fail('Этот тип заявки недоступен на портале.');
        const ticket = this.createTicket({ ...request, requesterLogin: this.portalLogin });
        const created = this.tickets[0];
        created.comments = [...(created.comments ?? []), ...(request.attachments?.length ? [{ id: this.id('comment'), author: request.requester, message: `${request.attachments.length} файл(а) приложено к заявке.`, createdAt: this.now(), internal: false, attachments: request.attachments.map(file => file.name) }] : [])];
        return ticket;
    }

    addPortalComment(ticketId: string, message: string, attachments: File[]): Observable<TicketComment> {
        const ticket = this.findTicket(ticketId);
        if (ticket.requesterLogin !== this.portalLogin) return this.fail('Доступны только собственные заявки.');
        return this.addTicketComment(ticketId, { author: ticket.requester, message, internal: false, attachments: attachments.map(file => file.name) });
    }

    reviewPortalTicket(ticketId: string, accepted: boolean): Observable<Ticket> {
        const ticket = this.findTicket(ticketId);
        if (ticket.requesterLogin !== this.portalLogin) return this.fail('Доступны только собственные заявки.');
        if (ticket.status !== 'Проверка') return this.fail('Заявку можно проверить только в статусе «Проверка».');
        return this.changeTicketStatus(ticketId, accepted ? 'Закрыта' : 'Ожидает выполнения');
    }

    createPortalAccount(request: CreatePortalAccountRequest): Observable<string> {
        const username = request.username.trim();
        const policy = this.settings.passwordPolicy;
        if (!username) return this.fail('Укажите логин пользователя.');
        if ((this.settings.portalAccounts ?? []).includes(username)) return this.fail('Учетная запись уже существует.');
        if (request.temporaryPassword.length < (policy?.minLength ?? 12)) return this.fail(`Пароль должен содержать минимум ${policy?.minLength ?? 12} символов.`);
        if (policy?.requireUppercase && !/[A-ZА-Я]/.test(request.temporaryPassword)) return this.fail('В пароле должна быть заглавная буква.');
        if (policy?.requireSpecial && !/[^a-zA-Zа-яА-Я0-9]/.test(request.temporaryPassword)) return this.fail('В пароле должен быть специальный символ.');
        this.settings.portalAccounts = [...(this.settings.portalAccounts ?? []), username];
        return of(username);
    }

    getTenants(): Observable<TenantRecord[]> {
        if (this.settings.tenantMode !== 'saas') return this.fail('Управление тенантами доступно только в SaaS-поставке.');
        return of(this.tenants.map(tenant => ({ ...tenant })));
    }

    createTenant(request: CreateTenantRequest): Observable<TenantRecord> {
        if (this.settings.tenantMode !== 'saas') return this.fail('Управление тенантами доступно только в SaaS-поставке.');
        const tenant: TenantRecord = { id: this.id('tenant'), name: request.name.trim(), firstAdminLogin: request.firstAdminLogin.trim(), active: true };
        if (!tenant.name || !tenant.firstAdminLogin) return this.fail('Название тенанта и логин администратора обязательны.');
        this.tenants.unshift(tenant);
        return of(tenant);
    }

    setTenantActive(tenantId: string, active: boolean): Observable<TenantRecord> {
        const tenant = this.tenants.find(item => item.id === tenantId);
        if (!tenant) return this.fail('Тенант не найден.');
        tenant.active = active;
        return of({ ...tenant });
    }

    deleteTenant(tenantId: string): Observable<void> {
        const tenant = this.tenants.find(item => item.id === tenantId);
        if (!tenant) return this.fail('Тенант не найден.');
        this.tenants.splice(this.tenants.indexOf(tenant), 1);
        return of(void 0);
    }

    private nextTicketId(prefix: string): string {
        const highest = this.tickets.reduce((value, ticket) => {
            const match = ticket.id.match(/(\d+)$/);
            return match ? Math.max(value, Number(match[1])) : value;
        }, 1000);
        return `${prefix}-${String(highest + 1).padStart(4, '0')}`;
    }

    private leastLoadedEngineer(): string | undefined {
        return (this.settings.engineers ?? []).map(engineer => ({
            engineer,
            openCount: this.tickets.filter(ticket => ticket.assignee === engineer && ticket.status !== 'Закрыта').length,
        })).sort((left, right) => left.openCount - right.openCount)[0]?.engineer;
    }

    private id(prefix: string): string { return `${prefix}-${Date.now()}-${Math.random().toString(36).slice(2, 7)}`; }
    private now(): string { return new Intl.DateTimeFormat('ru-RU', { dateStyle: 'short', timeStyle: 'short' }).format(new Date()); }
    private fail<T>(message: string): Observable<T> { return new Observable(subscriber => subscriber.error(new Error(message))); }
    private findTicket(ticketId: string): Ticket { const ticket = this.tickets.find(item => item.id === ticketId); if (!ticket) throw new Error('Заявка не найдена.'); return ticket; }
    private findAsset(assetId: string): AssetDetails { const asset = this.assets.find(item => item.id === assetId || item.recordId === assetId); if (!asset) throw new Error('Актив не найден.'); return asset; }
    private recordTicketHistory(ticket: Ticket, event: string, actor: string): void { ticket.history = [{ id: this.id('history'), event, actor, createdAt: this.now() }, ...(ticket.history ?? [])]; }
    private addAssetHistory(asset: AssetDetails, event: string, actor: string, details: string): void {
        const entry: AssetHistoryEntry = { id: this.id('asset-history'), event, actor, details, createdAt: this.now() };
        asset.history.unshift(entry);
        this.assetHistory.set(asset.id, asset.history);
    }
}