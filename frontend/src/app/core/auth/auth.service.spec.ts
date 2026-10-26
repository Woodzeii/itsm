import { TestBed } from '@angular/core/testing';
import { Observable, of } from 'rxjs';
import { AuthApi } from './auth-api';
import { LoginRequest, LoginResponse, SkipTwoFactorSetupRequest, VerifyTwoFactorRequest, VerifyTwoFactorResponse } from './auth.models';
import { AuthService } from './auth.service';

class AuthApiStub extends AuthApi {
    override login(_request: LoginRequest): Observable<LoginResponse> {
        return of({ requiresTwoFactor: true, requiresTwoFactorSetup: false, canSkipTwoFactorSetup: false, userId: 1, challengeId: '1', secret: '', otpAuthUri: '', qrCodeImageBase64: '', message: '' });
    }

    override verifyTwoFactor(_request: VerifyTwoFactorRequest): Observable<VerifyTwoFactorResponse> {
        return of({ accessToken: '', tokenType: 'Bearer', expiresIn: 3600 });
    }

    override skipTwoFactorSetup(_request: SkipTwoFactorSetupRequest): Observable<VerifyTwoFactorResponse> {
        return of({ accessToken: '', tokenType: 'Bearer', expiresIn: 3600 });
    }
}

describe('AuthService active roles', () => {
    beforeEach(() => {
        localStorage.clear();
        sessionStorage.clear();
    });

    function createService(): AuthService {
        TestBed.configureTestingModule({
            providers: [AuthService, { provide: AuthApi, useClass: AuthApiStub }],
        });
        return TestBed.inject(AuthService);
    }

    afterEach(() => {
        localStorage.clear();
        sessionStorage.clear();
    });

    it('does not require role selection for a single role', () => {
        localStorage.setItem('itsm_user_roles', JSON.stringify(['agent']));
        localStorage.setItem('itsm_user_role', 'agent');
        const service = createService();

        expect(service.getRoles()).toEqual(['agent']);
        expect(service.requiresRoleSelection()).toBe(false);
    });

    it('requires selection and activates only an available role', () => {
        localStorage.setItem('itsm_access_token', 'test-token');
        localStorage.setItem('itsm_user_roles', JSON.stringify(['admin', 'agent']));
        localStorage.setItem('itsm_user_role', 'admin');
        sessionStorage.setItem('itsm_role_selection_pending', 'true');
        const service = createService();

        expect(service.requiresRoleSelection()).toBe(true);
        expect(service.completeRoleSelection('agent')).toBe(true);
        expect(service.getRole()).toBe('agent');
        expect(service.requiresRoleSelection()).toBe(false);

        expect(service.completeRoleSelection('portal_user')).toBe(false);
        expect(service.getRole()).toBe('agent');
    });
});
