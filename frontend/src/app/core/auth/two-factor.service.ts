import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
    DisableTwoFactorRequest,
    TwoFactorSetupResponse,
    TwoFactorStatusResponse,
    VerifyTwoFactorSetupRequest,
} from './two-factor.models';

const API = `${environment.apiBaseUrl}/auth`;

@Injectable({ providedIn: 'root' })
export class TwoFactorService {
    private readonly http = inject(HttpClient);

    setup(): Observable<TwoFactorSetupResponse> {
        return this.http.post<TwoFactorSetupResponse>(`${API}/2fa/setup`, {});
    }

    verifySetup(req: VerifyTwoFactorSetupRequest): Observable<{ message: string }> {
        return this.http.post<{ message: string }>(`${API}/2fa/verify-setup`, req);
    }

    status(): Observable<TwoFactorStatusResponse> {
        return this.http.get<TwoFactorStatusResponse>(`${API}/2fa/status`);
    }

    disable(req: DisableTwoFactorRequest): Observable<{ message: string }> {
        return this.http.post<{ message: string }>(`${API}/2fa/disable`, req);
    }
}