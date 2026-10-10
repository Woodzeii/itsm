import { Routes } from '@angular/router';
import { AppLayoutComponent } from './layout/app-layout/app-layout';
import { adminAccessGuard, internalAccessGuard, portalAccessGuard, reportAccessGuard, tenantAdminAccessGuard } from './core/auth/role.guard';

export const routes: Routes = [
    { path: '', redirectTo: '/dashboard', pathMatch: 'full' },
    { path: 'login', loadComponent: () => import('./pages/login/login').then(module => module.LoginComponent) },
    {
        path: '',
        component: AppLayoutComponent,
        children: [
            { path: 'dashboard', loadComponent: () => import('./pages/overview/overview').then(module => module.OverviewComponent), canActivate: [internalAccessGuard] },
            { path: 'tickets', loadComponent: () => import('./pages/tickets/ticket-queue').then(module => module.TicketQueueComponent), canActivate: [internalAccessGuard] },
            { path: 'assets', loadComponent: () => import('./pages/assets/assets-page').then(module => module.AssetsPageComponent), canActivate: [internalAccessGuard] },
            { path: 'service-catalog', loadComponent: () => import('./pages/workspace-section/workspace-section').then(module => module.WorkspaceSectionComponent), data: { section: 'catalog' }, canActivate: [internalAccessGuard] },
            { path: 'escalations', loadComponent: () => import('./pages/workspace-section/workspace-section').then(module => module.WorkspaceSectionComponent), data: { section: 'escalations' }, canActivate: [internalAccessGuard] },
            { path: 'portal', loadComponent: () => import('./pages/portal/self-service-portal').then(module => module.SelfServicePortalComponent), canActivate: [portalAccessGuard] },
            { path: 'admin/tenants', loadComponent: () => import('./pages/tenant-admin/tenant-management').then(module => module.TenantManagementComponent), canActivate: [tenantAdminAccessGuard] },
            { path: 'admin', redirectTo: '/admin/settings', pathMatch: 'full' },
            { path: 'admin/settings', loadComponent: () => import('./pages/admin/admin-workspace').then(module => module.AdminWorkspaceComponent), data: { section: 'settings' }, canActivate: [adminAccessGuard] },
            { path: 'admin/forms', loadComponent: () => import('./pages/admin/admin-workspace').then(module => module.AdminWorkspaceComponent), data: { section: 'forms' }, canActivate: [adminAccessGuard] },
            { path: 'admin/escalation-policies', loadComponent: () => import('./pages/admin/admin-workspace').then(module => module.AdminWorkspaceComponent), data: { section: 'escalations' }, canActivate: [adminAccessGuard] },
            { path: 'admin/reports', loadComponent: () => import('./pages/admin/admin-workspace').then(module => module.AdminWorkspaceComponent), data: { section: 'reports' }, canActivate: [reportAccessGuard] },
            { path: 'dictionaries', loadComponent: () => import('./pages/dictionaries/dictionaries').then(module => module.DictionariesComponent), canActivate: [adminAccessGuard] },
            { path: 'asset-classes', loadComponent: () => import('./pages/asset-classes/asset-classes').then(module => module.AssetClassesComponent), canActivate: [adminAccessGuard] },
        ],
    },
];