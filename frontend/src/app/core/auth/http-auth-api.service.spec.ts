import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '../../../environments/environment';
import { HttpAuthApiService } from './http-auth-api.service';
import { SelectRoleResponse, SessionResponse } from './auth.models';

describe('HttpAuthApiService session endpoints', () => {
    let api: HttpAuthApiService;
    let http: HttpTestingController;

    beforeEach(() => {
        TestBed.configureTestingModule({
            providers: [
                provideHttpClient(),
                provideHttpClientTesting(),
                HttpAuthApiService,
            ],
        });
        api = TestBed.inject(HttpAuthApiService);
        http = TestBed.inject(HttpTestingController);
    });

    afterEach(() => http.verify());

    it('loads session roles from the backend with the current token', () => {
        const session: SessionResponse = {
            userId: 42,
            login: 'm.multi',
            email: 'm.multi@itsm.local',
            roles: ['admin', 'agent'],
            activeRole: null,
            requiresRoleSelection: true,
        };
        let result: SessionResponse | undefined;

        api.getSession('token-42').subscribe(value => result = value);

        const request = http.expectOne(`${environment.apiBaseUrl}/auth/session`);
        expect(request.request.method).toBe('GET');
        expect(request.request.headers.get('Authorization')).toBe('Bearer token-42');
        request.flush(session);

        expect(result).toEqual(session);
    });

    it('posts the selected role and returns a role-scoped token', () => {
        const response: SelectRoleResponse = {
            accessToken: 'new-token',
            tokenType: 'Bearer',
            expiresIn: 3600,
            activeRole: 'admin',
            roles: ['admin', 'agent'],
        };
        let result: SelectRoleResponse | undefined;

        api.selectRole({ role: 'admin' }, 'token-42').subscribe(value => result = value);

        const request = http.expectOne(`${environment.apiBaseUrl}/auth/session/role`);
        expect(request.request.method).toBe('POST');
        expect(request.request.headers.get('Authorization')).toBe('Bearer token-42');
        expect(request.request.body).toEqual({ role: 'admin' });
        request.flush(response);

        expect(result).toEqual(response);
    });
});
