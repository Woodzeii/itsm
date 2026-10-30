import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AdminApi } from './admin-api';
import {
    AuditLogEntry,
    CreateAdminUserRequest,
    RoleSummary,
    UpdateAdminUserRequest,
    UserSummary,
} from './admin.models';

@Injectable({ providedIn: 'root' })
export class AdminService {
    private readonly api = inject(AdminApi);

    getAuditLog(): Observable<AuditLogEntry[]> {
        return this.api.getAuditLog();
    }

    getUsers(): Observable<UserSummary[]> {
        return this.api.getUsers();
    }

    getRoles(): Observable<RoleSummary[]> {
        return this.api.getRoles();
    }

    createUser(request: CreateAdminUserRequest): Observable<UserSummary> {
        return this.api.createUser(request);
    }

    updateUser(id: number, request: UpdateAdminUserRequest): Observable<UserSummary> {
        return this.api.updateUser(id, request);
    }
}