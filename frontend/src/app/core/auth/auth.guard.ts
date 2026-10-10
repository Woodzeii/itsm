import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

/// Защита маршрутов: пускает только авторизованных.
/// Если токена нет — редиректит на /login с запоминанием целевого URL.
export const authGuard: CanActivateFn = (route, state) => {
    const auth = inject(AuthService);
    const router = inject(Router);

    if (auth.isAuthenticated()) {
        return true;
    }

    return router.createUrlTree(['/login'], {
        queryParams: { returnUrl: state.url },
    });
};

/// Защита админских маршрутов: требует роль admin.
export const adminGuard: CanActivateFn = () => {
    const auth = inject(AuthService);
    const router = inject(Router);

    if (!auth.isAuthenticated()) {
        return router.createUrlTree(['/login']);
    }

    if (auth.isAdmin()) {
        return true;
    }

    // Не админ — отправим на дашборд
    return router.createUrlTree(['/dashboard']);
};