import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../../environments/environment';
import { HttpWorkspaceApiService } from './http-workspace-api.service';

describe('HttpWorkspaceApiService', () => {
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

    it('adds a ticket message under messages with backend field names', () => {
        service.addTicketComment('42', { author: 'Agent', message: 'Test', internal: true, attachments: ['file.txt'] }).subscribe();
        const request = http.expectOne(`${environment.apiBaseUrl}/tickets/42/messages`);
        expect(request.request.method).toBe('POST');
        expect(request.request.body).toEqual({ messageBody: 'Test', isInternal: true, attachments: ['file.txt'] });
        request.flush({});
    });

    it('unwraps paged backend users response', () => {
        let users: unknown;
        service.getUsers().subscribe(result => users = result);
        const request = http.expectOne(`${environment.apiBaseUrl}/users`);
        request.flush({ items: [{ id: 1, username: 'admin' }], total: 1, page: 1, pageSize: 20 });
        expect(users).toEqual([{ id: 1, username: 'admin' }]);
    });
});
