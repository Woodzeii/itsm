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
        getReports: ReturnType<typeof vi.fn>;
        getReportDetails: ReturnType<typeof vi.fn>;
        downloadReportCsv: ReturnType<typeof vi.fn>;
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
            getReports: vi.fn().mockReturnValue(of([{
                id: 'tickets-status',
                title: 'Заявки по статусам',
                description: 'Количество заявок в статусах за период',
                value: 4,
                count: 4,
            }])),
            getReportDetails: vi.fn().mockReturnValue(of({
                columns: [],
                items: [],
                total: 0,
                skip: 0,
                take: 30,
            })),
            downloadReportCsv: vi.fn().mockReturnValue(of(new Blob(['csv']))),
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
                        getAssignmentSetting: () => of({
                            mode: 'auto',
                            manualAssignerUserId: null,
                            manualAssignerName: null,
                            activeAgentsCount: 2,
                        }),
                        saveAssignmentSetting: (mode: 'auto' | 'manual') => of({
                            mode,
                            manualAssignerUserId: null,
                            manualAssignerName: null,
                            activeAgentsCount: 2,
                        }),
                        saveAssignmentSettingWithAssigner: (mode: 'auto' | 'manual') => of({
                            mode,
                            manualAssignerUserId: null,
                            manualAssignerName: null,
                            activeAgentsCount: 2,
                        }),
                        getFormTicketTypes: () => of([{ id: '12', name: 'Инцидент' }]),
                        getFormTemplate: () => of({
                            ticketTypeId: '12',
                            ticketTypeName: 'Инцидент',
                            fields: [
                                { id: 'subject', name: 'Тема', type: 'string', required: true, visibleToRequester: true, immutable: true },
                                { id: 'description', name: 'Описание', type: 'textarea', required: true, visibleToRequester: true, immutable: true },
                                { id: 'criticality', name: 'Критичность', type: 'dictionary', required: true, visibleToRequester: true, immutable: true },
                            ],
                        }),
                        saveForm: (form: unknown) => of(form),
                        getUsers: () => of([]),
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
        ).toEqual(['ФИО', 'Email', 'Username', 'Департамент', 'Руководитель', 'Статус', 'Роли', '2FA', 'Действия']);
    });

    it('does not allow built-in form fields to be deleted', async () => {
        TestBed.overrideProvider(ActivatedRoute, {
            useValue: { snapshot: { data: { section: 'forms' } } },
        });

        const fixture = TestBed.createComponent(AdminWorkspaceComponent);
        fixture.detectChanges();
        await fixture.whenStable();
        fixture.detectChanges();

        expect(fixture.nativeElement.querySelectorAll('.field-row').length).toBe(3);
        expect(fixture.nativeElement.querySelector('.field-row .icon-action')).toBeNull();

        const form = fixture.componentInstance.activeForm!;
        fixture.componentInstance.removeField(form, form.fields[0]!);
        expect(form.fields).toHaveLength(3);
        expect(fixture.componentInstance.error).toContain('нельзя удалить');
    });

    it('adds, removes, reorders, and saves custom form fields locally', async () => {
        TestBed.overrideProvider(ActivatedRoute, {
            useValue: { snapshot: { data: { section: 'forms' } } },
        });

        const fixture = TestBed.createComponent(AdminWorkspaceComponent);
        fixture.detectChanges();
        await fixture.whenStable();
        fixture.detectChanges();

        const component = fixture.componentInstance;
        const form = component.activeForm!;
        for (let index = 1; index <= 5; index++) {
            component.newFieldName = `Новое поле ${index}`;
            component.addField(form);
        }
        expect(form.fields).toHaveLength(8);
        fixture.detectChanges();
        expect(component.activeForm).toBe(form);
        expect(component.activeForm?.fields).toHaveLength(8);
        expect(form.fields.filter(field => !field.immutable)).toHaveLength(5);
        expect(fixture.nativeElement.querySelectorAll('.field-row').length).toBe(8);
        expect(fixture.nativeElement.textContent).toContain('＋ Добавить поле');
        expect(fixture.nativeElement.querySelectorAll('.field-delete').length).toBe(5);

        component.setConditionField(form.fields[4]!, form.fields[3]!.id);
        form.fields[4]!.conditionValue = 'Инцидент';
        component.removeField(form, form.fields[3]!);
        expect(form.fields[3]!.conditionFieldId).toBeUndefined();

        component.removeField(form, form.fields[3]!);
        expect(form.fields).toHaveLength(6);

        const beforeMove = form.fields[3]!.id;
        component.moveField(form, { previousIndex: 3, currentIndex: 4 });
        expect(form.fields[4]!.id).toBe(beforeMove);

        component.saveForm(form);
        expect(component.notice).toBe('Схема формы сохранена.');
        component.formId = '12';
        component.loadForm('12');
        expect(component.activeForm?.fields.map(field => field.id)).toEqual(form.fields.map(field => field.id));
    });

    it('supports local ticket type add, edit, archive, restore, and delete', async () => {
        TestBed.overrideProvider(ActivatedRoute, {
            useValue: { snapshot: { data: { section: 'settings' } } },
        });

        const fixture = TestBed.createComponent(AdminWorkspaceComponent);
        fixture.detectChanges();
        await fixture.whenStable();

        const component = fixture.componentInstance;
        component.openTicketTypeEditor();
        component.ticketTypeDraft!.name = 'Новый тип';
        component.saveTicketTypeDraft();
        const addedType = component.ticketTypes.find(type => type.name === 'Новый тип')!;

        component.openTicketTypeEditor(addedType);
        component.ticketTypeDraft!.name = 'Обновлённый тип';
        component.saveTicketTypeDraft();
        const editedType = component.ticketTypes.find(type => type.id === addedType.id)!;
        expect(editedType.name).toBe('Обновлённый тип');

        component.archiveType(editedType);
        expect(component.ticketTypes.some(type => type.id === editedType.id)).toBe(false);
        component.restoreType(editedType);
        expect(component.ticketTypes.some(type => type.id === editedType.id)).toBe(true);

        const confirm = vi.spyOn(window, 'confirm').mockReturnValue(true);
        component.archiveType(editedType);
        component.deleteArchivedType(editedType);
        expect(component.archivedTicketTypes.some(type => type.id === editedType.id)).toBe(false);
        confirm.mockRestore();
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
            'ФИО', 'Email', 'Username', 'Департамент', 'Руководитель', 'Статус', 'Роли', '2FA', 'Действия',
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

    it('loads reports and details through the HTTP API rather than WorkspaceService', () => {
        const fixture = TestBed.createComponent(AdminWorkspaceComponent);
        const component = fixture.componentInstance;
        component.reportFrom = '2026-01-01';
        component.reportTo = '2026-01-31';

        component.loadReports();
        component.openReport({
            id: 'tickets-status',
            title: 'Заявки по статусам',
            description: 'Количество заявок в статусах за период',
            value: 4,
            count: 4,
        });

        expect(tenantSettingsApi.getReports).toHaveBeenCalledWith('2026-01-01', '2026-01-31');
        expect(tenantSettingsApi.getReportDetails).toHaveBeenCalledWith(
            'tickets-status',
            '2026-01-01',
            '2026-01-31',
            0,
            30,
        );
        expect(component.reports).toHaveLength(1);
        expect(component.reportDetailOpen).toBe(true);
    });

    it('shows repair outcome totals in the pie chart alongside report details', () => {
        TestBed.inject(ActivatedRoute).snapshot.data['section'] = 'reports';
        const fixture = TestBed.createComponent(AdminWorkspaceComponent);
        fixture.detectChanges();
        const component = fixture.componentInstance;
        (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('.report-open')!.click();
        component.activeReport = {
            id: 'repairs',
            title: 'Ремонты',
            description: 'Закрытые заявки на ремонт за период',
            value: 3,
            count: 3,
        };
        component.reportDetail = {
            columns: [],
            items: [],
            total: 3,
            skip: 0,
            take: 30,
            aggregates: { 'В эксплуатацию': 2, 'На склад': 1 },
        };
        fixture.detectChanges();

        expect(component.section).toBe('reports');
        expect(component.reportDetailOpen).toBe(true);
        expect(component.repairOutcomeSlices.map(slice => [slice.label, slice.value])).toEqual([
            ['В эксплуатацию', 2],
            ['На склад', 1],
        ]);
        expect(component.repairOutcomeGradient).toContain('conic-gradient');
        expect((fixture.nativeElement as HTMLElement).textContent).toContain('Детализация отчета');
        expect(fixture.nativeElement.querySelector('.repair-pie')).not.toBeNull();
        expect((fixture.nativeElement as HTMLElement).textContent).toContain('В эксплуатацию');
    });
});
