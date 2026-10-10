import { Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';
import { WorkspaceApi } from './workspace-api';
import {
    AssetRecord,
    CreateTicketRequest,
    Escalation,
    OverviewData,
    ServiceCategory,
    Ticket,
} from './workspace.models';

@Injectable()
export class MockWorkspaceApiService extends WorkspaceApi {
    private tickets: Ticket[] = [
        { id: 'INC-1048', title: 'Не подключается VPN после обновления', requester: 'Анна Белова', team: 'Сетевая команда', priority: 'Критический', status: 'В работе', createdAt: '10:42', slaRemaining: '01:18' },
        { id: 'SR-1047', title: 'Выдать доступ к корпоративному диску', requester: 'Михаил Орлов', team: 'Сервис-деск', priority: 'Высокий', status: 'Новая', createdAt: '10:19', slaRemaining: '03:42' },
        { id: 'INC-1046', title: 'Пропал доступ к принтеру на 3 этаже', requester: 'Елена Смирнова', team: 'Сервис-деск', priority: 'Средний', status: 'Ожидание', createdAt: '09:56', slaRemaining: '05:26' },
        { id: 'SR-1045', title: 'Установка лицензии Figma', requester: 'Дмитрий Волков', team: 'ИТ-отдел', priority: 'Низкий', status: 'В работе', createdAt: '09:41', slaRemaining: '07:10' },
        { id: 'INC-1044', title: 'Медленная работа CRM', requester: 'Ольга Кузнецова', team: 'Приложения', priority: 'Высокий', status: 'Эскалация', createdAt: '09:12', slaRemaining: '00:34' },
    ];

    private readonly assets: AssetRecord[] = [
        { id: 'LT-00231', name: 'Ноутбук Lenovo ThinkPad', owner: 'Алексей Климов', status: 'В эксплуатации', type: 'Ноутбук' },
        { id: 'SRV-00084', name: 'Сервер приложений CRM', owner: 'ИТ-инфраструктура', status: 'В работе', type: 'Сервер' },
        { id: 'MON-00142', name: 'Монитор Dell U2722D', owner: 'Анна Белова', status: 'В эксплуатации', type: 'Монитор' },
        { id: 'LIC-00097', name: 'Лицензия Figma Professional', owner: 'Дизайн-команда', status: 'Истекает через 14 дней', type: 'Лицензия' },
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
    getAssets(): Observable<AssetRecord[]> { return of([...this.assets]); }
    getServiceCategories(): Observable<ServiceCategory[]> { return of([...this.categories]); }
    getEscalations(): Observable<Escalation[]> { return of([...this.escalations]); }

    createTicket(request: CreateTicketRequest): Observable<Ticket> {
        const ticket: Ticket = {
            id: `SR-${String(1048 + this.tickets.length).padStart(4, '0')}`,
            title: request.title,
            requester: request.requester,
            team: request.category || 'Сервис-деск',
            priority: 'Средний',
            status: 'Новая',
            createdAt: new Intl.DateTimeFormat('ru-RU', { hour: '2-digit', minute: '2-digit' }).format(new Date()),
            slaRemaining: '—',
        };
        this.tickets = [ticket, ...this.tickets];
        return of(ticket);
    }
}