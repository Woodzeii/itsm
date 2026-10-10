import { ApplicationConfig, provideBrowserGlobalErrorListeners, provideZoneChangeDetection } from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideRouter } from '@angular/router';
import { jwtInterceptor } from './core/auth/jwt.interceptor';
import { routes } from './app.routes';
import { WorkspaceApi } from './core/workspace/workspace-api';
import { MockWorkspaceApiService } from './core/workspace/mock-workspace-api.service';

export const appConfig: ApplicationConfig = {
    providers: [
        provideBrowserGlobalErrorListeners(),
        provideZoneChangeDetection({ eventCoalescing: true }),
        provideRouter(routes),
        provideHttpClient(withInterceptors([jwtInterceptor])),
        { provide: WorkspaceApi, useClass: MockWorkspaceApiService },
        provideAnimationsAsync(),
    ],
};