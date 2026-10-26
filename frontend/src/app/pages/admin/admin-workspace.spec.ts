import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { of, Subject } from 'rxjs';
import { AdminService } from '../../core/admin/admin.service';
import { UserSummary } from '../../core/admin/admin.models';
import { WorkspaceService } from '../../core/workspace/workspace.service';
import { AdminWorkspaceComponent } from './admin-workspace';

describe('AdminWorkspaceComponent user list', () => {
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
                    useValue: { getAdminSettings: () => of({}) },
                },
                {
                    provide: AdminService,
                    useValue: {
                        getUsers: () => of([user]),
                        getRoles: () => of([{ id: 3, code: 'agent', name: 'Инженер' }]),
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
