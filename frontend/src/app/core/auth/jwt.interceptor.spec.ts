import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { AuthService } from './auth.service';
import { jwtInterceptor } from './jwt.interceptor';

describe('jwtInterceptor', () => {
    let client: HttpClient;
    let http: HttpTestingController;
    let router: Router;
    let auth: {
        getToken: ReturnType<typeof vi.fn>;
        logout: ReturnType<typeof vi.fn>;
    };

    beforeEach(() => {
        auth = {
            getToken: vi.fn().mockReturnValue('valid-token'),
            logout: vi.fn(),
        };

        TestBed.configureTestingModule({
            providers: [
                provideRouter([]),
                provideHttpClient(withInterceptors([jwtInterceptor])),
                provideHttpClientTesting(),
                { provide: AuthService, useValue: auth },
            ],
        });

        client = TestBed.inject(HttpClient);
        http = TestBed.inject(HttpTestingController);
        router = TestBed.inject(Router);
    });

    afterEach(() => http.verify());

    it('attaches the bearer token to API requests', () => {
        client.get('/api/tickets').subscribe();

        const request = http.expectOne('/api/tickets');
        expect(request.request.headers.get('Authorization')).toBe('Bearer valid-token');
        request.flush({});
    });

    it('clears the session and redirects to /login on 401', () => {
        const navigate = vi.spyOn(router, 'navigate').mockResolvedValue(true);
        let error: unknown;

        client.get('/api/tickets').subscribe({ error: err => error = err });

        http.expectOne('/api/tickets').flush(
            { message: 'Unauthorized' },
            { status: 401, statusText: 'Unauthorized' },
        );

        expect(auth.logout).toHaveBeenCalledOnce();
        expect(navigate).toHaveBeenCalledWith(['/login'], {
            queryParams: { returnUrl: router.url },
        });
        expect(error).toBeTruthy();
    });

    it('does not log out or redirect when the login request itself returns 401', () => {
        auth.getToken.mockReturnValue(null);
        const navigate = vi.spyOn(router, 'navigate');

        client.post('/api/auth/login', { username: 'admin', password: 'wrong' })
            .subscribe({ error: () => undefined });

        http.expectOne('/api/auth/login').flush(
            { message: 'Неверный логин или пароль' },
            { status: 401, statusText: 'Unauthorized' },
        );

        expect(auth.logout).not.toHaveBeenCalled();
        expect(navigate).not.toHaveBeenCalled();
    });
});
