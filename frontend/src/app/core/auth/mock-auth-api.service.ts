import { Injectable } from '@angular/core';
import { Observable, of, throwError } from 'rxjs';
import { AuthApi } from './auth-api';
import { LoginRequest, LoginResponse, VerifyTwoFactorRequest, VerifyTwoFactorResponse } from './auth.models';

@Injectable()
export class MockAuthApiService extends AuthApi {
    private readonly pendingUsers = new Map<number, string>();
    private nextUserId = 1;

    login(request: LoginRequest): Observable<LoginResponse> {
        if (!request.username.trim() || !request.password) {
            return throwError(() => new Error('Логин и пароль обязательны.'));
        }
        const userId = this.nextUserId++;
        this.pendingUsers.set(userId, request.username.trim());
        return of({ requiresTwoFactor: true, userId, message: 'Mock authentication challenge created.' });
    }

    verifyTwoFactor(request: VerifyTwoFactorRequest): Observable<VerifyTwoFactorResponse> {
        const username = this.pendingUsers.get(request.userId);
        if (!username) return throwError(() => new Error('Сессия входа не найдена. Начните вход заново.'));
        if (!/^\d{6}$/.test(request.code)) return throwError(() => new Error('Введите шестизначный код.'));

        this.pendingUsers.delete(request.userId);
        const normalizedLogin = username.toLocaleLowerCase();
        const role = ['admin', 'agent', 'manager', 'tenant_admin'].includes(normalizedLogin) ? normalizedLogin : 'portal_user';
        const claims = encodeBase64Url(JSON.stringify({ sub: String(request.userId), Login: username, role }));
        return of({ accessToken: `mock.${claims}.development`, tokenType: 'Bearer', expiresIn: 3600 });
    }
}

function encodeBase64Url(value: string): string {
    const bytes = new TextEncoder().encode(value);
    let binary = '';
    for (const byte of bytes) binary += String.fromCharCode(byte);
    return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/g, '');
}
