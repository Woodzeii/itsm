import { Injectable, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { AuthApi } from './auth-api';
import {
    LoginRequest,
    LoginResponse,
    SkipTwoFactorSetupRequest,
    VerifyTwoFactorRequest,
    VerifyTwoFactorResponse,
} from './auth.models';
const TOKEN_KEY = 'itsm_access_token';
const AUTH_KEY = 'itsm_is_authenticated';
const ROLE_KEY = 'itsm_user_role';
const ROLES_KEY = 'itsm_user_roles';
const ROLE_PENDING_KEY = 'itsm_role_selection_pending';
const LOGIN_KEY = 'itsm_login';

export type UserRole = 'admin' | 'agent' | 'manager' | 'portal_user' | 'tenant_admin';

@Injectable({ providedIn: 'root' })
export class AuthService {
    private readonly api = inject(AuthApi);
    readonly availableRoles = signal<UserRole[]>(this.readRoles());
    readonly activeRole = signal<UserRole>(this.readActiveRole());

    // ================= API =================

    login(username: string, password: string): Observable<LoginResponse> {
        const body: LoginRequest = { username, password };
        return this.api.login(body);
    }

    verifyTwoFactor(challengeId: string, code: string): Observable<VerifyTwoFactorResponse> {
        const body: VerifyTwoFactorRequest = { challengeId, code };
        return this.api
            .verifyTwoFactor(body)
            .pipe(tap(res => this.saveToken(res.accessToken)));
    }

    skipTwoFactorSetup(challengeId: string): Observable<VerifyTwoFactorResponse> {
        const body: SkipTwoFactorSetupRequest = { challengeId };
        return this.api
            .skipTwoFactorSetup(body)
            .pipe(tap(res => this.saveToken(res.accessToken)));
    }

    // ================= ТОКЕН =================

    private saveToken(token: string): void {
        localStorage.setItem(TOKEN_KEY, token);
        localStorage.setItem(AUTH_KEY, 'true');

        const payload = this.decodeJwt(token);
        if (!payload) return;

        const roleValue = payload['role']
            ?? payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'];
        const rolesValue = payload['roles'] ?? roleValue;
        const loginValue = payload['Login'] ?? payload['login'];
        const roles = this.normalizeRoles(rolesValue);
        const activeRole = this.normalizeRoles(roleValue)[0] ?? roles[0] ?? 'portal_user';

        if (roles.length) {
            localStorage.setItem(ROLES_KEY, JSON.stringify(roles));
            this.availableRoles.set(roles);
        }
        this.setActiveRole(activeRole);
        sessionStorage.setItem(ROLE_PENDING_KEY, roles.length > 1 ? 'true' : 'false');

        if (typeof loginValue === 'string') {
            localStorage.setItem(LOGIN_KEY, loginValue);
        }
    }

    private decodeJwt(token: string): Record<string, unknown> | null {
        try {
            const payload = token.split('.')[1];
            const decoded = atob(payload.replace(/-/g, '+').replace(/_/g, '/'));
            const utf8 = decodeURIComponent(
                Array.from(decoded)
                    .map(c => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2))
                    .join(''),
            );
            return JSON.parse(utf8) as Record<string, unknown>;
        } catch {
            return null;
        }
    }

    getToken(): string | null {
        return localStorage.getItem(TOKEN_KEY);
    }

    isAuthenticated(): boolean {
        return !!this.getToken();
    }

    getLogin(): string {
        return localStorage.getItem(LOGIN_KEY) ?? '';
    }

    getRole(): UserRole {
        return this.activeRole();
    }

    getRoles(): UserRole[] {
        return this.availableRoles();
    }

    hasMultipleRoles(): boolean {
        return this.availableRoles().length > 1;
    }

    requiresRoleSelection(): boolean {
        return this.isAuthenticated() && this.hasMultipleRoles()
            && sessionStorage.getItem(ROLE_PENDING_KEY) === 'true';
    }

    setActiveRole(role: UserRole): boolean {
        if (!this.availableRoles().includes(role)) return false;
        localStorage.setItem(ROLE_KEY, role);
        this.activeRole.set(role);
        return true;
    }

    completeRoleSelection(role: UserRole): boolean {
        if (!this.setActiveRole(role)) return false;
        sessionStorage.setItem(ROLE_PENDING_KEY, 'false');
        return true;
    }

    reopenRoleSelection(): void {
        sessionStorage.setItem(ROLE_PENDING_KEY, 'true');
    }

    isAdmin(): boolean {
        return this.getRole() === 'admin';
    }

    canRead(): boolean {
        const role = this.getRole();
        return role === 'admin' || role === 'agent' || role === 'manager';
    }

    isPortalUser(): boolean {
        const rolesJson = localStorage.getItem(ROLES_KEY);
        if (!rolesJson) return this.getRole() === 'portal_user';

        try {
            const roles: unknown = JSON.parse(rolesJson);
            return Array.isArray(roles) && roles.length === 1 && roles[0] === 'portal_user';
        } catch {
            return this.getRole() === 'portal_user';
        }
    }

    isTenantAdmin(): boolean {
        return this.getRole() === 'tenant_admin';
    }

    logout(): void {
        localStorage.removeItem(TOKEN_KEY);
        localStorage.removeItem(AUTH_KEY);
        localStorage.removeItem(ROLE_KEY);
        localStorage.removeItem(ROLES_KEY);
        localStorage.removeItem(LOGIN_KEY);
        sessionStorage.removeItem(ROLE_PENDING_KEY);
        this.availableRoles.set([]);
        this.activeRole.set('portal_user');
    }

    private readActiveRole(): UserRole {
        return (localStorage.getItem(ROLE_KEY) as UserRole) ?? 'portal_user';
    }

    private readRoles(): UserRole[] {
        try {
            const roles = JSON.parse(localStorage.getItem(ROLES_KEY) ?? '[]') as unknown;
            return this.normalizeRoles(roles);
        } catch {
            return this.normalizeRoles(localStorage.getItem(ROLE_KEY));
        }
    }

    private normalizeRoles(value: unknown): UserRole[] {
        const rawRoles = Array.isArray(value) ? value : typeof value === 'string' ? [value] : [];
        const knownRoles: UserRole[] = ['admin', 'agent', 'manager', 'portal_user', 'tenant_admin'];
        return [...new Set(rawRoles.filter((role): role is UserRole => knownRoles.includes(role as UserRole)))];
    }
}