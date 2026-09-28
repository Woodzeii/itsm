import { inject } from '@angular/core';
import { CanActivateFn, Router, Routes } from '@angular/router';
import { AuthService } from './core/auth/auth.service';
import { LoginComponent } from './pages/login/login';
import { DashboardComponent } from './pages/dashboard/dashboard';
import { AdminComponent } from './pages/admin/admin';

const authGuard: CanActivateFn = () => {
    const auth = inject(AuthService);
    const router = inject(Router);

    return auth.isAuthenticated() ? true : router.createUrlTree(['/login']);
};

export const routes: Routes = [
    { path: '', redirectTo: '/login', pathMatch: 'full' },
    { path: 'login', component: LoginComponent },
    { path: 'dashboard', component: DashboardComponent, canActivate: [authGuard] },
    { path: 'admin', component: AdminComponent, canActivate: [authGuard] },
];
