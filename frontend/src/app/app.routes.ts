import { Routes } from '@angular/router';
import { AppLayoutComponent } from './layout/app-layout/app-layout';
import { LoginComponent } from './pages/login/login';
import { OverviewComponent } from './pages/overview/overview';
import { WorkspaceSectionComponent } from './pages/workspace-section/workspace-section';
import { DictionariesComponent } from './pages/dictionaries/dictionaries';
import { AssetClassesComponent } from './pages/asset-classes/asset-classes';

export const routes: Routes = [
    { path: '', redirectTo: '/dashboard', pathMatch: 'full' },
    { path: 'login', component: LoginComponent },
    {
        path: '',
        component: AppLayoutComponent,
        children: [
            { path: 'dashboard', component: OverviewComponent },
            { path: 'tickets', component: WorkspaceSectionComponent, data: { section: 'tickets' } },
            { path: 'assets', component: WorkspaceSectionComponent, data: { section: 'assets' } },
            { path: 'service-catalog', component: WorkspaceSectionComponent, data: { section: 'catalog' } },
            { path: 'escalations', component: WorkspaceSectionComponent, data: { section: 'escalations' } },
            { path: 'admin', redirectTo: '/dictionaries', pathMatch: 'full' },
            { path: 'dictionaries', component: DictionariesComponent },
            { path: 'asset-classes', component: AssetClassesComponent },
        ],
    },
];