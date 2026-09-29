import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from './auth.service';

export const jwtInterceptor: HttpInterceptorFn = (request, next) => {
    const token = inject(AuthService).getToken();

    if (!token) {
        return next(request);
    }

    const authenticatedRequest = request.clone({
        setHeaders: {
            Authorization: `Bearer ${token}`,
        },
    });

    return next(authenticatedRequest);
};