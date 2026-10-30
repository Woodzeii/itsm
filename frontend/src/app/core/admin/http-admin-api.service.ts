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
        return this.http.get<unknown>(`${this.baseUrl}/users`, {
            params: { page: 1, pageSize: 200 },
        }).pipe(map(parseUsersResponse));
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
            tenantId: request.tenantId,
            isActive: request.isActive,
            roleIds: request.roleIds,
        });
    }

    updateUser(id: number, request: UpdateAdminUserRequest): Observable<UserSummary> {
        return this.http.put<UserSummary>(`${this.baseUrl}/users/${id}`, request);
    }
}

function parseUsersResponse(response: unknown): UserSummary[] {
    if (Array.isArray(response)) {
        return response.map(parseUserSummary);
    }

    if (!isRecord(response)) {
        throw new Error('Сервер вернул список пользователей в неожиданном формате.');
    }

    const items = response['items'] ?? response['Items'];
    if (Array.isArray(items)) {
        return items.map(parseUserSummary);
    }

    const data = response['data'] ?? response['Data'];
    if (data !== undefined) {
        return parseUsersResponse(data);
    }

    throw new Error('Сервер вернул список пользователей в неожиданном формате.');
}

function parseUserSummary(value: unknown): UserSummary {
    if (!isRecord(value)) {
        throw new Error('Сервер вернул пользователя в неожиданном формате.');
    }

    const id = value['id'] ?? value['Id'];
    const username = value['username'] ?? value['Username'];
    const email = value['email'] ?? value['Email'];
    const fullName = value['fullName'] ?? value['FullName'];
    const department = value['department'] ?? value['Department'] ?? null;
    const managerId = value['managerId'] ?? value['ManagerId'] ?? null;
    const tenantId = value['tenantId'] ?? value['TenantId'] ?? null;
    const roleIds = value['roleIds'] ?? value['RoleIds'];
    const isActive = value['isActive'] ?? value['IsActive'];

    if (
        typeof id !== 'number'
        || typeof username !== 'string'
        || typeof email !== 'string'
        || typeof fullName !== 'string'
        || (department !== null && typeof department !== 'string')
        || (managerId !== null && typeof managerId !== 'number')
        || (tenantId !== null && typeof tenantId !== 'number')
        || !Array.isArray(roleIds)
        || !roleIds.every(roleId => typeof roleId === 'number')
        || typeof isActive !== 'boolean'
    ) {
        throw new Error('Сервер вернул пользователя в неожиданном формате.');
    }

    return { id, username, email, fullName, department, managerId, tenantId, roleIds, isActive };
}

function isRecord(value: unknown): value is Record<string, unknown> {
    return typeof value === 'object' && value !== null && !Array.isArray(value);
}