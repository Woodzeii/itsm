import { Component, OnInit } from '@angular/core';
import { Router, RouterLink, RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';

@Component({
    selector: 'app-layout',
    standalone: true,
    imports: [RouterLink, RouterOutlet],
    template: `
    <div class="app-shell">
      <aside class="sidebar">
        <div class="brand">
          <span class="brand-mark">IT</span>
          <span>ITSM</span>
        </div>

        <nav class="nav">
          <a routerLink="/dashboard" routerLinkActive="active">Главная</a>
          <a routerLink="/dictionaries" routerLinkActive="active">Справочники</a>
          @if (isAdminOrAgent) {
            <a routerLink="/asset-classes" routerLinkActive="active">Классы активов</a>
          }
          <a routerLink="/profile/security" routerLinkActive="active">Безопасность</a>
          @if (isAdmin) {
            <a routerLink="/admin" routerLinkActive="active">Админка</a>
          }
        </nav>

        <div class="sidebar-footer">
          <button class="logout-btn" (click)="logout()">Выйти</button>
        </div>
      </aside>

      <div class="main-panel">
        <header class="topbar">
          <div class="page-title">Панель управления</div>

          <div class="user-profile">
            <div class="avatar">{{ userInitial }}</div>
            <div class="user-meta">
              <strong>{{ userTitle }}</strong>
              <small>{{ userEmail }}</small>
            </div>
          </div>
        </header>

        <main class="content">
          <router-outlet />
        </main>
      </div>
    </div>
  `,
    styles: [
        `
      :host {
        display: block;
        height: 100vh;
        background: #f4f7fb;
        font-family: Arial, sans-serif;
        color: #1f2937;
      }
      .app-shell { display: flex; height: 100vh; }
      .sidebar {
        width: 260px;
        background: #111827;
        color: #f9fafb;
        padding: 20px 18px;
        display: flex;
        flex-direction: column;
        gap: 24px;
      }
      .brand { display: flex; align-items: center; gap: 10px; font-size: 1.4rem; font-weight: 700; }
      .brand-mark {
        width: 36px; height: 36px; border-radius: 10px;
        background: linear-gradient(135deg, #3b82f6, #8b5cf6);
        display: flex; align-items: center; justify-content: center; font-size: 0.9rem;
      }
      .nav { display: flex; flex-direction: column; gap: 10px; margin-top: 10px; }
      .nav a {
        text-decoration: none; color: #d1d5db; padding: 12px 14px;
        border-radius: 10px; transition: background 0.2s ease, color 0.2s ease;
        font-weight: 500;
      }
      .nav a:hover, .nav a.active { background: rgba(255, 255, 255, 0.08); color: #ffffff; }

      .sidebar-footer { margin-top: auto; }
      .logout-btn {
        width: 100%;
        padding: 10px 14px;
        border: none;
        border-radius: 10px;
        background: #1f2937;
        color: #f9fafb;
        font-size: 0.95rem;
        cursor: pointer;
        transition: background 0.2s ease;
      }
      .logout-btn:hover { background: #374151; }

      .main-panel { flex: 1; display: flex; flex-direction: column; min-width: 0; }
      .topbar {
        height: 80px; background: #ffffff; border-bottom: 1px solid #e5e7eb;
        padding: 0 28px; display: flex; align-items: center; justify-content: space-between;
      }
      .page-title { font-size: 1.3rem; font-weight: 600; }
      .user-profile {
        display: flex; align-items: center; gap: 12px;
        padding: 8px 12px; border-radius: 12px; background: #f3f4f6;
      }
      .avatar {
        width: 36px; height: 36px; border-radius: 50%;
        background: linear-gradient(135deg, #60a5fa, #a78bfa);
        display: flex; align-items: center; justify-content: center;
        color: white; font-weight: 700;
      }
      .user-meta { display: flex; flex-direction: column; line-height: 1.2; }
      .user-meta small { color: #6b7280; }
      .content { flex: 1; padding: 24px; overflow: auto; }
    `,
    ],
})
export class AppLayoutComponent implements OnInit {
    isAdmin = false;
    isAdminOrAgent = false;
    userInitial = 'П';
    userTitle = 'Пользователь';
    userEmail = 'user@itsm.local';

    constructor(
        private readonly authService: AuthService,
        private readonly router: Router,
    ) { }

    ngOnInit(): void {
        this.isAdmin = this.authService.isAdmin();
        this.isAdminOrAgent = this.authService.canRead();

        if (this.isAdmin) {
            this.userInitial = 'А';
            this.userTitle = 'Администратор';
            this.userEmail = 'admin@itsm.local';
            return;
        }
        if (this.isAdminOrAgent) {
            this.userInitial = 'И';
            this.userTitle = 'Инженер ТП';
            this.userEmail = 'agent@itsm.local';
            return;
        }
        this.userInitial = 'П';
        this.userTitle = 'Пользователь';
        this.userEmail = 'user@itsm.local';
    }

    logout(): void {
        this.authService.logout();
        this.router.navigateByUrl('/login');
    }
}