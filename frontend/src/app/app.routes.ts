import { Routes } from '@angular/router';
import { AppLayoutComponent } from './layout/app-layout/app-layout';
import { authGuard } from './core/auth/auth.guard';
import { adminAccessGuard, adminOnlyAccessGuard, internalAccessGuard, portalAccessGuard, reportAccessGuard, roleSelectionRequiredGuard, selectRoleGuard, tenantAdminAccessGuard } from './core/auth/role.guard';
import { pendingChangesGuard } from './core/auth/pending-changes.guard';

export const routes: Routes = [
    { path: '', redirectTo: '/dashboard', pathMatch: 'full' },
    { path: 'login', loadComponent: () => import('./pages/login/login').then(module => module.LoginComponent) },
    {
        path: 'select-role',
        loadComponent: () => import('./pages/role-select/role-select').then(module => module.RoleSelectComponent),
        canActivate: [selectRoleGuard],
    },
    {
        path: '',
        component: AppLayoutComponent,
        canActivate: [authGuard, roleSelectionRequiredGuard],
        children: [
            { path: 'dashboard', loadComponent: () => import('./pages/overview/overview').then(module => module.OverviewComponent), canActivate: [internalAccessGuard] },
            { path: 'tickets', loadComponent: () => import('./pages/tickets/ticket-queue').then(module => module.TicketQueueComponent), canActivate: [internalAccessGuard] },
            { path: 'knowledge-base', loadComponent: () => import('./pages/knowledge-base/knowledge-base').then(module => module.KnowledgeBaseComponent), canActivate: [internalAccessGuard] },
            { path: 'assets', loadComponent: () => import('./pages/assets/assets-page').then(module => module.AssetsPageComponent), canActivate: [adminAccessGuard] },
            { path: 'assignments', loadComponent: () => import('./pages/assignment-history/assignment-history').then(module => module.AssignmentHistoryComponent), canActivate: [internalAccessGuard] },
            { path: 'escalations', loadComponent: () => import('./pages/workspace-section/workspace-section').then(module => module.WorkspaceSectionComponent), data: { section: 'escalations' }, canActivate: [internalAccessGuard] },
            { path: 'portal', loadComponent: () => import('./pages/portal/self-service-portal').then(module => module.SelfServicePortalComponent), canActivate: [portalAccessGuard] },
            { path: 'admin/tenants', loadComponent: () => import('./pages/tenant-admin/tenant-management').then(module => module.TenantManagementComponent), canActivate: [tenantAdminAccessGuard] },
            { path: 'admin', redirectTo: '/admin/settings', pathMatch: 'full' },
            { path: 'admin/users/new', loadComponent: () => import('./pages/admin/user-create').then(module => module.UserCreateComponent), canActivate: [adminOnlyAccessGuard] },
            { path: 'admin/users', loadComponent: () => import('./pages/admin/admin-workspace').then(module => module.AdminWorkspaceComponent), data: { section: 'users' }, canActivate: [adminOnlyAccessGuard], canDeactivate: [pendingChangesGuard] },
            { path: 'admin/settings', loadComponent: () => import('./pages/admin/admin-workspace').then(module => module.AdminWorkspaceComponent), data: { section: 'settings' }, canActivate: [adminAccessGuard], canDeactivate: [pendingChangesGuard] },
            { path: 'admin/forms', loadComponent: () => import('./pages/admin/admin-workspace').then(module => module.AdminWorkspaceComponent), data: { section: 'forms' }, canActivate: [adminAccessGuard], canDeactivate: [pendingChangesGuard] },
            { path: 'admin/escalation-policies', loadComponent: () => import('./pages/admin/admin-workspace').then(module => module.AdminWorkspaceComponent), data: { section: 'escalations' }, canActivate: [adminAccessGuard], canDeactivate: [pendingChangesGuard] },
            { path: 'admin/reports', loadComponent: () => import('./pages/admin/admin-workspace').then(module => module.AdminWorkspaceComponent), data: { section: 'reports' }, canActivate: [reportAccessGuard], canDeactivate: [pendingChangesGuard] },
            { path: 'dictionaries', loadComponent: () => import('./pages/dictionaries/dictionaries').then(module => module.DictionariesComponent), canActivate: [adminAccessGuard] },
            { path: 'asset-classes', loadComponent: () => import('./pages/asset-classes/asset-classes').then(module => module.AssetClassesComponent), canActivate: [adminAccessGuard] },
            { path: 'profile/security', loadComponent: () => import('./pages/profile/security').then(module => module.SecurityComponent) },
        ],
    },
    { path: '**', redirectTo: '/dashboard' },
];