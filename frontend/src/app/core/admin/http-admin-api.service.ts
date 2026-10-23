import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AdminApi } from './admin-api';
import {
    AuditLogEntry,
    CreateAdminUserRequest,
    RoleSummary,
    UpdateAdminUserRequest,
    UserSummary,
} from './admin.models';

@Injectable()
export class HttpAdminApiService extends AdminApi {
    private readonly http = inject(HttpClient);
    private readonly baseUrl = environment.apiBaseUrl;

    getAuditLog(): Observable<AuditLogEntry[]> {
        return this.http.get<AuditLogEntry[]>(`${this.baseUrl}/admin/audit-log`);
    }

    getUsers(): Observable<UserSummary[]> {
        return this.http.get<{ items: UserSummary[] }>(`${this.baseUrl}/users`, {
            params: { page: 1, pageSize: 200 },
        }).pipe(map(result => result.items));
    }

    getRoles(): Observable<RoleSummary[]> {
        return this.http.get<RoleSummary[]>(`${this.baseUrl}/users/roles`);
    }

    createUser(request: CreateAdminUserRequest): Observable<UserSummary> {
        return this.http.post<UserSummary>(`${this.baseUrl}/users`, {
            username: request.username,
            email: request.email,
            password: request.initialPassword,
            fullName: request.fullName,
            department: request.department,
            managerId: request.managerId,
            isActive: request.isActive,
            roleIds: request.roleIds,
        });
    }

    updateUser(id: number, request: UpdateAdminUserRequest): Observable<UserSummary> {
        return this.http.put<UserSummary>(`${this.baseUrl}/users/${id}`, request);
    }
}