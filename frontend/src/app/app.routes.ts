import { Routes } from '@angular/router';
import { AppLayoutComponent } from './layout/app-layout/app-layout';
import { LoginComponent } from './pages/login/login';
import { DashboardComponent } from './pages/dashboard/dashboard';
import { DictionariesComponent } from './pages/dictionaries/dictionaries';
import { AssetClassesComponent } from './pages/asset-classes/asset-classes';

export const routes: Routes = [
    { path: '', redirectTo: '/dashboard', pathMatch: 'full' },
    { path: 'login', component: LoginComponent },
    {
        path: '',
        component: AppLayoutComponent,
        children: [
            { path: 'dashboard', component: DashboardComponent },
            { path: 'admin', component: DashboardComponent },
            { path: 'dictionaries', component: DictionariesComponent },
            { path: 'asset-classes', component: AssetClassesComponent },
        ],
    },
];