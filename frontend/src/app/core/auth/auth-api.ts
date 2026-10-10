import { Observable } from 'rxjs';
import { LoginRequest, LoginResponse, VerifyTwoFactorRequest, VerifyTwoFactorResponse } from './auth.models';

export abstract class AuthApi {
    abstract login(request: LoginRequest): Observable<LoginResponse>;
    abstract verifyTwoFactor(request: VerifyTwoFactorRequest): Observable<VerifyTwoFactorResponse>;
}
