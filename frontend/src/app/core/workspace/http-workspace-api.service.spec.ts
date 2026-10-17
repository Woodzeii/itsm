import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AuthService } from '../auth/auth.service';
import { Ticket } from './workspace.models';
import { HttpWorkspaceApiService } from './http-workspace-api.service';

describe('HttpWorkspaceApiService', () => {
    let api: HttpWorkspaceApiService;
    let http: HttpTestingController;

    beforeEach(() => {
        TestBed.configureTestingModule({
            providers: [
                HttpWorkspaceApiService,
                provideHttpClient(),
                provideHttpClientTesting(),
                { provide: AuthService, useValue: { getRole: () => 'admin' } },
            ],
        });
        api = TestBed.inject(HttpWorkspaceApiService);
        http = TestBed.inject(HttpTestingController);
    });

    afterEach(() => http.verify());

    it('loads a ticket and its messages through the contract endpoints', () => {
        const ticket: Ticket = {
            id: '12',
            title: 'Access request',
            requester: 'user',
            team: 'support',
            priority: 'High',
            status: 'Open',
            createdAt: '2026-10-18T00:00:00Z',
            slaRemaining: '',
        };
        let result: Ticket | undefined;

        api.getTicket('12').subscribe(value => result = value);

        http.expectOne('http://localhost:5062/api/tickets/12').flush(ticket);
        http.expectOne('http://localhost:5062/api/tickets/12/messages').flush([]);

        expect(result?.id).toBe('12');
        expect(result?.comments).toEqual([]);
    });

    it('uses the contract status update verb and status ID payload', () => {
        let result: Ticket | undefined;

        api.changeTicketStatus('12', 'In progress').subscribe(value => result = value);

        http.expectOne('http://localhost:5062/api/ticket-statuses').flush([{ id: 4, name: 'In progress' }]);
        const request = http.expectOne('http://localhost:5062/api/tickets/12/status');
        expect(request.request.method).toBe('PUT');
        expect(request.request.body).toEqual({ statusId: 4 });
        request.flush({ id: '12' } as Ticket);

        expect(result?.id).toBe('12');
    });

    it('does not invent a route for escalation lists missing from the contract', () => {
        let error: Error | undefined;

        api.getEscalations().subscribe({ error: value => error = value });

        expect(error?.message).toContain('нет метода получения списка активных эскалаций');
        http.expectNone(() => true);
    });
});
