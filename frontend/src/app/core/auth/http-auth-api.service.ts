import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { AuthApi } from './auth-api';
import {
    LoginRequest,
    LoginResponse,
    SkipTwoFactorSetupRequest,
    VerifyTwoFactorRequest,
    VerifyTwoFactorResponse,
} from './auth.models';
import { environment } from '../../../environments/environment';

@Injectable()
export class HttpAuthApiService extends AuthApi {
    private readonly http = inject(HttpClient);
    private readonly baseUrl = environment.apiBaseUrl;

    login(request: LoginRequest) {
        return this.http.post<LoginResponse>(`${this.baseUrl}/auth/login`, request);
    }

    verifyTwoFactor(request: VerifyTwoFactorRequest) {
        return this.http.post<VerifyTwoFactorResponse>(`${this.baseUrl}/auth/verify-2fa`, request);
    }

    skipTwoFactorSetup(request: SkipTwoFactorSetupRequest) {
        return this.http.post<VerifyTwoFactorResponse>(`${this.baseUrl}/auth/skip-2fa-setup`, request);
    }
}
