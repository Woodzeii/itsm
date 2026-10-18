import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AdminApi } from './admin-api';
import { AuditLogEntry, RoleSummary, UserSummary } from './admin.models';

@Injectable()
export class HttpAdminApiService extends AdminApi {
    private readonly http = inject(HttpClient);
    private readonly baseUrl = `${environment.apiBaseUrl}/admin`;

    getAuditLog(): Observable<AuditLogEntry[]> {
        return this.http.get<AuditLogEntry[]>(`${this.baseUrl}/audit-log`);
    }

    getUsers(): Observable<UserSummary[]> {
        return this.http.get<UserSummary[]>(`${this.baseUrl}/users`);
    }

    getRoles(): Observable<RoleSummary[]> {
        return this.http.get<RoleSummary[]>(`${this.baseUrl}/roles`);
    }
}