import { TestBed } from '@angular/core/testing';
import { RouterStateSnapshot, UrlTree, provideRouter } from '@angular/router';
import { AuthApi } from './auth-api';
import { authGuard } from './auth.guard';

function makeToken(payload: Record<string, unknown>): string {
    const encoded = btoa(JSON.stringify(payload)).replace(/\+/g, '-').replace(/\//g, '_');
    return `header.${encoded}.signature`;
}

function runAuthGuard(url: string): boolean | UrlTree {
    const state = { url } as RouterStateSnapshot;
    return TestBed.runInInjectionContext(() => authGuard({} as never, state)) as boolean | UrlTree;
}

describe('authGuard', () => {
    beforeEach(() => {
        localStorage.clear();
        TestBed.configureTestingModule({
            providers: [
                provideRouter([]),
                {
                    provide: AuthApi,
                    useValue: {
                        login: vi.fn(),
                        verifyTwoFactor: vi.fn(),
                        skipTwoFactorSetup: vi.fn(),
                        getSession: vi.fn(),
                        selectRole: vi.fn(),
                    },
                },
            ],
        });
    });

    afterEach(() => localStorage.clear());

    it('redirects an unauthenticated user to /login with returnUrl', () => {
        const result = runAuthGuard('/tickets') as UrlTree;

        expect(result instanceof UrlTree).toBe(true);
        expect(result.toString()).toContain('/login');
        expect(result.toString()).toContain('returnUrl=%2Ftickets');
    });

    it('redirects a user with an expired token and clears the session', () => {
        localStorage.setItem('itsm_access_token', makeToken({
            exp: Math.floor(Date.now() / 1000) - 60,
            roles: JSON.stringify(['agent']),
        }));

        const result = runAuthGuard('/dashboard') as UrlTree;

        expect(result instanceof UrlTree).toBe(true);
        expect(result.toString()).toContain('/login');
        expect(localStorage.getItem('itsm_access_token')).toBeNull();
    });

    it('lets an authenticated user through', () => {
        localStorage.setItem('itsm_access_token', makeToken({
            exp: Math.floor(Date.now() / 1000) + 3600,
            roles: JSON.stringify(['agent']),
            role: 'agent',
        }));

        expect(runAuthGuard('/dashboard')).toBe(true);
    });
});
