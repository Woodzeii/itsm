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
// ⚠️ НЕ РЕАЛИЗОВАНО В BACKEND.

export interface UserSummary {
    id: number;
    username: string;
    email: string;
    fullName: string;
    department: string | null;
    managerId: number | null;
    roleIds: number[];
    isActive: boolean;
}

// ==================== Role Summary ====================
// ⚠️ НЕ РЕАЛИЗОВАНО В BACKEND.

export interface RoleSummary {
    id: number;
    code: string;
    name: string;
}