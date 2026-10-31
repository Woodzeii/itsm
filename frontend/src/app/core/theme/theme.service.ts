import { Injectable, computed, signal } from '@angular/core';

export type AppTheme = 'light' | 'dark';

const THEME_STORAGE_KEY = 'itsm-theme';

@Injectable({ providedIn: 'root' })
export class ThemeService {
    private readonly current = signal<AppTheme>(this.readInitialTheme());

    readonly theme = this.current.asReadonly();
    readonly isDark = computed(() => this.current() === 'dark');

    constructor() {
        this.apply(this.current());
    }

    toggle(): void {
        this.set(this.isDark() ? 'light' : 'dark');
    }

    set(theme: AppTheme): void {
        this.current.set(theme);
        try {
            localStorage.setItem(THEME_STORAGE_KEY, theme);
        } catch {
            // Приватный режим/ограничения браузера — тема всё равно применится к документу.
        }
        this.apply(theme);
    }

    private apply(theme: AppTheme): void {
        if (typeof document === 'undefined') return;
        document.documentElement.setAttribute('data-theme', theme);
        document.documentElement.style.colorScheme = theme;
    }

    private readInitialTheme(): AppTheme {
        if (typeof document !== 'undefined') {
            const stored = this.readStoredTheme();
            if (stored) return stored;

            const initial = document.documentElement.getAttribute('data-theme');
            if (initial === 'dark' || initial === 'light') return initial;
        }

        if (
            typeof window !== 'undefined'
            && typeof window.matchMedia === 'function'
            && window.matchMedia('(prefers-color-scheme: dark)').matches
        ) {
            return 'dark';
        }

        return 'light';
    }

    private readStoredTheme(): AppTheme | null {
        try {
            const stored = localStorage.getItem(THEME_STORAGE_KEY);
            return stored === 'dark' || stored === 'light' ? stored : null;
        } catch {
            return null;
        }
    }
}
