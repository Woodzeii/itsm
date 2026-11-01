import { TestBed } from '@angular/core/testing';
import { ThemeService } from './theme.service';

describe('ThemeService', () => {
    beforeEach(() => {
        localStorage.clear();
        document.documentElement.removeAttribute('data-theme');
        document.documentElement.style.colorScheme = '';
        TestBed.resetTestingModule();
    });

    afterEach(() => {
        localStorage.clear();
        document.documentElement.removeAttribute('data-theme');
        document.documentElement.style.colorScheme = '';
    });

    it('applies the light theme by default', () => {
        const service = TestBed.inject(ThemeService);

        expect(service.theme()).toBe('light');
        expect(service.isDark()).toBe(false);
        expect(document.documentElement.getAttribute('data-theme')).toBe('light');
    });

    it('toggles the document theme and persists the choice', () => {
        const service = TestBed.inject(ThemeService);

        service.toggle();

        expect(service.isDark()).toBe(true);
        expect(document.documentElement.getAttribute('data-theme')).toBe('dark');
        expect(document.documentElement.style.colorScheme).toBe('dark');
        expect(localStorage.getItem('itsm-theme')).toBe('dark');

        service.toggle();

        expect(service.isDark()).toBe(false);
        expect(document.documentElement.getAttribute('data-theme')).toBe('light');
        expect(localStorage.getItem('itsm-theme')).toBe('light');
    });

    it('restores a stored dark theme on startup', () => {
        localStorage.setItem('itsm-theme', 'dark');

        const service = TestBed.inject(ThemeService);

        expect(service.theme()).toBe('dark');
        expect(document.documentElement.getAttribute('data-theme')).toBe('dark');
    });
});
