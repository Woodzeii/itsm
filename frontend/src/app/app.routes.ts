import { Routes } from '@angular/router';
import { AppLayoutComponent } from './layout/app-layout/app-layout';
import { LoginComponent } from './pages/login/login';
import { DashboardComponent } from './pages/dashboard/dashboard';

// Re-enable the auth guard when protected routes should require login.
// const authGuard: CanActivateFn = () => {
//   const auth = inject(AuthService);
//   const router = inject(Router);
//
//   return auth.isAuthenticated() ? true : router.createUrlTree(['/login']);
// };

export const routes: Routes = [
    { path: '', redirectTo: '/dashboard', pathMatch: 'full' },
    { path: 'login', component: LoginComponent },
    {
        path: '',
        component: AppLayoutComponent,
        // canActivate: [authGuard],
        children: [
            { path: 'dashboard', component: DashboardComponent },
            { path: 'admin', component: DashboardComponent },
        ],
    },
];
