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

    it('loads ticket messages and preserves internal flags and downloadable attachment ids', () => {
        let comments: unknown;
        service.getTicketComments('42').subscribe(result => comments = result);

        const request = http.expectOne(`${environment.apiBaseUrl}/tickets/42/messages`);
        expect(request.request.method).toBe('GET');
        request.flush([{
            id: 8,
            authorName: 'Agent',
            messageBody: 'Internal note',
            isInternal: true,
            attachments: [{ id: 13, fileName: 'log.txt' }],
            createdAt: '2026-11-07T08:00:00Z',
        }]);

        expect(comments).toEqual([{
            id: '8',
            author: 'Agent',
            message: 'Internal note',
            createdAt: '2026-11-07T08:00:00Z',
            internal: true,
            attachments: ['log.txt'],
            attachmentRecords: [{ id: '13', fileName: 'log.txt' }],
        }]);
    });

    it('loads a message page with its offset and size', () => {
        let comments: unknown;
        service.getTicketCommentsPage('42', 30, 30).subscribe(result => comments = result);

        const request = http.expectOne(req =>
            req.url === `${environment.apiBaseUrl}/tickets/42/messages`
            && req.params.get('skip') === '30'
            && req.params.get('take') === '30');
        expect(request.request.method).toBe('GET');
        request.flush([]);

        expect(comments).toEqual([]);
    });

    it('loads ticket history and maps backend identifiers to strings', () => {
        let history: unknown;
        service.getTicketHistory('42').subscribe(result => history = result);

        const request = http.expectOne(`${environment.apiBaseUrl}/tickets/42/history`);
        expect(request.request.method).toBe('GET');
        request.flush([{
            id: 8,
            event: 'Заявка создана',
            actor: 'Agent',
            createdAt: '2026-11-07T08:00:00Z',
        }]);

        expect(history).toEqual([{
            id: '8',
            event: 'Заявка создана',
            actor: 'Agent',
            createdAt: '2026-11-07T08:00:00Z',
        }]);
    });

    it('loads a history page with its offset and size', () => {
        let history: unknown;
        service.getTicketHistoryPage('42', 30, 30).subscribe(result => history = result);

        const request = http.expectOne(req =>
            req.url === `${environment.apiBaseUrl}/tickets/42/history`
            && req.params.get('skip') === '30'
            && req.params.get('take') === '30');
        expect(request.request.method).toBe('GET');
        request.flush([]);

        expect(history).toEqual([]);
    });

    it('posts a ticket message with backend field names', () => {
        service.addTicketComment('42', {
            author: 'Agent',
            message: 'Test',
            internal: true,
        }, []).subscribe();

        const request = http.expectOne(`${environment.apiBaseUrl}/tickets/42/messages`);
        expect(request.request.method).toBe('POST');
        expect(request.request.body).toEqual({
            messageBody: 'Test',
            isInternal: true,
            attachments: [],
        });
        request.flush({});
    });

    it('uploads staff attachments before creating a message with upload tokens', () => {
        const file = new File(['sample'], 'log.txt', { type: 'text/plain' });
        service.addTicketComment('42', {
            author: 'Agent',
            message: 'Логи',
            internal: false,
        }, [file]).subscribe();

        const upload = http.expectOne(`${environment.apiBaseUrl}/tickets/42/attachments`);
        expect(upload.request.method).toBe('POST');
        expect(upload.request.body instanceof FormData).toBe(true);
        const uploadId = '00112233445566778899aabbccddeeff';
        upload.flush({ id: uploadId, fileName: 'log.txt', sizeBytes: 6, expiresAt: '2026-11-08T08:00:00Z' });

        const message = http.expectOne(`${environment.apiBaseUrl}/tickets/42/messages`);
        expect(message.request.method).toBe('POST');
        expect(message.request.body).toEqual({
            messageBody: 'Логи',
            isInternal: false,
            attachments: [uploadId],
        });
        message.flush({
            id: 9,
            authorName: 'Agent',
            messageBody: 'Логи',
            isInternal: false,
            attachments: [{ id: 14, fileName: 'log.txt' }],
        });
    });

    it('downloads ticket attachment bytes through the authorized API client', () => {
        let downloaded: Blob | undefined;
        service.downloadTicketAttachment('42', { id: '13', fileName: 'log.txt' }).subscribe(blob => downloaded = blob);

        const request = http.expectOne(`${environment.apiBaseUrl}/tickets/42/attachments/13`);
        expect(request.request.method).toBe('GET');
        request.flush(new Blob(['sample']));

        expect(downloaded?.size).toBe(6);
    });

    it('escalates a ticket through POST /tickets/{id}/escalate without a client initiator', () => {
        service.escalateTicket('42', 'Engineer').subscribe();

        const request = http.expectOne(`${environment.apiBaseUrl}/tickets/42/escalate`);
        expect(request.request.method).toBe('POST');
        expect(request.request.body).toBeNull();
        request.flush({});
    });

    it('reads and updates the assignment mode through the backend', () => {
        let setting: { mode: string; activeAgentsCount: number } | undefined;
        service.saveAssignmentSetting('manual').subscribe(value => setting = value);

        const request = http.expectOne(`${environment.apiBaseUrl}/tickets/assignment-setting`);
        expect(request.request.method).toBe('PUT');
        expect(request.request.body).toEqual({ mode: 'manual' });
        request.flush({ mode: 'manual', manualAssignerUserId: null, manualAssignerName: null, activeAgentsCount: 3 });

        expect(setting).toEqual({ mode: 'manual', manualAssignerUserId: null, manualAssignerName: null, activeAgentsCount: 3 });
    });

    it('triggers automatic assignment through /auto-assign', () => {
        let result: { ticketId: string; assigneeName: string } | undefined;
        service.autoAssignTicket('42').subscribe(value => result = value);

        const request = http.expectOne(`${environment.apiBaseUrl}/tickets/42/auto-assign`);
        expect(request.request.method).toBe('POST');
        request.flush({ ticketId: 42, assigneeId: 5, assigneeName: 'Пётр Петров', openTicketsCount: 1, reason: 'least-loaded' });

        expect(result).toEqual(expect.objectContaining({ ticketId: '42', assigneeName: 'Пётр Петров' }));
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
            requesterId: null,
            customFieldsData: { office: 'Москва' },
        });
        request.flush({ id: 42, title: 'Нет доступа к VPN' });
    });

    it('loads the signed-in user tickets from the portal endpoint', () => {
        let tickets: unknown;
        service.getPortalTickets().subscribe(result => tickets = result);

        const request = http.expectOne(`${environment.apiBaseUrl}/tickets/mine`);
        expect(request.request.method).toBe('GET');
        request.flush([{ id: 42, title: 'VPN', typeName: 'Инцидент', statusName: 'Новая' }]);

        expect(tickets).toMatchObject([{ id: '42', title: 'VPN', type: 'Инцидент' }]);
    });

    it('loads available portal ticket types', () => {
        let types: unknown;
        service.getPortalTicketTypes().subscribe(result => types = result);

        const request = http.expectOne(`${environment.apiBaseUrl}/tickets/portal-types`);
        expect(request.request.method).toBe('GET');
        request.flush([{ id: 1, name: 'Инцидент' }]);

        expect(types).toEqual([{ id: 1, name: 'Инцидент' }]);
    });

    it('creates a portal ticket with its selected type and three built-in fields', () => {
        service.createPortalTicket({
            title: 'Нет доступа к VPN',
            description: 'Не подключается',
            requester: 'Иван',
            type: 'Инцидент',
            typeId: 1,
            priority: 'Средний',
        }).subscribe();

        const request = http.expectOne(`${environment.apiBaseUrl}/tickets`);
        expect(request.request.method).toBe('POST');
        expect(request.request.body).toEqual({
            title: 'Нет доступа к VPN',
            description: 'Не подключается',
            typeId: 1,
            priority: 'Средний',
            assetId: null,
            requesterId: null,
            customFieldsData: null,
        });
        request.flush({ id: 43, title: 'Нет доступа к VPN', typeName: 'Инцидент' });
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

    it('resolves real status and assignee ids from the loaded queue', () => {
        service.getTickets().subscribe();

        http.expectOne(`${environment.apiBaseUrl}/tickets`).flush([{
            id: 42,
            title: 'VPN',
            statusId: 3,
            statusName: 'В работе',
            priority: 'Высокий',
            assigneeId: 5,
            assigneeName: 'Пётр Петров',
        }]);

        expect(service.resolveStatusId('В работе')).toBe(3);
        expect(service.resolveStatusId('в работе')).toBe(3);
        expect(service.resolveAssigneeId('Пётр Петров')).toBe(5);
        expect(service.knownAssigneeNames()).toEqual(['Пётр Петров']);
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
        request.flush({ id: 15, name: 'Ноутбук' });
        http.expectOne(`${environment.apiBaseUrl}/assets/15`).flush({ asset: { id: 15, name: 'Ноутбук' } });
    });

    it('creates an asset with the backend contract and returns its detail', () => {
        let detail: { recordId?: string; name: string } | undefined;
        service.createAsset({
            name: 'Ноутбук Dell',
            type: 'Ноутбук',
            className: 'Ноутбук',
            id: 'INV-9001',
            serialNumber: 'SN-9001',
            assetClassId: 3,
            warehouseId: 1,
        }).subscribe(result => detail = result);

        const request = http.expectOne(`${environment.apiBaseUrl}/assets`);
        expect(request.request.method).toBe('POST');
        expect(request.request.body).toEqual({
            name: 'Ноутбук Dell',
            inventoryNumber: 'INV-9001',
            serialNumber: 'SN-9001',
            assetClassId: 3,
            warehouseId: 1,
            locationId: null,
            departmentId: null,
            assignedUserId: null,
        });
        request.flush({ id: 15, name: 'Ноутбук Dell', inventoryNumber: 'INV-9001' });

        const detailRequest = http.expectOne(`${environment.apiBaseUrl}/assets/15`);
        expect(detailRequest.request.method).toBe('GET');
        detailRequest.flush({ asset: { id: 15, name: 'Ноутбук Dell', inventoryNumber: 'INV-9001', lifecycleStage: 'InStock' } });

        expect(detail?.recordId).toBe('15');
        expect(detail?.name).toBe('Ноутбук Dell');
    });

    it('registers an asset movement using backend movement types and reloads the card', () => {
        service.moveAsset('15', { kind: 'Выдача со склада', from: '', to: 'Анна Белова', toUserId: 7 }).subscribe();

        const request = http.expectOne(`${environment.apiBaseUrl}/assets/15/movements`);
        expect(request.request.method).toBe('POST');
        expect(request.request.body).toEqual({
            movementType: 'issue',
            toUserId: 7,
            toWarehouseId: null,
            toLocationId: null,
            toDepartmentId: null,
            comment: null,
        });
        request.flush({ id: 1, assetId: 15, movementType: 'issue' });
        http.expectOne(`${environment.apiBaseUrl}/assets/15`).flush({ asset: { id: 15, name: 'Ноутбук' } });
    });

    it('posts portal messages as public JSON to the ticket messages endpoint', () => {
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

    it('loads the persisted enabled tenant admin-creation policy from the backend', () => {
        let policy: { tenantId: number; allowAdminsCreateAdmins: boolean } | undefined;
        service.getTenantAdminCreationPolicy().subscribe(result => policy = result);

        const request = http.expectOne(`${environment.apiBaseUrl}/tenants/settings`);
        expect(request.request.method).toBe('GET');
        request.flush({ tenantId: 1, allowAdminsCreateAdmins: true });

        expect(policy).toEqual({ tenantId: 1, allowAdminsCreateAdmins: true });
    });

    it('saves the persisted tenant admin-creation policy without mock-only fields', () => {
        let policy: { tenantId: number; allowAdminsCreateAdmins: boolean } | undefined;
        service.saveTenantAdminCreationPolicy(false).subscribe(result => policy = result);

        const request = http.expectOne(`${environment.apiBaseUrl}/tenants/settings`);
        expect(request.request.method).toBe('PUT');
        expect(request.request.body).toEqual({ allowAdminsCreateAdmins: false });
        request.flush({ tenantId: 1, allowAdminsCreateAdmins: false });

        expect(policy).toEqual({ tenantId: 1, allowAdminsCreateAdmins: false });
    });
});
