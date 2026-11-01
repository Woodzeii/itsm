import { Component, inject } from '@angular/core';
import { ThemeService } from './theme.service';

@Component({
    selector: 'app-theme-toggle',
    standalone: true,
    template: `
    <button
      type="button"
      class="theme-toggle"
      (click)="theme.toggle()"
      [attr.aria-label]="theme.isDark() ? 'Включить светлую тему' : 'Включить тёмную тему'"
      [attr.title]="theme.isDark() ? 'Светлая тема' : 'Тёмная тема'">
      @if (theme.isDark()) {
        <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
          <circle cx="12" cy="12" r="4.2" stroke="currentColor" stroke-width="1.8"/>
          <path d="M12 2.5v2M12 19.5v2M2.5 12h2M19.5 12h2M5.2 5.2l1.4 1.4M17.4 17.4l1.4 1.4M18.8 5.2l-1.4 1.4M6.6 17.4l-1.4 1.4" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"/>
        </svg>
      } @else {
        <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
          <path d="M20 14.2A8.2 8.2 0 0 1 9.8 4a8.2 8.2 0 1 0 10.2 10.2Z" stroke="currentColor" stroke-width="1.8" stroke-linejoin="round"/>
        </svg>
      }
    </button>
  `,
    styles: [
        `
      :host {
        position: fixed;
        right: 20px;
        bottom: 20px;
        z-index: 1200;
      }
      .theme-toggle {
        width: 46px;
        height: 46px;
        display: grid;
        place-items: center;
        padding: 0;
        border: 1px solid var(--app-border);
        border-radius: 50%;
        background: var(--app-surface);
        color: var(--app-text);
        box-shadow: 0 6px 20px rgba(15, 23, 42, 0.18);
        cursor: pointer;
        transition: transform 0.15s ease, background 0.15s ease, border-color 0.15s ease;
      }
      .theme-toggle:hover { transform: translateY(-1px); border-color: var(--app-primary); color: var(--app-primary); }
      .theme-toggle:focus-visible { outline: 2px solid var(--app-primary); outline-offset: 2px; }
      .theme-toggle svg { width: 21px; height: 21px; }
      @media (max-width: 760px) {
        :host { right: 12px; bottom: 12px; }
        .theme-toggle { width: 42px; height: 42px; }
      }
    `,
    ],
})
export class ThemeToggleComponent {
    protected readonly theme = inject(ThemeService);
}
