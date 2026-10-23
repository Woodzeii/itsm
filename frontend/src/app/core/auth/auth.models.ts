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