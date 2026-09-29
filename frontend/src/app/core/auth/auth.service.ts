import { Injectable } from '@angular/core';

export type UserRole = 'user' | 'admin';

@Injectable({ providedIn: 'root' })
export class AuthService {
    private readonly authKey = 'itsm_is_authenticated';
    private readonly roleKey = 'itsm_user_role';
    private readonly tokenKey = 'itsm_access_token';

    isAuthenticated(): boolean {
        return localStorage.getItem(this.authKey) === 'true';
    }

    getRole(): UserRole {
        return (localStorage.getItem(this.roleKey) as UserRole) || 'user';
    }

    isAdmin(): boolean {
        return this.getRole() === 'admin';
    }

    getToken(): string | null {
        return localStorage.getItem(this.tokenKey);
    }

    login(role: UserRole = 'user'): void {
        localStorage.setItem(this.authKey, 'true');
        localStorage.setItem(this.roleKey, role);
    }

    logout(): void {
        localStorage.removeItem(this.authKey);
        localStorage.removeItem(this.roleKey);
        localStorage.removeItem(this.tokenKey);
    }
}
