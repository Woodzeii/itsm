export interface LoginRequest {
    username: string;
    password: string;
}

export interface LoginResponse {
    requiresTwoFactor: boolean;
    userId: number;
    message: string;
}

export interface VerifyTwoFactorRequest {
    userId: number;
    code: string;
}

export interface VerifyTwoFactorResponse {
    accessToken: string;
    tokenType: string;
    expiresIn: number;
}