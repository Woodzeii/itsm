import { Injectable, inject } from '@angular/core';
import { Observable, throwError, tap } from 'rxjs';
import { AuthApi } from './auth-api';
import {
    LoginRequest,
    LoginResponse,
    SelectRoleRequest,
    SelectRoleResponse,
    SessionResponse,
    SkipTwoFactorSetupRequest,
    VerifyTwoFactorRequest,
    VerifyTwoFactorResponse,
} from './auth.models';
const TOKEN_KEY = 'itsm_access_token';
const AUTH_KEY = 'itsm_is_authenticated';
const ROLE_KEY = 'itsm_user_role';
const ROLES_KEY = 'itsm_user_roles';
const ACTIVE_ROLE_KEY = 'itsm_active_role';
const LOGIN_KEY = 'itsm_login';

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

    // ================= СЕССИЯ И АКТИВНАЯ РОЛЬ =================

    /**
     * Загружает сессию с бэкенда: роли пользователя и активную роль.
     * Именно бэкенд решает, нужен ли промежуточный экран выбора роли.
     */
    loadSession(): Observable<SessionResponse> {
        const token = this.getToken();
        if (!token) {
            return throwError(() => new Error('Нет активной сессии. Выполните вход.'));
        }

        return this.api.getSession(token).pipe(tap(session => this.saveSession(session)));
    }

    /**
     * Фиксирует активную роль на бэкенде и сохраняет токен, ограниченный этой ролью.
     */
    selectRole(role: UserRole | string): Observable<SelectRoleResponse> {
        const token = this.getToken();
        if (!token) {
            return throwError(() => new Error('Нет активной сессии. Выполните вход.'));
        }

        const body: SelectRoleRequest = { role };
        return this.api.selectRole(body, token).pipe(
            tap(res => {
                this.saveToken(res.accessToken);

                // Ответ бэкенда — источник истины для активной роли сессии.
                if (res.roles.length > 0) {
                    localStorage.setItem(ROLES_KEY, JSON.stringify(res.roles));
                }
                if (res.activeRole) {
                    localStorage.setItem(ACTIVE_ROLE_KEY, res.activeRole);
                    localStorage.setItem(ROLE_KEY, res.activeRole);
                }
            }),
        );
    }

    private saveSession(session: SessionResponse): void {
        localStorage.setItem(ROLES_KEY, JSON.stringify(session.roles));

        const role = session.activeRole ?? session.roles[0] ?? null;
        if (role) {
            localStorage.setItem(ROLE_KEY, role);
        }

        if (session.activeRole) {
            localStorage.setItem(ACTIVE_ROLE_KEY, session.activeRole);
        } else {
            localStorage.removeItem(ACTIVE_ROLE_KEY);
        }

        if (session.login) {
            localStorage.setItem(LOGIN_KEY, session.login);
        }
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
        const activeRoleValue = payload['activeRole'];
        const loginValue = payload['Login'] ?? payload['login'];

        if (typeof rolesValue === 'string') {
            localStorage.setItem(ROLES_KEY, rolesValue);
        } else {
            localStorage.removeItem(ROLES_KEY);
        }

        if (typeof activeRoleValue === 'string' && activeRoleValue) {
            localStorage.setItem(ACTIVE_ROLE_KEY, activeRoleValue);
            localStorage.setItem(ROLE_KEY, activeRoleValue);
        } else {
            localStorage.removeItem(ACTIVE_ROLE_KEY);
            if (typeof roleValue === 'string') {
                localStorage.setItem(ROLE_KEY, roleValue);
            } else if (Array.isArray(roleValue) && roleValue.length > 0) {
                localStorage.setItem(ROLE_KEY, String(roleValue[0]));
            }
        }

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

    /** Авторизован только при наличии неистёкшего JWT. Истёкший токен сразу очищается. */
    isAuthenticated(): boolean {
        const token = this.getToken();
        if (!token) return false;

        const payload = this.decodeJwt(token);
        if (!payload) {
            this.logout();
            return false;
        }

        const expiresAt = payload['exp'];
        if (typeof expiresAt === 'number' && expiresAt * 1000 <= Date.now()) {
            this.logout();
            return false;
        }

        return true;
    }

    /** Остаток жизни текущего JWT в миллисекундах (null — токена или exp нет). */
    getTokenRemainingMs(): number | null {
        const token = this.getToken();
        if (!token) return null;

        const payload = this.decodeJwt(token);
        const expiresAt = payload?.['exp'];
        if (typeof expiresAt !== 'number') return null;

        return expiresAt * 1000 - Date.now();
    }

    getLogin(): string {
        return localStorage.getItem(LOGIN_KEY) ?? '';
    }

    /** Активная роль текущей сессии, выбранная на промежуточном экране. */
    getActiveRole(): UserRole | null {
        const role = localStorage.getItem(ACTIVE_ROLE_KEY);
        return role ? (role as UserRole) : null;
    }

    /** Все роли пользователя (из claim "roles", выданного бэкендом). */
    getRoles(): UserRole[] {
        const rolesJson = localStorage.getItem(ROLES_KEY);
        if (rolesJson) {
            try {
                const roles: unknown = JSON.parse(rolesJson);
                if (Array.isArray(roles)) {
                    return roles.filter((role): role is string => typeof role === 'string') as UserRole[];
                }
            } catch {
                // Повреждённое значение — используем роль из claim "role".
            }
        }

        const role = localStorage.getItem(ROLE_KEY);
        return role ? [role as UserRole] : [];
    }

    hasMultipleRoles(): boolean {
        return this.getRoles().length > 1;
    }

    /** Мультиролевому пользователю нужно выбрать активную роль для этой сессии. */
    needsRoleSelection(): boolean {
        return this.hasMultipleRoles() && this.getActiveRole() === null;
    }

    getRole(): UserRole {
        return (localStorage.getItem(ROLE_KEY) as UserRole) ?? 'portal_user';
    }

    /** Стартовый маршрут интерфейса для роли. */
    homeUrlForRole(role: UserRole | string | null | undefined): string {
        switch (role) {
            case 'admin':
                return '/admin';
            case 'tenant_admin':
                return '/admin/tenants';
            case 'portal_user':
                return '/portal';
            case 'agent':
            case 'manager':
            default:
                return '/dashboard';
        }
    }

    isAdmin(): boolean {
        return this.getRole() === 'admin';
    }

    canRead(): boolean {
        const role = this.getRole();
        return role === 'admin' || role === 'agent' || role === 'manager';
    }

    isPortalUser(): boolean {
        const activeRole = this.getActiveRole();
        if (activeRole) return activeRole === 'portal_user';

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
        localStorage.removeItem(ACTIVE_ROLE_KEY);
        localStorage.removeItem(LOGIN_KEY);
    }
}