export interface TwoFactorSetupResponse {
    secret: string;
    otpAuthUri: string;
    qrCodeImageBase64: string;
    issuer: string;
}

export interface VerifyTwoFactorSetupRequest {
    code: string;
}

export interface TwoFactorStatusResponse {
    isEnabled: boolean;
    enabledAt: string | null;
    isRequired: boolean;
    graceEndsAt: string | null;
}

export interface DisableTwoFactorRequest {
    password: string;
}

export interface ChangePasswordRequest {
    currentPassword: string;
    newPassword: string;
}