import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
    LoginRequest,
    LoginResponse,
    VerifyTwoFactorRequest,
    VerifyTwoFactorResponse,
} from './auth.models';

const API = environment.apiUrl;
const TOKEN_KEY = 'itsm_access_token';
const AUTH_KEY = 'itsm_is_authenticated';
const ROLE_KEY = 'itsm_user_role';
const LOGIN_KEY = 'itsm_login';

export type UserRole = 'admin' | 'agent' | 'manager' | 'portal_user' | 'tenant_admin';

@Injectable({ providedIn: 'root' })
export class AuthService {
    private readonly http = inject(HttpClient);

    // ================= API =================

    login(username: string, password: string): Observable<LoginResponse> {
        const body: LoginRequest = { username, password };
        return this.http.post<LoginResponse>(`${API}/api/auth/login`, body);
    }

    verifyTwoFactor(userId: number, code: string): Observable<VerifyTwoFactorResponse> {
        const body: VerifyTwoFactorRequest = { userId, code };
        return this.http
            .post<VerifyTwoFactorResponse>(`${API}/api/auth/verify-2fa`, body)
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
        const loginValue = payload['Login'] ?? payload['login'];

        if (typeof roleValue === 'string') {
            localStorage.setItem(ROLE_KEY, roleValue);
        } else if (Array.isArray(roleValue) && roleValue.length > 0) {
            localStorage.setItem(ROLE_KEY, String(roleValue[0]));
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

    isAuthenticated(): boolean {
        return !!this.getToken();
    }

    getLogin(): string {
        return localStorage.getItem(LOGIN_KEY) ?? '';
    }

    getRole(): UserRole {
        return (localStorage.getItem(ROLE_KEY) as UserRole) ?? 'portal_user';
    }

    isAdmin(): boolean {
        return this.getRole() === 'admin';
    }

    canRead(): boolean {
        const role = this.getRole();
        return role === 'admin' || role === 'agent';
    }

    logout(): void {
        localStorage.removeItem(TOKEN_KEY);
        localStorage.removeItem(AUTH_KEY);
        localStorage.removeItem(ROLE_KEY);
        localStorage.removeItem(LOGIN_KEY);
    }
}