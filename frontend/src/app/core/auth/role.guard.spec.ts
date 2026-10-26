import { TestBed } from '@angular/core/testing';
import { Router, UrlTree } from '@angular/router';
import { AuthService, UserRole } from './auth.service';
import { adminOnlyAccessGuard, internalAccessGuard } from './role.guard';

class AuthServiceStub {
    authenticated = true;
    role: UserRole = 'agent';
    roles: UserRole[] = ['agent'];

    isAuthenticated = () => this.authenticated;
    getRole = () => this.role;
    getRoles = () => this.roles;
    isAdmin = () => this.role === 'admin';
    isTenantAdmin = () => this.role === 'tenant_admin';
    isPortalUser = () => this.role === 'portal_user';
    canRead = () => ['admin', 'agent', 'manager'].includes(this.role);
}

describe('role guards', () => {
    let auth: AuthServiceStub;
    let router: Router;

    beforeEach(() => {
        TestBed.configureTestingModule({
            providers: [
                { provide: AuthService, useClass: AuthServiceStub },
                { provide: Router, useValue: { parseUrl: (url: string) => ({ url }) as unknown as UrlTree } },
            ],
        });
        auth = TestBed.inject(AuthService) as unknown as AuthServiceStub;
        router = TestBed.inject(Router);
    });

    it('allows an active agent into internal routes', () => {
        const result = TestBed.runInInjectionContext(() => internalAccessGuard({} as never, {} as never));
        expect(result).toBe(true);
    });

    it('redirects an active agent away from admin-only routes', () => {
        const result = TestBed.runInInjectionContext(() => adminOnlyAccessGuard({} as never, {} as never));
        expect((result as unknown as { url: string }).url).toBe('/dashboard');
    });

    it('allows an active admin into admin-only routes', () => {
        auth.role = 'admin';
        const result = TestBed.runInInjectionContext(() => adminOnlyAccessGuard({} as never, {} as never));
        expect(result).toBe(true);
    });

    it('redirects unauthenticated users to login', () => {
        auth.authenticated = false;
        const result = TestBed.runInInjectionContext(() => adminOnlyAccessGuard({} as never, {} as never));
        expect((result as unknown as { url: string }).url).toBe('/login');
    });
});
