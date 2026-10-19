import { Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';
import { delay } from 'rxjs/operators';
import { AdminApi } from './admin-api';
import { AuditLogEntry, RoleSummary, UserSummary } from './admin.models';

/**
 * ⚠️ НЕ РЕАЛИЗОВАНО В BACKEND.
 * Хардкод-данные для фронта. Совпадают с AdminController на backend.
 * Когда backend будет готов — переключить на HttpAdminApiService.
 */
@Injectable()
export class MockAdminApiService extends AdminApi {
    getAuditLog(): Observable<AuditLogEntry[]> {
        const now = Date.now();
        return of<AuditLogEntry[]>([
            {
                id: 1,
                userId: 1,
                userFullName: 'Администратор',
                entityType: 'Asset',
                entityId: 1,
                action: 'created',
                oldValues: null,
                newValues: '{"name":"Ноутбук Lenovo ThinkPad"}',
                ipAddress: '127.0.0.1',
                createdAt: new Date(now - 2 * 60 * 60 * 1000).toISOString(),
            },
            {
                id: 2,
                userId: 1,
                userFullName: 'Администратор',
                entityType: 'Ticket',
                entityId: 5,
                action: 'status_changed',
                oldValues: '{"status":"open"}',
                newValues: '{"status":"in_progress"}',
                ipAddress: '127.0.0.1',
                createdAt: new Date(now - 5 * 60 * 60 * 1000).toISOString(),
            },
            {
                id: 3,
                userId: 2,
                userFullName: 'Иван Иванов',
                entityType: 'User',
                entityId: 2,
                action: 'updated',
                oldValues: '{"department":"Sales"}',
                newValues: '{"department":"IT"}',
                ipAddress: '10.0.0.15',
                createdAt: new Date(now - 24 * 60 * 60 * 1000).toISOString(),
            },
        ]).pipe(delay(150));
    }

    getUsers(): Observable<UserSummary[]> {
        return of<UserSummary[]>([
            { id: 1, username: 'admin', email: 'admin@itsm.local', fullName: 'Администратор Системы', department: 'IT', managerId: null, roleIds: [1], isActive: true },
            { id: 2, username: 'i.ivanov', email: 'i.ivanov@itsm.local', fullName: 'Иван Иванов', department: 'IT', managerId: 1, roleIds: [4], isActive: true },
            { id: 3, username: 'p.petrov', email: 'p.petrov@itsm.local', fullName: 'Пётр Петров', department: 'IT', managerId: 2, roleIds: [3], isActive: true },
            { id: 4, username: 's.sidorov', email: 's.sidorov@itsm.local', fullName: 'Сидор Сидоров', department: 'Бухгалтерия', managerId: null, roleIds: [5], isActive: true },
            { id: 5, username: 'a.anna', email: 'a.anna@itsm.local', fullName: 'Анна Аннова', department: 'Бухгалтерия', managerId: null, roleIds: [5], isActive: false },
        ]).pipe(delay(150));
    }

    getRoles(): Observable<RoleSummary[]> {
        return of<RoleSummary[]>([
            { id: 1, code: 'tenant_admin', name: 'Администратор тенантов' },
            { id: 2, code: 'admin', name: 'Администратор' },
            { id: 3, code: 'agent', name: 'Инженер ТП' },
            { id: 4, code: 'manager', name: 'Руководитель' },
            { id: 5, code: 'portal_user', name: 'Пользователь портала' },
        ]).pipe(delay(150));
    }
}