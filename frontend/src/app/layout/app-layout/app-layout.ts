import { Component, OnInit } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';

@Component({
    selector: 'app-layout',
    standalone: true,
    imports: [RouterLink, RouterLinkActive, RouterOutlet],
    template: `
    <div class="app-shell">
      <aside class="sidebar">
        <div class="brand">
          <div class="brand-mark" aria-hidden="true">
            <svg viewBox="0 0 24 24" fill="none"><circle cx="12" cy="12" r="8.5" stroke="currentColor" stroke-width="2.4"/><path d="M12 3.5v4M12 16.5v4M3.5 12h4m9 0h4" stroke="currentColor" stroke-width="2.4" stroke-linecap="round"/></svg>
          </div>
          <div class="brand-copy"><strong>ITSM <span>NC</span></strong><small>Service management</small></div>
        </div>

        @if (isAdminOrAgent || isAdmin) {
        <div class="nav-group">
          <p class="nav-caption">Рабочее пространство</p>
          <nav class="nav">
          <a routerLink="/dashboard" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }">
            <svg viewBox="0 0 24 24" aria-hidden="true"><rect x="3.5" y="3.5" width="7" height="7" rx="1.5"/><rect x="13.5" y="3.5" width="7" height="5" rx="1.5"/><rect x="13.5" y="11.5" width="7" height="9" rx="1.5"/><rect x="3.5" y="13.5" width="7" height="7" rx="1.5"/></svg>
            <span>Обзор</span>
          </a>
          <a routerLink="/tickets" routerLinkActive="active">
            <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M4 5.5h16v13H4zM8 9h8m-8 4h5"/></svg>
            <span>Заявки</span>
          </a>
          <a routerLink="/assets" routerLinkActive="active">
            <svg viewBox="0 0 24 24" aria-hidden="true"><path d="m12 3 8.5 4.5v9L12 21l-8.5-4.5v-9L12 3Z"/><path d="m3.8 7.7 8.2 4.5 8.2-4.5M12 12.2V21"/></svg>
            <span>Активы</span>
          </a>
          <a routerLink="/escalations" routerLinkActive="active">
            <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 3 22 20H2L12 3Z"/><path d="M12 9v5m0 3h.01"/></svg>
            <span>Эскалации</span>
          </a>
          @if (isAdmin) {
            <a routerLink="/dictionaries" routerLinkActive="active">
              <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M5 4.5h14a1.5 1.5 0 0 1 1.5 1.5v12a1.5 1.5 0 0 1-1.5 1.5H5A2.5 2.5 0 0 1 2.5 17V7A2.5 2.5 0 0 1 5 4.5Z"/><path d="M7.5 8.5h9m-9 4h9m-9 4h5"/></svg>
              <span>Справочники</span>
            </a>
            <a routerLink="/asset-classes" routerLinkActive="active">
              <svg viewBox="0 0 24 24" aria-hidden="true"><path d="m12 3 8.5 4.5v9L12 21l-8.5-4.5v-9L12 3Z"/><path d="m3.8 7.7 8.2 4.5 8.2-4.5M12 12.2V21"/></svg>
              <span>Классы активов</span>
            </a>
            <a routerLink="/admin" routerLinkActive="active">
              <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 3.5 14 5l2.6-.2.9 2.4 2.2 1.3-.7 2.5.7 2.5-2.2 1.3-.9 2.4L14 17l-2 1.5L10 17l-2.6.2-.9-2.4-2.2-1.3.7-2.5-.7-2.5 2.2-1.3.9-2.4L10 5l2-1.5Z"/><circle cx="12" cy="11.5" r="3"/></svg>
              <span>Администрирование</span>
            </a>
            <a routerLink="/admin/forms" routerLinkActive="active"><svg viewBox="0 0 24 24" aria-hidden="true"><path d="M5 4h14v16H5zM8 8h8m-8 4h8m-8 4h5"/></svg><span>Конструктор форм</span></a>
            <a routerLink="/admin/escalation-policies" routerLinkActive="active"><svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 3 22 20H2L12 3Z"/><path d="M12 9v5m0 3h.01"/></svg><span>Правила эскалации</span></a>
            <a routerLink="/admin/reports" routerLinkActive="active"><svg viewBox="0 0 24 24" aria-hidden="true"><path d="M5 4h14v16H5zM8 8h8m-8 4h8m-8 4h5"/></svg><span>Отчёты</span></a>
          }
          </nav>
        </div>
        } @else if (isTenantAdmin) {
          <nav class="nav"><a routerLink="/admin/tenants" routerLinkActive="active"><svg viewBox="0 0 24 24" aria-hidden="true"><path d="M3 20h18M5 20V7l7-4 7 4v13M9 20v-5h6v5M9 9h.01M15 9h.01"/></svg><span>Тенанты</span></a></nav>
        } @else {
          <nav class="nav"><a routerLink="/portal" routerLinkActive="active"><svg viewBox="0 0 24 24" aria-hidden="true"><path d="M4 5.5h16v13H4zM8 9h8m-8 4h5"/></svg><span>Мои заявки</span></a></nav>
        }

        <div class="sidebar-spacer"></div>
        <div class="sidebar-profile">
          <div class="avatar">{{ userInitial }}</div>
          <div class="user-meta"><strong>{{ userTitle }}</strong><small>{{ userRole }}</small></div>
          <span class="profile-more" aria-hidden="true">···</span>
        </div>
      </aside>

      <div class="main-panel">
        <header class="topbar">
          <div class="breadcrumb"><span>Рабочее пространство</span><span class="crumb-divider">/</span><strong>Service Desk</strong></div>
          <div class="top-actions">
          <button class="icon-button notification-button" type="button" aria-label="Уведомления">
            <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M18 9a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9Zm-8 13h4"/><path d="M9 18h6"/></svg>
            <span class="notification-dot"></span>
          </button>
          <span class="top-divider"></span>
          <div class="user-profile">
            <div class="avatar">{{ userInitial }}</div>
            <div class="user-meta">
              <strong>{{ userTitle }}</strong>
              <small>{{ userRole }}</small>
            </div>
            <span class="chevron" aria-hidden="true">⌄</span>
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
        min-height: 100vh;
        background: #f7f8fb;
        color: #111827;
      }

      .app-shell {
        display: flex;
        min-height: 100vh;
      }

      .sidebar {
        width: 252px;
        flex: 0 0 252px;
        background: #fff;
        border-right: 1px solid #e2e6ed;
        padding: 0 16px 16px;
        display: flex;
        flex-direction: column;
        min-height: 100vh;
      }

      .brand {
        height: 76px;
        margin: 0 -16px 20px;
        padding: 0 24px;
        display: flex;
        align-items: center;
        gap: 12px;
        border-bottom: 1px solid #eef0f4;
      }

      .brand-mark {
        width: 36px;
        height: 36px;
        border-radius: 11px;
        background: #2563eb;
        color: #fff;
        display: grid;
        place-items: center;
        box-shadow: 0 2px 4px #2563eb30;
      }

      .brand-mark svg { width: 21px; height: 21px; }
      .brand-copy { display: flex; flex-direction: column; gap: 2px; }
      .brand-copy strong { font-size: 15px; line-height: 1.2; }
      .brand-copy strong span { color: #2563eb; }
      .brand-copy small { color: #98a1af; font-size: 11px; }
      .nav-caption {
        margin: 0;
        padding: 0 12px 8px;
        color: #9aa3b1;
        font-size: 10px;
        font-weight: 700;
        letter-spacing: .08em;
        text-transform: uppercase;
      }

      .nav {
        display: flex;
        flex-direction: column;
        gap: 3px;
      }

      .nav a {
        display: flex;
        align-items: center;
        gap: 12px;
        min-height: 40px;
        padding: 9px 12px;
        border-radius: 7px;
        color: #586273;
        text-decoration: none;
        font-weight: 500;
        font-size: 13px;
        transition: background .16s ease, color .16s ease;
      }

      .nav a svg, .icon-button svg {
        width: 17px;
        height: 17px;
        flex: 0 0 auto;
        fill: none;
        stroke: currentColor;
        stroke-width: 1.7;
        stroke-linecap: round;
        stroke-linejoin: round;
      }

      .nav a:hover,
      .nav a.active {
        background: #eff5ff;
        color: #1d5ed6;
      }

      .nav a.active { font-weight: 650; }
      .sidebar-spacer { flex: 1; }
      .workspace-note {
        display: flex;
        align-items: center;
        gap: 8px;
        padding: 12px;
        color: #778293;
        font-size: 11px;
      }
      .status-dot, .notification-dot {
        width: 7px;
        height: 7px;
        border-radius: 50%;
        background: #21a675;
      }
      .sidebar-profile {
        display: flex;
        align-items: center;
        gap: 9px;
        padding: 12px 10px;
        background: #f6f7f9;
        border-radius: 9px;
      }
      .avatar {
        width: 32px;
        height: 32px;
        flex: 0 0 32px;
        border-radius: 50%;
        background: #e4efff;
        display: grid;
        place-items: center;
        color: #215fc2;
        font-size: 11px;
        font-weight: 700;
      }
      .user-meta { min-width: 0; display: flex; flex-direction: column; gap: 3px; }
      .user-meta strong, .user-meta small { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
      .user-meta strong { color: #303846; font-size: 11px; font-weight: 650; }
      .user-meta small { color: #939baa; font-size: 10px; }
      .profile-more { margin-left: auto; color: #9aa3b1; font-size: 20px; line-height: 1; }
      .top-actions { display: flex; align-items: center; gap: 16px; }
      .icon-button {
        position: relative;
        width: 34px;
        height: 34px;
        padding: 8px;
        border: 0;
        background: transparent;
        color: #697586;
        cursor: pointer;
      }
      .notification-dot { position: absolute; width: 6px; height: 6px; top: 6px; right: 6px; background: #2563eb; }
      .top-divider { width: 1px; height: 24px; background: #e4e7ec; }
      .chevron { color: #97a0ae; font-size: 15px; }
      .breadcrumb { display: flex; align-items: center; gap: 9px; color: #98a1af; font-size: 12px; }
      .breadcrumb strong { color: #586273; font-weight: 550; }
      .crumb-divider { color: #c4c9d1; }

      .main-panel {
        flex: 1;
        display: flex;
        flex-direction: column;
        min-width: 0;
      }

      .topbar {
        height: 76px;
        flex: 0 0 76px;
        background: #fff;
        border-bottom: 1px solid #e2e6ed;
        padding: 0 32px;
        display: flex;
        align-items: center;
        justify-content: space-between;
      }

      .user-profile {
        display: flex;
        align-items: center;
        gap: 9px;
      }

      .content {
        flex: 1;
        min-width: 0;
      }

      @media (max-width: 760px) {
        .app-shell { flex-direction: column; }
        .sidebar { width: 100%; min-height: 0; flex: 0 0 auto; padding: 0 12px 10px; border-right: 0; border-bottom: 1px solid #e2e6ed; }
        .brand { height: 62px; min-height: 62px; margin: 0 -12px 8px; padding: 0 16px; }
        .nav-caption, .sidebar-spacer, .workspace-note, .sidebar-profile { display: none; }
        .nav { flex-direction: row; overflow-x: auto; }
        .nav a { min-height: 36px; padding: 8px 10px; white-space: nowrap; }
        .nav a svg { width: 15px; height: 15px; }
        .topbar { height: 62px; flex-basis: 62px; padding: 0 16px; }
        .breadcrumb { font-size: 11px; }
        .top-actions { gap: 8px; }
        .top-actions .user-meta, .top-divider, .chevron { display: none; }
      }

      @media (max-width: 420px) {
        .breadcrumb span:first-child, .crumb-divider { display: none; }
      }
    `,
    ],
})
export class AppLayoutComponent implements OnInit {
    isAdmin = false;
    isAdminOrAgent = false;
    isTenantAdmin = false;
    userInitial = 'П';
    userTitle = 'Пользователь';
    userRole = 'Пользователь';

    constructor(private readonly authService: AuthService) { }

    ngOnInit(): void {
        this.isAdmin = this.authService.isAdmin();
        this.isAdminOrAgent = this.authService.canRead();
        this.isTenantAdmin = this.authService.isTenantAdmin();
        const roleLabels = {
          admin: 'Администратор',
          agent: 'Инженер поддержки',
          manager: 'Руководитель',
          portal_user: 'Пользователь',
          tenant_admin: 'Администратор организации',
        };
        const login = this.authService.getLogin();
        this.userRole = roleLabels[this.authService.getRole()];
        this.userTitle = login || this.userRole;
        this.userInitial = (login || this.userRole).charAt(0).toLocaleUpperCase();
    }
}