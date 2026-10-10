import { ApplicationConfig, provideBrowserGlobalErrorListeners, provideZoneChangeDetection } from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideRouter } from '@angular/router';
import { jwtInterceptor } from './core/auth/jwt.interceptor';
import { routes } from './app.routes';
import { environment } from '../environments/environment';
import { AuthApi } from './core/auth/auth-api';
import { MockAuthApiService } from './core/auth/mock-auth-api.service';
import { HttpAuthApiService } from './core/auth/http-auth-api.service';
import { workspaceApiProvider } from './core/workspace/workspace-api.provider';

export const appConfig: ApplicationConfig = {
    providers: [
        provideBrowserGlobalErrorListeners(),
        provideZoneChangeDetection({ eventCoalescing: true }),
        provideRouter(routes),
        provideHttpClient(withInterceptors([jwtInterceptor])),
        { provide: AuthApi, useClass: environment.useMockAuth ? MockAuthApiService : HttpAuthApiService },
        workspaceApiProvider,
        provideAnimationsAsync(),
    ],
};