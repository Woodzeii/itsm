import { Injectable, inject } from '@angular/core';
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
const LOGIN_KEY = 'itsm_login';
const TENANT_ID_KEY = 'itsm_tenant_id';

export type UserRole = 'admin' | 'agent' | 'manager' | 'portal_user' | 'tenant_admin';

@Injectable({ providedIn: 'root' })
export class AuthService {
    private readonly api = inject(AuthApi);

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
        const rolesValue = payload['roles'];
        const loginValue = payload['Login'] ?? payload['login'];
        const tenantIdValue = payload['tenantId'] ?? payload['tenant_id'] ?? payload['TenantId'];

        if (typeof rolesValue === 'string') {
            localStorage.setItem(ROLES_KEY, rolesValue);
        } else {
            localStorage.removeItem(ROLES_KEY);
        }

        if (typeof roleValue === 'string') {
            localStorage.setItem(ROLE_KEY, roleValue);
        } else if (Array.isArray(roleValue) && roleValue.length > 0) {
            localStorage.setItem(ROLE_KEY, String(roleValue[0]));
        }

        if (typeof loginValue === 'string') {
            localStorage.setItem(LOGIN_KEY, loginValue);
        }

        if (typeof tenantIdValue === 'number' || (typeof tenantIdValue === 'string' && !isNaN(Number(tenantIdValue)))) {
            localStorage.setItem(TENANT_ID_KEY, String(tenantIdValue));
        } else {
            localStorage.removeItem(TENANT_ID_KEY);
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

    getTenantId(): number | null {
        const tenantId = localStorage.getItem(TENANT_ID_KEY);
        return tenantId ? parseInt(tenantId, 10) : null;
    }

    getRole(): UserRole {
        return (localStorage.getItem(ROLE_KEY) as UserRole) ?? 'portal_user';
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
        localStorage.removeItem(TENANT_ID_KEY);
    }
}