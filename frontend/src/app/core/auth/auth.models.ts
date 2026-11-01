export interface LoginRequest {
    username: string;
    password: string;
}

export interface LoginResponse {
    requiresTwoFactor: boolean;
    requiresTwoFactorSetup: boolean;
    canSkipTwoFactorSetup: boolean;
    userId: number;
    challengeId: string;
    secret: string;
    otpAuthUri: string;
    qrCodeImageBase64: string;
    message: string;
}

export interface VerifyTwoFactorRequest {
    challengeId: string;
    code: string;
}

export interface SkipTwoFactorSetupRequest {
    challengeId: string;
}

export interface VerifyTwoFactorResponse {
    accessToken: string;
    tokenType: string;
    expiresIn: number;
}

/** Ответ GET /api/auth/session — роли и активная роль сессии берутся с бэкенда. */
export interface SessionResponse {
    userId: number;
    login: string;
    email: string;
    roles: string[];
    activeRole: string | null;
    requiresRoleSelection: boolean;
}

export interface SelectRoleRequest {
    role: string;
}

/** Ответ POST /api/auth/session/role — новый токен, ограниченный выбранной ролью. */
export interface SelectRoleResponse {
    accessToken: string;
    tokenType: string;
    expiresIn: number;
    activeRole: string;
    roles: string[];
}