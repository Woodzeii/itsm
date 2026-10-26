import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '../../../environments/environment';
import { CreateAdminUserRequest, RoleSummary, UpdateAdminUserRequest, UserSummary } from './admin.models';
import { HttpAdminApiService } from './http-admin-api.service';

describe('HttpAdminApiService user methods', () => {
    let api: HttpAdminApiService;
    let http: HttpTestingController;

    const user: UserSummary = {
        id: 15,
        username: 'new.user',
        email: 'new.user@example.com',
        fullName: 'New User',
        department: 'IT',
        managerId: 2,
        tenantId: 1,
        roleIds: [3],
        isActive: true,
    };

    beforeEach(() => {
        TestBed.configureTestingModule({
            providers: [
                provideHttpClient(),
                provideHttpClientTesting(),
                HttpAdminApiService,
            ],
        });
        api = TestBed.inject(HttpAdminApiService);
        http = TestBed.inject(HttpTestingController);
    });

    afterEach(() => http.verify());

    it('loads users from the paged backend response', () => {
        let result: UserSummary[] | undefined;
        api.getUsers().subscribe(users => result = users);

        const request = http.expectOne(`${environment.apiBaseUrl}/users?page=1&pageSize=200`);
        expect(request.request.method).toBe('GET');
        request.flush({ items: [user], total: 1, page: 1, pageSize: 200 });

        expect(result).toEqual([user]);
    });

    it('loads users wrapped in a data envelope', () => {
        let result: UserSummary[] | undefined;
        api.getUsers().subscribe(users => result = users);

        http.expectOne(request => request.url.endsWith('/users')).flush({
            data: { items: [user], total: 1, page: 1, pageSize: 200 },
        });

        expect(result).toEqual([user]);
    });

    it('maps the full backend user DTO without dropping users', () => {
        const responseUser = {
            ...user,
            isManager: false,
            isTwoFactorEnabled: true,
            status: 'Verified',
            failedLoginAttempts: 0,
            lockedUntil: null,
            createdAt: '2026-10-23T21:28:33.051702+00:00',
            roleCodes: ['agent'],
        };
        let result: UserSummary[] | undefined;
        api.getUsers().subscribe(users => result = users);

        http.expectOne(request => request.url.endsWith('/users')).flush({
            items: [responseUser, { ...responseUser, id: 16, managerId: null, tenantId: null }],
            total: 2,
            page: 1,
            pageSize: 200,
        });

        expect(result).toEqual([
            user,
            { ...user, id: 16, managerId: null, tenantId: null },
        ]);
    });

    it('normalizes PascalCase user data and rejects malformed entries', () => {
        let result: UserSummary[] | undefined;
        api.getUsers().subscribe(users => result = users);

        http.expectOne(request => request.url.endsWith('/users')).flush({
            Items: [{
                Id: user.id,
                Username: user.username,
                Email: user.email,
                FullName: user.fullName,
                Department: user.department,
                ManagerId: user.managerId,
                TenantId: user.tenantId,
                RoleIds: user.roleIds,
                IsActive: user.isActive,
            }],
        });

        expect(result).toEqual([user]);

        let error: unknown;
        api.getUsers().subscribe({ error: err => error = err });
        http.expectOne(request => request.url.endsWith('/users')).flush({ items: [{}] });
        expect(error).toEqual(new Error('Сервер вернул пользователя в неожиданном формате.'));
    });

    it('loads the available system roles', () => {
        const roles: RoleSummary[] = [{ id: 3, code: 'agent', name: 'Инженер' }];
        let result: RoleSummary[] | undefined;
        api.getRoles().subscribe(value => result = value);

        const request = http.expectOne(`${environment.apiBaseUrl}/users/roles`);
        expect(request.request.method).toBe('GET');
        request.flush(roles);

        expect(result).toEqual(roles);
    });

    it('creates a user using the backend password field', () => {
        const input: CreateAdminUserRequest = {
            username: user.username,
            email: user.email,
            fullName: user.fullName,
            department: user.department ?? '',
            managerId: user.managerId,
            initialPassword: 'StrongPassword1!',
            isActive: true,
            roleIds: user.roleIds,
        };
        let result: UserSummary | undefined;
        api.createUser(input).subscribe(value => result = value);

        const request = http.expectOne(`${environment.apiBaseUrl}/users`);
        expect(request.request.method).toBe('POST');
        expect(request.request.body).toEqual({
            username: input.username,
            email: input.email,
            password: input.initialPassword,
            fullName: input.fullName,
            department: input.department,
            managerId: input.managerId,
            isActive: input.isActive,
            roleIds: input.roleIds,
        });
        request.flush(user);

        expect(result).toEqual(user);
    });

    it('updates a user through the backend PUT endpoint', () => {
        const changes: UpdateAdminUserRequest = {
            username: user.username,
            email: user.email,
            fullName: user.fullName,
            department: user.department,
            managerId: user.managerId,
            tenantId: user.tenantId,
            isActive: user.isActive,
            roleIds: user.roleIds,
        };
        let result: UserSummary | undefined;
        api.updateUser(user.id, changes).subscribe(value => result = value);

        const request = http.expectOne(`${environment.apiBaseUrl}/users/${user.id}`);
        expect(request.request.method).toBe('PUT');
        expect(request.request.body).toEqual(changes);
        request.flush(user);

        expect(result).toEqual(user);
    });
});
