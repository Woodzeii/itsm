import { ApplicationConfig, provideBrowserGlobalErrorListeners, provideZoneChangeDetection } from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideRouter } from '@angular/router';
import { jwtInterceptor } from './core/auth/jwt.interceptor';
import { routes } from './app.routes';
import { WorkspaceApi } from './core/workspace/workspace-api';
import { MockWorkspaceApiService } from './core/workspace/mock-workspace-api.service';
import { HttpWorkspaceApiService } from './core/workspace/http-workspace-api.service';
import { environment } from '../environments/environment';
import { AuthApi } from './core/auth/auth-api';
import { MockAuthApiService } from './core/auth/mock-auth-api.service';
import { HttpAuthApiService } from './core/auth/http-auth-api.service';

export const appConfig: ApplicationConfig = {
    providers: [
        provideBrowserGlobalErrorListeners(),
        provideZoneChangeDetection({ eventCoalescing: true }),
        provideRouter(routes),
        provideHttpClient(withInterceptors([jwtInterceptor])),
        { provide: AuthApi, useClass: environment.useMockAuth ? MockAuthApiService : HttpAuthApiService },
        { provide: WorkspaceApi, useClass: environment.useMockWorkspaceApi ? MockWorkspaceApiService : HttpWorkspaceApiService },
        provideAnimationsAsync(),
    ],
};