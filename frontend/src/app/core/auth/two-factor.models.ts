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
}

export interface DisableTwoFactorRequest {
    password: string;
}