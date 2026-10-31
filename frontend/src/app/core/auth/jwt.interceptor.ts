import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from './auth.service';

/** Запросы самого входа: их 401 означает неверные данные, а не истёкшую сессию. */
const AUTH_REQUESTS_WITHOUT_REDIRECT = [
    '/auth/login',
    '/auth/verify-2fa',
    '/auth/skip-2fa-setup',
];

export const jwtInterceptor: HttpInterceptorFn = (request, next) => {
    const auth = inject(AuthService);
    const router = inject(Router);
    const token = auth.getToken();

    const authenticatedRequest = token
        ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
        : request;

    return next(authenticatedRequest).pipe(
        catchError((error: unknown) => {
            if (
                error instanceof HttpErrorResponse
                && error.status === 401
                && token
                && !isAuthRequest(request.url)
            ) {
                // Токен больше не принимается сервером — завершаем сессию и уводим на вход.
                auth.logout();

                const returnUrl = router.url;
                void router.navigate(['/login'], {
                    queryParams: returnUrl && returnUrl !== '/login'
                        ? { returnUrl }
                        : {},
                });
            }

            return throwError(() => error);
        }),
    );
};

function isAuthRequest(url: string): boolean {
    return AUTH_REQUESTS_WITHOUT_REDIRECT.some(path => url.includes(path));
}