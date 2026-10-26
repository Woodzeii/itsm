import { Injectable } from '@angular/core';
import { Observable, of, throwError } from 'rxjs';
import { AuthApi } from './auth-api';
import {
    LoginRequest,
    LoginResponse,
    SkipTwoFactorSetupRequest,
    VerifyTwoFactorRequest,
    VerifyTwoFactorResponse,
} from './auth.models';

@Injectable()
export class MockAuthApiService extends AuthApi {
    private readonly pendingUsers = new Map<string, { userId: number; username: string; isSetup: boolean }>();
    private readonly enrolledUsers = new Set<string>();
    private nextUserId = 1;
    private nextChallengeId = 1;

    login(request: LoginRequest): Observable<LoginResponse> {
        if (!request.username.trim() || !request.password) {
            return throwError(() => new Error('Логин и пароль обязательны.'));
        }
        const userId = this.nextUserId++;
        const username = request.username.trim();
        const role = roleFor(username);
        const isSetup = !this.enrolledUsers.has(username.toLocaleLowerCase());
        const challengeId = String(this.nextChallengeId++);
        this.pendingUsers.set(challengeId, { userId, username, isSetup });
        return of({
            requiresTwoFactor: true,
            requiresTwoFactorSetup: isSetup,
            canSkipTwoFactorSetup: isSetup && role === 'portal_user',
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
        return of(createTokenResponse(pending.userId, pending.username));
    }

    skipTwoFactorSetup(request: SkipTwoFactorSetupRequest): Observable<VerifyTwoFactorResponse> {
        const pending = this.pendingUsers.get(request.challengeId);
        if (!pending || !pending.isSetup || roleFor(pending.username) !== 'portal_user') {
            return throwError(() => new Error('Настройку 2FA нельзя пропустить.'));
        }

        this.pendingUsers.delete(request.challengeId);
        return of(createTokenResponse(pending.userId, pending.username));
    }
}

function rolesFor(username: string): string[] {
    const normalizedLogin = username.toLocaleLowerCase();
    if (normalizedLogin === 'admin') return ['admin', 'agent'];
    if (normalizedLogin === 'agent') return ['agent'];
    if (normalizedLogin === 'manager') return ['manager'];
    if (normalizedLogin === 'tenant_admin') return ['tenant_admin'];
    return ['portal_user'];
}

function roleFor(username: string): string {
    return rolesFor(username)[0];
}

function createTokenResponse(userId: number, username: string): VerifyTwoFactorResponse {
    const roles = rolesFor(username);
    const claims = encodeBase64Url(JSON.stringify({ sub: String(userId), Login: username, role: roles[0], roles }));
    return { accessToken: `mock.${claims}.development`, tokenType: 'Bearer', expiresIn: 3600 };
}

function encodeBase64Url(value: string): string {
    const bytes = new TextEncoder().encode(value);
    let binary = '';
    for (const byte of bytes) binary += String.fromCharCode(byte);
    return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/g, '');
}
