import { TestBed } from '@angular/core/testing';
import { ThemeToggleComponent } from './theme-toggle';

describe('ThemeToggleComponent', () => {
    beforeEach(async () => {
        localStorage.clear();
        document.documentElement.removeAttribute('data-theme');
        TestBed.resetTestingModule();
        await TestBed.configureTestingModule({
            imports: [ThemeToggleComponent],
        }).compileComponents();
    });

    afterEach(() => {
        localStorage.clear();
        document.documentElement.removeAttribute('data-theme');
    });

    it('renders a button with an accessible label on any page', () => {
        const fixture = TestBed.createComponent(ThemeToggleComponent);
        fixture.detectChanges();

        const button = fixture.nativeElement.querySelector('button.theme-toggle') as HTMLButtonElement;
        expect(button).toBeTruthy();
        expect(button.getAttribute('aria-label')).toBe('Включить тёмную тему');
    });

    it('switches the document to dark and back to light', () => {
        const fixture = TestBed.createComponent(ThemeToggleComponent);
        fixture.detectChanges();
        const button = fixture.nativeElement.querySelector('button.theme-toggle') as HTMLButtonElement;

        button.click();
        fixture.detectChanges();

        expect(document.documentElement.getAttribute('data-theme')).toBe('dark');
        expect(button.getAttribute('aria-label')).toBe('Включить светлую тему');

        button.click();
        fixture.detectChanges();

        expect(document.documentElement.getAttribute('data-theme')).toBe('light');
        expect(button.getAttribute('aria-label')).toBe('Включить тёмную тему');
    });
});
