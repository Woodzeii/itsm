import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../../environments/environment';
import { HttpWorkspaceApiService } from './http-workspace-api.service';

describe('HttpWorkspaceApiService backend contract', () => {
    let service: HttpWorkspaceApiService;
    let http: HttpTestingController;

    beforeEach(() => {
        TestBed.configureTestingModule({
            providers: [HttpWorkspaceApiService, provideHttpClient(), provideHttpClientTesting()],
        });
        service = TestBed.inject(HttpWorkspaceApiService);
        http = TestBed.inject(HttpTestingController);
    });

    afterEach(() => http.verify());

    it('changes ticket status with PUT and statusId', () => {
        service.changeTicketStatus('42', 3).subscribe();

        const request = http.expectOne(`${environment.apiBaseUrl}/tickets/42/status`);
        expect(request.request.method).toBe('PUT');
        expect(request.request.body).toEqual({ statusId: 3 });
        request.flush({});
    });

    it('assigns a ticket with POST and assigneeId', () => {
        service.assignTicket('42', 7).subscribe();

        const request = http.expectOne(`${environment.apiBaseUrl}/tickets/42/assign`);
        expect(request.request.method).toBe('POST');
        expect(request.request.body).toEqual({ assigneeId: 7 });
        request.flush({});
    });

    it('changes priority through PUT /tickets/{id}', () => {
        service.changeTicketPriority('42', 'Высокий').subscribe();

        const request = http.expectOne(`${environment.apiBaseUrl}/tickets/42`);
        expect(request.request.method).toBe('PUT');
        expect(request.request.body).toEqual({ priority: 'Высокий' });
        request.flush({});
    });

    it('adds a ticket message under messages with backend field names', () => {
        service.addTicketComment('42', {
            author: 'Agent',
            message: 'Test',
            internal: true,
            attachments: ['file.txt'],
        }).subscribe();

        const request = http.expectOne(`${environment.apiBaseUrl}/tickets/42/messages`);
        expect(request.request.method).toBe('POST');
        expect(request.request.body).toEqual({
            messageBody: 'Test',
            isInternal: true,
            attachments: ['file.txt'],
        });
        request.flush({});
    });

    it('escalates a ticket through POST /tickets/{id}/escalate without a client initiator', () => {
        service.escalateTicket('42', 'Engineer').subscribe();

        const request = http.expectOne(`${environment.apiBaseUrl}/tickets/42/escalate`);
        expect(request.request.method).toBe('POST');
        expect(request.request.body).toBeNull();
        request.flush({});
    });

    it('maps backend ticket responses to the frontend ticket model', () => {
        let ticket: unknown;
        service.getTickets().subscribe(tickets => ticket = tickets[0]);

        http.expectOne(`${environment.apiBaseUrl}/tickets`).flush([{
            id: 42,
            title: 'VPN не работает',
            typeName: 'Инцидент',
            statusName: 'Открыта',
            priority: 'Высокий',
            creatorName: 'Иван Иванов',
            assigneeName: 'Пётр Петров',
            assetId: 15,
            customFieldsData: { office: 'Москва' },
            createdAt: '2026-10-31T10:00:00+00:00',
            slaReactionDeadline: '2026-10-31T12:00:00+00:00',
            slaResolutionDeadline: '2026-11-01T10:00:00+00:00',
        }]);

        expect(ticket).toMatchObject({
            id: '42',
            title: 'VPN не работает',
            requester: 'Иван Иванов',
            type: 'Инцидент',
            status: 'Открыта',
            priority: 'Высокий',
            assignee: 'Пётр Петров',
            assetId: '15',
            fields: { office: 'Москва' },
        });
    });

    it('creates a ticket with the backend CreateTicketRequest contract', () => {
        service.createTicket({
            title: 'Нет доступа к VPN',
            requester: 'Иван',
            typeId: 2,
            priority: 'Высокий',
            description: 'Не подключается',
            assetId: '15',
            fields: { office: 'Москва' },
        }).subscribe();

        const request = http.expectOne(`${environment.apiBaseUrl}/tickets`);
        expect(request.request.method).toBe('POST');
        expect(request.request.body).toEqual({
            title: 'Нет доступа к VPN',
            description: 'Не подключается',
            typeId: 2,
            priority: 'Высокий',
            assetId: 15,
            customFieldsData: { office: 'Москва' },
        });
        request.flush({ id: 42, title: 'Нет доступа к VPN' });
    });

    it('builds the overview from the real ticket queue', () => {
        let overview: { metrics: { label: string; value: string }[] } | undefined;
        service.getOverview().subscribe(result => overview = result);

        const request = http.expectOne(`${environment.apiBaseUrl}/tickets`);
        expect(request.request.method).toBe('GET');
        request.flush([{
            id: 42,
            title: 'VPN',
            statusName: 'Открыта',
            priority: 'Критический',
            creatorName: 'Иван',
            createdAt: new Date().toISOString(),
        }]);

        expect(overview?.metrics.find(metric => metric.label === 'Активные заявки')?.value).toBe('1');
        expect(overview?.metrics.find(metric => metric.label === 'Критические')?.value).toBe('1');
    });

    it('builds escalations from the ticket queue instead of a missing endpoint', () => {
        let escalations: unknown;
        service.getEscalations().subscribe(result => escalations = result);

        http.expectOne(`${environment.apiBaseUrl}/tickets`).flush([{
            id: 42,
            title: 'VPN',
            typeName: 'Инцидент',
            statusName: 'Открыта',
            priority: 'Критический',
            creatorName: 'Иван',
            createdAt: new Date().toISOString(),
        }]);

        expect(escalations).toEqual([{
            id: '42',
            title: 'VPN',
            team: 'Инцидент',
            priority: 'Критический',
            slaRemaining: '',
        }]);
    });

    it('unwraps paged backend users response', () => {
        let users: unknown;
        service.getUsers().subscribe(result => users = result);

        const request = http.expectOne(`${environment.apiBaseUrl}/users`);
        request.flush({ items: [{ id: 1, username: 'admin' }], total: 1, page: 1, pageSize: 20 });

        expect(users).toEqual([{ id: 1, username: 'admin' }]);
    });

    it('uses the user roles endpoint implemented by the backend', () => {
        service.getSystemRoles().subscribe();

        const request = http.expectOne(`${environment.apiBaseUrl}/users/roles`);
        expect(request.request.method).toBe('GET');
        request.flush([]);
    });

    it('toggles a user with PUT /users/{id}/active', () => {
        service.setUserActive(5, false).subscribe();

        const request = http.expectOne(`${environment.apiBaseUrl}/users/5/active`);
        expect(request.request.method).toBe('PUT');
        expect(request.request.body).toEqual({ isActive: false });
        request.flush({});
    });

    it('updates an asset with PUT, not PATCH', () => {
        service.updateAsset('15', { name: 'Ноутбук' }).subscribe();

        const request = http.expectOne(`${environment.apiBaseUrl}/assets/15`);
        expect(request.request.method).toBe('PUT');
        request.flush({});
    });

    it('posts portal messages as JSON to the ticket messages endpoint', () => {
        service.addPortalComment('42', 'Здравствуйте', []).subscribe();

        const request = http.expectOne(`${environment.apiBaseUrl}/tickets/42/messages`);
        expect(request.request.method).toBe('POST');
        expect(request.request.body).toEqual({
            messageBody: 'Здравствуйте',
            isInternal: false,
            attachments: [],
        });
        request.flush({});
    });

    it('reports endpoints missing in the backend explicitly', () => {
        let error: Error | undefined;
        service.getAdminSettings().subscribe({ error: err => error = err });

        expect(error?.message).toContain('admin settings');
    });
});
