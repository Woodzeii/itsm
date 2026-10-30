import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AdminService } from '../../core/admin/admin.service';
import { CreateAdminUserRequest, UserSummary } from '../../core/admin/admin.models';
import { UserCreateComponent } from './user-create';

describe('UserCreateComponent', () => {
    let admin: {
        getUsers: ReturnType<typeof vi.fn>;
        getRoles: ReturnType<typeof vi.fn>;
        createUser: ReturnType<typeof vi.fn>;
    };
    let router: Router;

    const createdUser: UserSummary = {
        id: 21,
        username: 'new.user',
        email: 'new.user@example.com',
        fullName: 'New User',
        department: 'IT',
        managerId: null,
        tenantId: null,
        roleIds: [3],
        isActive: true,
    };

    beforeEach(async () => {
        admin = {
            getUsers: vi.fn().mockReturnValue(of([])),
            getRoles: vi.fn().mockReturnValue(of([{ id: 3, code: 'agent', name: 'Инженер' }])),
            createUser: vi.fn().mockReturnValue(of(createdUser)),
        };

        await TestBed.configureTestingModule({
            imports: [UserCreateComponent],
            providers: [
                provideRouter([]),
                { provide: AdminService, useValue: admin },
            ],
        }).compileComponents();
        router = TestBed.inject(Router);
    });

    it('creates the user and navigates to the list with a success notice', () => {
        const navigate = vi.spyOn(router, 'navigate').mockResolvedValue(true);
        const fixture = TestBed.createComponent(UserCreateComponent);
        fixture.detectChanges();
        const component = fixture.componentInstance;
        component.form = {
            username: createdUser.username,
            email: createdUser.email,
            fullName: createdUser.fullName,
            department: createdUser.department ?? '',
            managerId: null,
            initialPassword: 'StrongPassword1!',
            isActive: true,
            roleIds: [3],
        };

        component.createUser();

        expect(admin.createUser).toHaveBeenCalledOnce();
        expect(admin.createUser).toHaveBeenCalledWith(component.form);
        expect(navigate).toHaveBeenCalledWith(['/admin/users'], {
            state: { userCreated: createdUser.fullName },
        });
        expect(component.saving).toBe(false);
    });

    it('shows the backend error and stays on the form when creation fails', () => {
        admin.createUser.mockReturnValue(throwError(() => new Error('Логин уже существует')));
        const navigate = vi.spyOn(router, 'navigate');
        const fixture = TestBed.createComponent(UserCreateComponent);
        fixture.detectChanges();
        const component = fixture.componentInstance;
        component.form = {
            username: 'existing',
            email: 'existing@example.com',
            fullName: 'Existing User',
            department: 'IT',
            managerId: null,
            initialPassword: 'StrongPassword1!',
            isActive: true,
            roleIds: [3],
        };

        component.createUser();

        expect(component.error).toBe('Логин уже существует');
        expect(component.saving).toBe(false);
        expect(navigate).not.toHaveBeenCalled();
    });
});
