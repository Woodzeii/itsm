import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { AuthApi } from './auth-api';
import { AuthService } from './auth.service';
import { SessionResponse, VerifyTwoFactorResponse } from './auth.models';

function makeToken(payload: Record<string, unknown>): string {
    const encoded = btoa(JSON.stringify(payload))
        .replace(/\+/g, '-')
        .replace(/\//g, '_');
    return `header.${encoded}.signature`;
}

function tokenResponse(payload: Record<string, unknown>): VerifyTwoFactorResponse {
    return { accessToken: makeToken(payload), tokenType: 'Bearer', expiresIn: 3600 };
}

describe('AuthService multi-role session', () => {
    let service: AuthService;
    let api: {
        login: ReturnType<typeof vi.fn>;
        verifyTwoFactor: ReturnType<typeof vi.fn>;
        skipTwoFactorSetup: ReturnType<typeof vi.fn>;
        getSession: ReturnType<typeof vi.fn>;
        selectRole: ReturnType<typeof vi.fn>;
    };

    beforeEach(() => {
        localStorage.clear();
        api = {
            login: vi.fn(),
            verifyTwoFactor: vi.fn(),
            skipTwoFactorSetup: vi.fn(),
            getSession: vi.fn(),
            selectRole: vi.fn(),
        };

        TestBed.configureTestingModule({
            providers: [{ provide: AuthApi, useValue: api }],
        });
        service = TestBed.inject(AuthService);
    });

    afterEach(() => localStorage.clear());

    it('stores all roles from the 2FA token and asks for a role selection', () => {
        api.verifyTwoFactor.mockReturnValue(of(tokenResponse({
            Login: 'm.multi',
            roles: JSON.stringify(['admin', 'agent']),
            role: ['admin', 'agent'],
        })));

        service.verifyTwoFactor('challenge', '123456').subscribe();

        expect(service.getRoles()).toEqual(['admin', 'agent']);
        expect(service.getActiveRole()).toBeNull();
        expect(service.needsRoleSelection()).toBe(true);
        expect(service.getLogin()).toBe('m.multi');
    });

    it('treats a single-role token as already having an active role', () => {
        api.verifyTwoFactor.mockReturnValue(of(tokenResponse({
            Login: 'p.petrov',
            roles: JSON.stringify(['agent']),
            role: 'agent',
            activeRole: 'agent',
        })));

        service.verifyTwoFactor('challenge', '123456').subscribe();

        expect(service.getRoles()).toEqual(['agent']);
        expect(service.getActiveRole()).toBe('agent');
        expect(service.needsRoleSelection()).toBe(false);
    });

    it('loads the session from the backend and stores its roles', () => {
        localStorage.setItem('itsm_access_token', 'current-token');
        const session: SessionResponse = {
            userId: 7,
            login: 'm.multi',
            email: 'm.multi@itsm.local',
            roles: ['admin', 'agent'],
            activeRole: null,
            requiresRoleSelection: true,
        };
        api.getSession.mockReturnValue(of(session));

        let result: SessionResponse | undefined;
        service.loadSession().subscribe(value => result = value);

        expect(api.getSession).toHaveBeenCalledWith('current-token');
        expect(result).toEqual(session);
        expect(service.getRoles()).toEqual(['admin', 'agent']);
        expect(service.getActiveRole()).toBeNull();
        expect(service.needsRoleSelection()).toBe(true);
    });

    it('selects a role through the backend and stores the scoped token', () => {
        localStorage.setItem('itsm_access_token', 'current-token');
        localStorage.setItem('itsm_user_roles', JSON.stringify(['admin', 'agent']));
        api.selectRole.mockReturnValue(of({
            ...tokenResponse({
                Login: 'm.multi',
                roles: JSON.stringify(['admin', 'agent']),
                role: 'agent',
                activeRole: 'agent',
            }),
            activeRole: 'agent',
            roles: ['admin', 'agent'],
        }));

        service.selectRole('agent').subscribe();

        expect(api.selectRole).toHaveBeenCalledWith({ role: 'agent' }, 'current-token');
        expect(service.getActiveRole()).toBe('agent');
        expect(service.getRole()).toBe('agent');
        expect(service.needsRoleSelection()).toBe(false);
    });

    it('treats an expired token as unauthenticated and clears the session', () => {
        localStorage.setItem('itsm_access_token', makeToken({
            exp: Math.floor(Date.now() / 1000) - 60,
            roles: JSON.stringify(['agent']),
            role: 'agent',
        }));
        localStorage.setItem('itsm_user_roles', JSON.stringify(['agent']));

        expect(service.isAuthenticated()).toBe(false);
        expect(localStorage.getItem('itsm_access_token')).toBeNull();
        expect(localStorage.getItem('itsm_user_roles')).toBeNull();
    });

    it('treats a malformed token as unauthenticated and clears the session', () => {
        localStorage.setItem('itsm_access_token', 'not-a-jwt');

        expect(service.isAuthenticated()).toBe(false);
        expect(localStorage.getItem('itsm_access_token')).toBeNull();
    });

    it('accepts a token with a future expiration', () => {
        localStorage.setItem('itsm_access_token', makeToken({
            exp: Math.floor(Date.now() / 1000) + 3600,
            roles: JSON.stringify(['agent']),
            role: 'agent',
        }));

        expect(service.isAuthenticated()).toBe(true);
    });

    it('reports the remaining token lifetime', () => {
        localStorage.setItem('itsm_access_token', makeToken({
            exp: Math.floor(Date.now() / 1000) + 3600,
        }));

        const remaining = service.getTokenRemainingMs();

        expect(remaining).not.toBeNull();
        expect(remaining!).toBeGreaterThan(3_500_000);
        expect(remaining!).toBeLessThanOrEqual(3_600_000);
    });

    it('maps roles to their workspace entry points', () => {
        expect(service.homeUrlForRole('admin')).toBe('/admin');
        expect(service.homeUrlForRole('tenant_admin')).toBe('/admin/tenants');
        expect(service.homeUrlForRole('portal_user')).toBe('/portal');
        expect(service.homeUrlForRole('agent')).toBe('/dashboard');
        expect(service.homeUrlForRole('manager')).toBe('/dashboard');
    });

    it('clears the active role on logout', () => {
        localStorage.setItem('itsm_user_roles', JSON.stringify(['admin', 'agent']));
        localStorage.setItem('itsm_active_role', 'agent');

        service.logout();

        expect(service.getRoles()).toEqual([]);
        expect(service.getActiveRole()).toBeNull();
        expect(service.needsRoleSelection()).toBe(false);
    });
});
