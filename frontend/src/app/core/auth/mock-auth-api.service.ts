import { Injectable } from '@angular/core';
import { Observable, of, throwError } from 'rxjs';
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

interface MockPendingUser {
    userId: number;
    username: string;
    isSetup: boolean;
    roles: string[];
}

interface MockSession {
    userId: number;
    username: string;
    roles: string[];
    activeRole: string | null;
}

@Injectable()
export class MockAuthApiService extends AuthApi {
    private readonly pendingUsers = new Map<string, MockPendingUser>();
    private readonly enrolledUsers = new Set<string>();
    private readonly issuedSessions = new Map<string, MockSession>();
    private nextUserId = 1;
    private nextChallengeId = 1;

    login(request: LoginRequest): Observable<LoginResponse> {
        if (!request.username.trim() || !request.password) {
            return throwError(() => new Error('Логин и пароль обязательны.'));
        }
        const userId = this.nextUserId++;
        const username = request.username.trim();
        const roles = rolesFor(username);
        const isPortalOnly = roles.length === 1 && roles[0] === 'portal_user';
        const isSetup = !this.enrolledUsers.has(username.toLocaleLowerCase());
        const challengeId = String(this.nextChallengeId++);
        this.pendingUsers.set(challengeId, { userId, username, isSetup, roles });
        return of({
            requiresTwoFactor: true,
            requiresTwoFactorSetup: isSetup,
            canSkipTwoFactorSetup: isSetup && isPortalOnly,
            userId,
            challengeId,
            secret: '',
            otpAuthUri: '',
            qrCodeImageBase64: '',
            message: isSetup
                ? 'Mock mode: enter any 6-digit code to simulate 2FA setup.'
                : 'Mock mode: enter any 6-digit code.',
        });
    }

    verifyTwoFactor(request: VerifyTwoFactorRequest): Observable<VerifyTwoFactorResponse> {
        const pending = this.pendingUsers.get(request.challengeId);
        if (!pending) return throwError(() => new Error('Сессия входа не найдена. Начните вход заново.'));
        if (!/^\d{6}$/.test(request.code)) return throwError(() => new Error('Введите шестизначный код.'));

        this.pendingUsers.delete(request.challengeId);
        if (pending.isSetup) this.enrolledUsers.add(pending.username.toLocaleLowerCase());
        return of(this.createTokenResponse(pending.userId, pending.username, pending.roles, null));
    }

    skipTwoFactorSetup(request: SkipTwoFactorSetupRequest): Observable<VerifyTwoFactorResponse> {
        const pending = this.pendingUsers.get(request.challengeId);
        const isPortalOnly = pending?.roles.length === 1 && pending.roles[0] === 'portal_user';
        if (!pending || !pending.isSetup || !isPortalOnly) {
            return throwError(() => new Error('Настройку 2FA нельзя пропустить.'));
        }

        this.pendingUsers.delete(request.challengeId);
        return of(this.createTokenResponse(pending.userId, pending.username, pending.roles, null));
    }

    getSession(accessToken: string): Observable<SessionResponse> {
        const session = this.issuedSessions.get(accessToken);
        if (!session) {
            return throwError(() => new Error('Сессия не найдена. Начните вход заново.'));
        }

        return of({
            userId: session.userId,
            login: session.username,
            email: `${session.username}@itsm.local`,
            roles: [...session.roles],
            activeRole: session.activeRole,
            requiresRoleSelection: session.roles.length > 1 && !session.activeRole,
        });
    }

    selectRole(request: SelectRoleRequest, accessToken: string): Observable<SelectRoleResponse> {
        const session = this.issuedSessions.get(accessToken);
        if (!session) {
            return throwError(() => new Error('Сессия не найдена. Начните вход заново.'));
        }

        const activeRole = session.roles.find(role => role === request.role);
        if (!activeRole) {
            return throwError(() => new Error('Выбранная роль недоступна для этой учётной записи.'));
        }

        const response = this.createTokenResponse(
            session.userId,
            session.username,
            session.roles,
            activeRole,
        );

        return of({ ...response, activeRole, roles: [...session.roles] });
    }

    private createTokenResponse(
        userId: number,
        username: string,
        roles: string[],
        activeRole: string | null,
    ): VerifyTwoFactorResponse {
        const claims: Record<string, string> = {
            sub: String(userId),
            Login: username,
            roles: JSON.stringify(roles),
        };
        if (activeRole) {
            claims['role'] = activeRole;
            claims['activeRole'] = activeRole;
        } else if (roles.length === 1) {
            claims['role'] = roles[0];
            claims['activeRole'] = roles[0];
        }

        const token = `mock.${encodeBase64Url(JSON.stringify(claims))}.development`;
        this.issuedSessions.set(token, {
            userId,
            username,
            roles: [...roles],
            activeRole,
        });

        return { accessToken: token, tokenType: 'Bearer', expiresIn: 3600 };
    }
}

function rolesFor(username: string): string[] {
    const normalizedLogin = username.toLocaleLowerCase();
    if (normalizedLogin === 'multi') return ['admin', 'agent'];
    return [['admin', 'agent', 'manager', 'tenant_admin'].includes(normalizedLogin)
        ? normalizedLogin
        : 'portal_user'];
}

function encodeBase64Url(value: string): string {
    const bytes = new TextEncoder().encode(value);
    let binary = '';
    for (const byte of bytes) binary += String.fromCharCode(byte);
    return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/g, '');
}
