import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../../environments/environment';
import { TenantUserQuota } from './workspace.models';

@Injectable({ providedIn: 'root' })
export class TenantUserQuotaService {
    private readonly http = inject(HttpClient);
    private readonly baseUrl = environment.apiBaseUrl;

    getCurrentTenantQuota() {
        return this.http.get<TenantUserQuota>(`${this.baseUrl}/tenants/me/user-quota`);
    }
}
