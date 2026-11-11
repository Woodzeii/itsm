import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { of, Subject } from 'rxjs';
import { AdminService } from '../../core/admin/admin.service';
import { UserSummary } from '../../core/admin/admin.models';
import { WorkspaceService } from '../../core/workspace/workspace.service';
import { AuthService } from '../../core/auth/auth.service';
import { HttpWorkspaceApiService } from '../../core/workspace/http-workspace-api.service';
import { AdminWorkspaceComponent } from './admin-workspace';

describe('AdminWorkspaceComponent user list', () => {
    let tenantSettingsApi: {
        getTenantAdminCreationPolicy: ReturnType<typeof vi.fn>;
        saveAdminSettings: ReturnType<typeof vi.fn>;
        saveTenantAdminCreationPolicy: ReturnType<typeof vi.fn>;
    };

    const user: UserSummary = {
        id: 7,
        username: 'test.user',
        email: 'test@example.com',
        fullName: 'Тестовый Пользователь',
        department: 'IT',
        managerId: null,
        tenantId: null,
        roleIds: [3],
        isActive: true,
    };

    beforeEach(async () => {
        tenantSettingsApi = {
            getTenantAdminCreationPolicy: vi.fn().mockReturnValue(of({
                tenantId: 1,
                allowAdminsCreateAdmins: true,
            })),
            saveAdminSettings: vi.fn().mockReturnValue(of({ allowAdminsCreateAdmins: false })),
            saveTenantAdminCreationPolicy: vi.fn().mockReturnValue(of({
                tenantId: 1,
                allowAdminsCreateAdmins: false,
            })),
        };
        await TestBed.configureTestingModule({
            imports: [AdminWorkspaceComponent],
            providers: [
                provideRouter([]),
                {
                    provide: ActivatedRoute,
                    useValue: { snapshot: { data: { section: 'users' } } },
                },
                {
                    provide: WorkspaceService,
                    useValue: {
                        getAdminSettings: () => of({
                            assignmentMode: 'automatic',
                            manager: '',
                            slaEnabled: true,
                            slaMode: '24/7',
                            workingHours: '',
                            workingDays: [],
                            holidays: [],
                            criticalities: ['Критический'],
                            statuses: [],
                            statusTransitions: {},
                            portalSlaFields: [],
                            reportAccessRoles: [],
                        }),
                    },
                },
                {
                    provide: HttpWorkspaceApiService,
                    useValue: tenantSettingsApi,
                },
                {
                    provide: AdminService,
                    useValue: {
                        getUsers: () => of([user]),
                        getRoles: () => of([{ id: 3, code: 'agent', name: 'Инженер' }]),
                    },
                },
                {
                    provide: AuthService,
                    useValue: {
                        isTenantAdmin: () => false,
                        isTenantOwner: () => false,
                        getLogin: () => 'admin',
                    },
                },
            ],
        }).compileComponents();
    });

    it('renders users returned by the admin API', async () => {
        const fixture = TestBed.createComponent(AdminWorkspaceComponent);
        fixture.detectChanges();
        await fixture.whenStable();
        fixture.detectChanges();

        const pageText = (fixture.nativeElement as HTMLElement).textContent ?? '';
        expect(pageText).toContain(user.fullName);
        expect(pageText).toContain(user.email);
        expect(pageText).not.toContain('Пользователей не найдено');
        expect(
            Array.from(
                fixture.nativeElement.querySelectorAll('.users-table tbody tr td') as NodeListOf<Element>,
                cell => cell.getAttribute('data-label'),
            ),
        ).toEqual(['ФИО', 'Email', 'Username', 'Департамент', 'Руководитель', 'Статус', 'Роли', 'Действия']);
    });

    it('shows the owner badge and disables editing for a local admin', async () => {
        const owner: UserSummary = { ...user, roleIds: [6], username: 'owner' };
        TestBed.overrideProvider(AdminService, {
            useValue: {
                getUsers: () => of([owner]),
                getRoles: () => of([{ id: 6, code: 'tenant_owner', name: 'Владелец тенанта' }]),
            },
        });

        const fixture = TestBed.createComponent(AdminWorkspaceComponent);
        fixture.detectChanges();
        await fixture.whenStable();
        fixture.detectChanges();

        expect(fixture.nativeElement.querySelector('.owner-badge')?.textContent).toContain('Владелец');
        expect(fixture.nativeElement.querySelector('button[aria-label="Редактировать пользователя Тестовый Пользователь"]')?.disabled).toBe(true);
    });

    it('shows the admin creation policy prominently to the tenant owner and saves it', () => {
        TestBed.overrideProvider(ActivatedRoute, {
            useValue: { snapshot: { data: { section: 'settings' } } },
        });
        TestBed.overrideProvider(AuthService, {
            useValue: {
                isTenantAdmin: () => false,
                isTenantOwner: () => true,
                isAdmin: () => true,
                getLogin: () => 'owner',
            },
        });

        const fixture = TestBed.createComponent(AdminWorkspaceComponent);
        fixture.detectChanges();

        expect(tenantSettingsApi.getTenantAdminCreationPolicy).toHaveBeenCalledOnce();
        expect(fixture.componentInstance.allowAdminsCreateAdmins).toBe(true);
        const section = fixture.nativeElement.querySelector('.tenant-admin-policy') as HTMLElement;
        expect(section.textContent).toContain('Права администраторов');
        expect(section.textContent).toContain('Разрешить администраторам создавать администраторов');

        const toggle = section.querySelector('input[type="checkbox"]') as HTMLInputElement;
        expect(toggle.disabled).toBe(false);
        expect(toggle.checked).toBe(true);
        toggle.checked = false;
        toggle.dispatchEvent(new Event('change'));
        fixture.detectChanges();

        (section.querySelector('button') as HTMLButtonElement).click();

        expect(tenantSettingsApi.saveTenantAdminCreationPolicy).toHaveBeenCalledOnce();
        expect(tenantSettingsApi.saveTenantAdminCreationPolicy).toHaveBeenCalledWith(false);
    });

    it('keeps only one assignable role when editing a legacy multi-role user', () => {
        const multiRoleUser: UserSummary = { ...user, roleIds: [3, 2] };
        TestBed.overrideProvider(AdminService, {
            useValue: {
                getUsers: () => of([multiRoleUser]),
                getRoles: () => of([
                    { id: 2, code: 'admin', name: 'Администратор' },
                    { id: 3, code: 'agent', name: 'Инженер ТП' },
                ]),
            },
        });

        const fixture = TestBed.createComponent(AdminWorkspaceComponent);
        fixture.detectChanges();

        fixture.componentInstance.editUser(multiRoleUser);

        expect(fixture.componentInstance.editingUser?.roleIds).toEqual([3]);
    });

    it('keeps field labels visible when editing a user on narrow screens', async () => {
        const fixture = TestBed.createComponent(AdminWorkspaceComponent);
        fixture.detectChanges();
        await fixture.whenStable();
        fixture.detectChanges();

        const editButton = fixture.nativeElement.querySelector(
            'button[aria-label="Редактировать пользователя Тестовый Пользователь"]',
        ) as HTMLButtonElement;
        editButton.click();
        fixture.detectChanges();

        const editCells = fixture.nativeElement.querySelectorAll('.edit-row td') as NodeListOf<Element>;
        expect(Array.from(editCells, cell => cell.getAttribute('data-label'))).toEqual([
            'ФИО', 'Email', 'Username', 'Департамент', 'Руководитель', 'Статус', 'Роли', 'Действия',
        ]);
        expect(fixture.nativeElement.querySelector('.edit-row input[name="fullName"]')).not.toBeNull();
        expect(fixture.nativeElement.querySelector('.edit-row select[name="managerId"]')).not.toBeNull();
    });

    it('shows only the user name until a mobile card is expanded', async () => {
        const fixture = TestBed.createComponent(AdminWorkspaceComponent);
        fixture.detectChanges();
        await fixture.whenStable();
        fixture.detectChanges();

        const row = fixture.nativeElement.querySelector('.users-table tbody tr') as HTMLTableRowElement;
        const toggle = row.querySelector('.mobile-user-toggle') as HTMLButtonElement;
        expect(row.classList.contains('mobile-expanded')).toBe(false);
        expect(toggle.getAttribute('aria-expanded')).toBe('false');

        toggle.click();
        fixture.detectChanges();

        expect(row.classList.contains('mobile-expanded')).toBe(true);
        expect(toggle.getAttribute('aria-expanded')).toBe('true');
        expect(row.querySelector('[data-label="Email"]')?.textContent).toContain(user.email);
        expect(row.querySelector('[data-label="Действия"] button')).not.toBeNull();
    });

    it('filters users by search, department, role, and active status', async () => {
        const users: UserSummary[] = [
            user,
            {
                ...user,
                id: 8,
                username: 'robert',
                email: 'robert@example.com',
                fullName: 'Роберт Иванов',
                department: 'IT',
                roleIds: [5],
                isActive: false,
            },
            {
                ...user,
                id: 9,
                username: 'sales.user',
                email: 'sales@example.com',
                fullName: 'Анна Петрова',
                department: 'Продажи',
            },
        ];
        TestBed.overrideProvider(AdminService, {
            useValue: {
                getUsers: () => of(users),
                getRoles: () => of([
                    { id: 3, code: 'agent', name: 'Инженер' },
                    { id: 5, code: 'portal_user', name: 'Пользователь портала' },
                ]),
            },
        });

        const fixture = TestBed.createComponent(AdminWorkspaceComponent);
        fixture.detectChanges();
        await fixture.whenStable();
        fixture.detectChanges();

        (fixture.nativeElement.querySelector('.mobile-filters-toggle') as HTMLButtonElement).click();
        fixture.detectChanges();

        const search = fixture.nativeElement.querySelector('input[name="usersSearch"]') as HTMLInputElement;
        search.value = 'ROBERT@EXAMPLE.COM';
        search.dispatchEvent(new Event('input'));
        fixture.detectChanges();

        let pageText = (fixture.nativeElement as HTMLElement).textContent ?? '';
        expect(pageText).toContain('Роберт Иванов');
        expect(pageText).not.toContain('Тестовый Пользователь');
        expect(pageText).not.toContain('Анна Петрова');

        search.value = '';
        search.dispatchEvent(new Event('input'));
        const department = fixture.nativeElement.querySelector('select[name="usersDepartment"]') as HTMLSelectElement;
        department.value = 'IT';
        department.dispatchEvent(new Event('change'));
        const role = fixture.nativeElement.querySelector('select[name="usersRole"]') as HTMLSelectElement;
        role.value = '5';
        role.dispatchEvent(new Event('change'));
        const status = fixture.nativeElement.querySelector('select[name="usersStatus"]') as HTMLSelectElement;
        status.value = 'inactive';
        status.dispatchEvent(new Event('change'));
        fixture.detectChanges();

        pageText = (fixture.nativeElement as HTMLElement).textContent ?? '';
        expect(pageText).toContain('Роберт Иванов');
        expect(pageText).not.toContain('Тестовый Пользователь');
        expect(pageText).not.toContain('Анна Петрова');

        (fixture.nativeElement.querySelector('.clear-user-filters') as HTMLButtonElement).click();
        fixture.detectChanges();
        pageText = (fixture.nativeElement as HTMLElement).textContent ?? '';
        expect(pageText).toContain('Тестовый Пользователь');
        expect(pageText).toContain('Роберт Иванов');
        expect(pageText).toContain('Анна Петрова');
    });

    it('toggles the mobile filters panel with the Filters button', () => {
        const fixture = TestBed.createComponent(AdminWorkspaceComponent);
        fixture.detectChanges();

        const toggle = fixture.nativeElement.querySelector('.mobile-filters-toggle') as HTMLButtonElement;
        const filters = fixture.nativeElement.querySelector('#users-filters') as HTMLElement;

        expect(toggle.textContent).toContain('Filters');
        expect(toggle.getAttribute('aria-expanded')).toBe('false');
        expect(filters.classList.contains('filters-expanded')).toBe(false);

        toggle.click();
        fixture.detectChanges();

        expect(toggle.getAttribute('aria-expanded')).toBe('true');
        expect(filters.classList.contains('filters-expanded')).toBe(true);
    });

    it('replaces the loading message when the user request completes', () => {
        const users = new Subject<UserSummary[]>();
        TestBed.overrideProvider(AdminService, {
            useValue: {
                getUsers: () => users,
                getRoles: () => of([{ id: 3, code: 'agent', name: 'Инженер' }]),
            },
        });

        const fixture = TestBed.createComponent(AdminWorkspaceComponent);
        fixture.detectChanges();

        expect((fixture.nativeElement as HTMLElement).textContent).toContain('Загрузка пользователей');

        users.next([user]);
        users.complete();
        fixture.detectChanges();

        const pageText = (fixture.nativeElement as HTMLElement).textContent ?? '';
        expect(pageText).toContain(user.fullName);
        expect(pageText).not.toContain('Загрузка пользователей');
    });

    it('clears the loading message when the user request fails', () => {
        const users = new Subject<UserSummary[]>();
        TestBed.overrideProvider(AdminService, {
            useValue: {
                getUsers: () => users,
                getRoles: () => of([{ id: 3, code: 'agent', name: 'Инженер' }]),
            },
        });

        const fixture = TestBed.createComponent(AdminWorkspaceComponent);
        fixture.detectChanges();
        users.error(new Error('Request failed'));
        fixture.detectChanges();

        const pageText = (fixture.nativeElement as HTMLElement).textContent ?? '';
        expect(pageText).toContain('Request failed');
        expect(pageText).not.toContain('Загрузка пользователей');
    });

    it('clears the loading message if the user request completes without a result', () => {
        const users = new Subject<UserSummary[]>();
        TestBed.overrideProvider(AdminService, {
            useValue: {
                getUsers: () => users,
                getRoles: () => of([{ id: 3, code: 'agent', name: 'Инженер' }]),
            },
        });

        const fixture = TestBed.createComponent(AdminWorkspaceComponent);
        fixture.detectChanges();
        users.complete();
        fixture.detectChanges();

        const pageText = (fixture.nativeElement as HTMLElement).textContent ?? '';
        expect(pageText).toContain('Пользователей не найдено');
        expect(pageText).not.toContain('Загрузка пользователей');
    });
});
