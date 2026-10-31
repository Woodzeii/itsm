import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { Subject, of, throwError } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { SessionResponse } from '../../core/auth/auth.models';
import { RoleSelectComponent } from './role-select';

describe('RoleSelectComponent', () => {
    let auth: {
        loadSession: ReturnType<typeof vi.fn>;
        selectRole: ReturnType<typeof vi.fn>;
        homeUrlForRole: ReturnType<typeof vi.fn>;
        getLogin: ReturnType<typeof vi.fn>;
        logout: ReturnType<typeof vi.fn>;
    };
    let session$: Subject<SessionResponse>;
    let router: Router;

    const multiRoleSession: SessionResponse = {
        userId: 9,
        login: 'm.multi',
        email: 'm.multi@itsm.local',
        roles: ['admin', 'agent'],
        activeRole: null,
        requiresRoleSelection: true,
    };

    beforeEach(async () => {
        session$ = new Subject<SessionResponse>();
        auth = {
            loadSession: vi.fn().mockReturnValue(session$),
            selectRole: vi.fn(),
            homeUrlForRole: vi.fn((role: string) => role === 'admin' ? '/admin' : '/dashboard'),
            getLogin: vi.fn().mockReturnValue('m.multi'),
            logout: vi.fn(),
        };

        await TestBed.configureTestingModule({
            imports: [RoleSelectComponent],
            providers: [
                provideRouter([]),
                { provide: AuthService, useValue: auth },
            ],
        }).compileComponents();

        router = TestBed.inject(Router);
    });

    function createComponent(): RoleSelectComponent {
        const fixture = TestBed.createComponent(RoleSelectComponent);
        fixture.detectChanges();
        return fixture.componentInstance;
    }

    it('renders the workspaces returned by the backend session', () => {
        const component = createComponent();
        session$.next(multiRoleSession);
        session$.complete();

        expect(auth.loadSession).toHaveBeenCalled();
        expect(component.options.map(option => option.role)).toEqual(['agent', 'admin']);
        expect(component.options.map(option => option.title)).toEqual([
            'Интерфейс инженера',
            'Админка',
        ]);
        expect(component.loading).toBe(false);
    });

    it('selects a role through AuthService and opens the matching workspace', () => {
        const navigate = vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);
        const component = createComponent();
        session$.next(multiRoleSession);
        auth.selectRole.mockReturnValue(of({
            accessToken: 'scoped-token',
            tokenType: 'Bearer',
            expiresIn: 3600,
            activeRole: 'admin',
            roles: ['admin', 'agent'],
        }));

        component.choose('admin');

        expect(auth.selectRole).toHaveBeenCalledWith('admin');
        expect(navigate).toHaveBeenCalledWith('/admin');
        expect(component.busy).toBe(false);
    });

    it('redirects straight to the workspace when selection is not required', () => {
        const navigate = vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);
        createComponent();

        session$.next({
            ...multiRoleSession,
            roles: ['agent'],
            activeRole: 'agent',
            requiresRoleSelection: false,
        });

        expect(navigate).toHaveBeenCalledWith('/dashboard');
    });

    it('shows the backend error and lets the user retry', () => {
        const component = createComponent();

        session$.error(new Error('Сервис недоступен'));

        expect(component.loading).toBe(false);
        expect(component.error).toBe('Сервис недоступен');
    });

    it('shows an error returned by the role selection endpoint', () => {
        const component = createComponent();
        session$.next(multiRoleSession);
        // Ошибка в формате HttpErrorResponse от бэкенда.
        auth.selectRole.mockReturnValue(throwError(() => ({
            error: { message: 'Выбранная роль недоступна для этой учётной записи' },
        })));

        component.choose('admin');

        expect(component.busy).toBe(false);
        expect(component.selectedRole).toBeNull();
        expect(component.error).toBe('Выбранная роль недоступна для этой учётной записи');
    });

    it('logs out and returns to the login page', () => {
        const navigate = vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);
        const component = createComponent();

        component.logout();

        expect(auth.logout).toHaveBeenCalled();
        expect(navigate).toHaveBeenCalledWith('/login');
    });
});
