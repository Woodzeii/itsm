import { Observable } from 'rxjs';
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

export abstract class AuthApi {
    abstract login(request: LoginRequest): Observable<LoginResponse>;
    abstract verifyTwoFactor(request: VerifyTwoFactorRequest): Observable<VerifyTwoFactorResponse>;
    abstract skipTwoFactorSetup(request: SkipTwoFactorSetupRequest): Observable<VerifyTwoFactorResponse>;

    /** Роли пользователя и активная роль текущей сессии (источник — бэкенд). */
    abstract getSession(accessToken: string): Observable<SessionResponse>;

    /** Фиксирует активную роль сессии и возвращает токен, ограниченный этой ролью. */
    abstract selectRole(request: SelectRoleRequest, accessToken: string): Observable<SelectRoleResponse>;
}
