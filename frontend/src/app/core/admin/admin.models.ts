// ==================== Audit Log ====================
// ⚠️ НЕ РЕАЛИЗОВАНО В BACKEND. Хардкод в AdminController.

export interface AuditLogEntry {
    id: number;
    userId: number | null;
    userFullName: string | null;
    entityType: string;
    entityId: number;
    action: string;
    oldValues: string | null;
    newValues: string | null;
    ipAddress: string | null;
    createdAt: string;
}

// ==================== User Summary ====================

export interface UserSummary {
    id: number;
    username: string;
    email: string;
    fullName: string;
    department: string | null;
    managerId: number | null;
    tenantId: number | null;
    roleIds: number[];
    isActive: boolean;
}

export interface CreateAdminUserRequest {
    username: string;
    email: string;
    fullName: string;
    department: string;
    managerId: number | null;
    tenantId: number | null;
    initialPassword: string;
    isActive: boolean;
    roleIds: number[];
}

export interface UpdateAdminUserRequest {
    username: string;
    email: string;
    fullName: string;
    department: string | null;
    managerId: number | null;
    tenantId: number | null;
    isActive: boolean;
    roleIds: number[];
}

// ==================== Role Summary ====================

export interface RoleSummary {
    id: number;
    code: string;
    name: string;
}