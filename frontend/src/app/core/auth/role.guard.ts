import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';
import { WorkspaceService } from '../workspace/workspace.service';
import { map } from 'rxjs';

/// Не пускает в рабочие разделы, пока мультиролевой пользователь не выбрал активную роль.
export const roleSelectionRequiredGuard: CanActivateFn = () => {
    const auth = inject(AuthService);
    const router = inject(Router);
    if (!auth.isAuthenticated()) return router.parseUrl('/login');
    return auth.needsRoleSelection() ? router.parseUrl('/select-role') : true;
};

/// Защита промежуточного экрана: доступен только пока роль не выбрана.
export const selectRoleGuard: CanActivateFn = () => {
    const auth = inject(AuthService);
    const router = inject(Router);
    if (!auth.isAuthenticated()) return router.parseUrl('/login');
    if (auth.needsRoleSelection()) return true;
    return router.parseUrl(auth.homeUrlForRole(auth.getActiveRole() ?? auth.getRole()));
};

export const internalAccessGuard: CanActivateFn = () => {
    const auth = inject(AuthService);
    const router = inject(Router);
    if (!auth.isAuthenticated()) return router.parseUrl('/login');
    return auth.canRead() ? true : router.parseUrl('/portal');
};

export const portalAccessGuard: CanActivateFn = () => {
    const auth = inject(AuthService);
    const router = inject(Router);
    if (!auth.isAuthenticated()) return router.parseUrl('/login');
    return auth.isPortalUser() ? true : router.parseUrl(auth.isAdmin() || auth.canRead() ? '/dashboard' : '/login');
};

export const adminAccessGuard: CanActivateFn = () => {
    const auth = inject(AuthService);
    const router = inject(Router);
    if (!auth.isAuthenticated()) return router.parseUrl('/login');
    const role = auth.getRole();
    return role === 'admin' || role === 'agent'
        ? true
        : router.parseUrl(auth.canRead() ? '/dashboard' : '/portal');
};

export const adminOnlyAccessGuard: CanActivateFn = () => {
    const auth = inject(AuthService);
    const router = inject(Router);
    if (!auth.isAuthenticated()) return router.parseUrl('/login');
    return auth.isAdmin() ? true : router.parseUrl(auth.canRead() ? '/dashboard' : '/portal');
};

export const reportAccessGuard: CanActivateFn = () => {
    const auth = inject(AuthService);
    const router = inject(Router);
    if (!auth.isAuthenticated()) return router.parseUrl('/login');
    return inject(WorkspaceService).getAdminSettings().pipe(map(settings => settings.reportAccessRoles?.includes(auth.getRole()) ? true : router.parseUrl('/dashboard')));
};

export const tenantAdminAccessGuard: CanActivateFn = () => {
    const auth = inject(AuthService);
    const router = inject(Router);
    if (!auth.isAuthenticated()) return router.parseUrl('/login');
    if (!auth.isTenantAdmin()) return router.parseUrl(auth.canRead() ? '/dashboard' : '/portal');
    return inject(WorkspaceService).getAdminSettings().pipe(map(settings => settings.tenantMode === 'saas' ? true : router.parseUrl('/login')));
};