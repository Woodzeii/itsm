import { Observable } from 'rxjs';
import {
    AuditLogEntry,
    CreateAdminUserRequest,
    RoleSummary,
    UpdateAdminUserRequest,
    UserSummary,
} from './admin.models';

/** API for administrative audit and user management. */
export abstract class AdminApi {
    abstract getAuditLog(): Observable<AuditLogEntry[]>;
    abstract getUsers(): Observable<UserSummary[]>;
    abstract getRoles(): Observable<RoleSummary[]>;
    abstract createUser(request: CreateAdminUserRequest): Observable<UserSummary>;
    abstract updateUser(id: number, request: UpdateAdminUserRequest): Observable<UserSummary>;
}