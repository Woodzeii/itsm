import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, CanActivateFn, UrlTree, convertToParamMap, provideRouter } from '@angular/router';
import { AuthApi } from './auth-api';
import {
    adminOnlyAccessGuard,
    internalAccessGuard,
    portalAccessGuard,
    roleSelectionRequiredGuard,
    selectRoleGuard,
} from './role.guard';

function runGuard(
    guard: CanActivateFn,
    queryParams: Record<string, string> = {},
): boolean | UrlTree {
    const route = { queryParamMap: convertToParamMap(queryParams) } as ActivatedRouteSnapshot;
    return TestBed.runInInjectionContext(() => guard(route, {} as never)) as boolean | UrlTree;
}

function redirectTarget(result: boolean | UrlTree): string {
    expect(result instanceof UrlTree).toBe(true);
    return (result as UrlTree).toString();
}

describe('role selection guards', () => {
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

    function setSession(roles: string[], activeRole: string | null): void {
        localStorage.setItem('itsm_access_token', 'token');
        localStorage.setItem('itsm_user_roles', JSON.stringify(roles));
        localStorage.setItem('itsm_user_role', activeRole ?? roles[0]);
        if (activeRole) localStorage.setItem('itsm_active_role', activeRole);
    }

    it('redirects authenticated multi-role users without an active role to /select-role', () => {
        setSession(['admin', 'agent'], null);

        expect(redirectTarget(runGuard(roleSelectionRequiredGuard))).toBe('/select-role');
    });

    it('allows working routes once the active role is chosen', () => {
        setSession(['admin', 'agent'], 'agent');

        expect(runGuard(roleSelectionRequiredGuard)).toBe(true);
    });

    it('allows single-role users without the intermediate screen', () => {
        setSession(['agent'], 'agent');

        expect(runGuard(roleSelectionRequiredGuard)).toBe(true);
    });

    it('redirects unauthenticated users to /login', () => {
        expect(redirectTarget(runGuard(roleSelectionRequiredGuard))).toBe('/login');
    });

    it('opens /select-role only while the role is not chosen', () => {
        setSession(['admin', 'agent'], null);
        expect(runGuard(selectRoleGuard)).toBe(true);

        localStorage.setItem('itsm_active_role', 'admin');
        expect(redirectTarget(runGuard(selectRoleGuard))).toBe('/admin');
    });

    it('sends users who already chose a role to the workspace entry point', () => {
        setSession(['portal_user'], null);
        localStorage.setItem('itsm_active_role', 'portal_user');

        expect(redirectTarget(runGuard(selectRoleGuard))).toBe('/portal');
    });

    it('opens /select-role for an explicit role switch', () => {
        setSession(['admin', 'agent'], 'agent');

        expect(redirectTarget(runGuard(selectRoleGuard))).toBe('/dashboard');
        expect(runGuard(selectRoleGuard, { switch: '1' })).toBe(true);
    });

    it('applies the selected active role to the existing workspace guards', () => {
        setSession(['admin', 'agent'], 'agent');

        expect(runGuard(internalAccessGuard)).toBe(true);
        expect(redirectTarget(runGuard(adminOnlyAccessGuard))).toBe('/dashboard');

        localStorage.setItem('itsm_active_role', 'admin');
        localStorage.setItem('itsm_user_role', 'admin');

        expect(runGuard(adminOnlyAccessGuard)).toBe(true);
    });

    it('lets a multi-role user into the portal when portal_user is active', () => {
        setSession(['admin', 'portal_user'], 'portal_user');

        expect(runGuard(portalAccessGuard)).toBe(true);
    });
});
