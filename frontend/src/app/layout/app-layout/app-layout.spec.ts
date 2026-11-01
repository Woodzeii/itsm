import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { AppLayoutComponent } from './app-layout';

describe('AppLayoutComponent role switch', () => {
    let auth: {
        isAdmin: ReturnType<typeof vi.fn>;
        canRead: ReturnType<typeof vi.fn>;
        isTenantAdmin: ReturnType<typeof vi.fn>;
        hasMultipleRoles: ReturnType<typeof vi.fn>;
        getLogin: ReturnType<typeof vi.fn>;
        getRole: ReturnType<typeof vi.fn>;
        getTokenRemainingMs: ReturnType<typeof vi.fn>;
        logout: ReturnType<typeof vi.fn>;
    };
    let router: Router;

    beforeEach(async () => {
        auth = {
            isAdmin: vi.fn().mockReturnValue(true),
            canRead: vi.fn().mockReturnValue(true),
            isTenantAdmin: vi.fn().mockReturnValue(false),
            hasMultipleRoles: vi.fn().mockReturnValue(true),
            getLogin: vi.fn().mockReturnValue('m.multi'),
            getRole: vi.fn().mockReturnValue('admin'),
            getTokenRemainingMs: vi.fn().mockReturnValue(null),
            logout: vi.fn(),
        };

        await TestBed.configureTestingModule({
            imports: [AppLayoutComponent],
            providers: [
                provideRouter([]),
                { provide: AuthService, useValue: auth },
            ],
        }).compileComponents();

        router = TestBed.inject(Router);
    });

    function switchButton(): HTMLButtonElement | null {
        const fixture = TestBed.createComponent(AppLayoutComponent);
        fixture.detectChanges();
        return fixture.nativeElement.querySelector('[aria-label="Сменить активную роль"]');
    }

    it('shows the switch-role button next to logout for multi-role users', () => {
        const button = switchButton();

        expect(button).toBeTruthy();
        expect(button!.textContent).toContain('Сменить роль');
    });

    it('opens the role selection screen in switch mode', () => {
        const navigate = vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);
        const fixture = TestBed.createComponent(AppLayoutComponent);
        fixture.detectChanges();
        const button = fixture.nativeElement.querySelector(
            '[aria-label="Сменить активную роль"]',
        ) as HTMLButtonElement;

        button.click();

        expect(navigate).toHaveBeenCalledWith('/select-role?switch=1');
    });

    it('does not show the switch-role button for single-role users', () => {
        auth.hasMultipleRoles.mockReturnValue(false);

        expect(switchButton()).toBeNull();
    });

    it('automatically returns to /login when the token expires', () => {
        vi.useFakeTimers();
        try {
            auth.getTokenRemainingMs.mockReturnValue(1000);
            const navigate = vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);
            const fixture = TestBed.createComponent(AppLayoutComponent);
            fixture.detectChanges();

            vi.advanceTimersByTime(1000);

            expect(auth.logout).toHaveBeenCalledOnce();
            expect(navigate).toHaveBeenCalledWith('/login');
            fixture.destroy();
        } finally {
            vi.useRealTimers();
        }
    });
});
