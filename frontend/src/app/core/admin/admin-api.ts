import { Observable } from 'rxjs';
import { AuditLogEntry, RoleSummary, UserSummary } from './admin.models';

/**
 * ⚠️ НЕ РЕАЛИЗОВАНО В BACKEND.
 * Хардкод-данные в AdminController (backend) и MockAdminApiService (frontend).
 */
export abstract class AdminApi {
    abstract getAuditLog(): Observable<AuditLogEntry[]>;
    abstract getUsers(): Observable<UserSummary[]>;
    abstract getRoles(): Observable<RoleSummary[]>;
}