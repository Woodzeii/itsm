import { Component, OnDestroy, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { environment } from '../../../environments/environment';

interface NotificationItem {
  id: number;
  type: string;
  title: string;
  body?: string | null;
  ticketId?: number | null;
  isRead: boolean;
  createdAt: string;
  unreadCount: number;
}

@Component({
  selector: 'app-layout',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  template: `
    <div class="app-shell">
      <aside class="sidebar">
        <div class="brand">
          <img class="brand-logo" src="/itsm_nc_logo.svg" alt="" aria-hidden="true" />
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
          <a routerLink="/knowledge-base" routerLinkActive="active">
            <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M4 5.5A2.5 2.5 0 0 1 6.5 3H20v16H6.5A2.5 2.5 0 0 0 4 21V5.5Z"/><path d="M4 17.5A2.5 2.5 0 0 1 6.5 15H20M8 7h8m-8 4h8"/></svg>
            <span>База знаний</span>
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
            <a routerLink="/admin/users" routerLinkActive="active"><svg viewBox="0 0 24 24" aria-hidden="true"><circle cx="9" cy="8" r="3"/><path d="M3.5 20v-1.5a5.5 5.5 0 0 1 11 0V20M17 8h4m-2-2v4m-2 5h2a3 3 0 0 1 3 3v2"/></svg><span>Пользователи</span></a>
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
      </aside>

      <div class="main-panel">
        <header class="topbar">
          <div class="breadcrumb"><span>Рабочее пространство</span><span class="crumb-divider">/</span><strong>Service Desk</strong></div>
          <div class="top-actions">
          <div class="notification-wrapper">
            <button class="icon-button notification-button" type="button" [attr.aria-expanded]="notificationOpen"
              aria-label="Уведомления" (click)="toggleNotifications()">
              <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M18 9a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9Zm-8 13h4"/><path d="M9 18h6"/></svg>
              @if (unreadCount > 0) { <span class="notification-badge">{{ unreadCount > 99 ? '99+' : unreadCount }}</span> }
            </button>
            @if (notificationOpen) {
              <section class="notification-dropdown" role="dialog" aria-label="Уведомления">
                <header><strong>Уведомления</strong><button type="button" (click)="markAllNotificationsRead()" [disabled]="unreadCount === 0">Прочитать все</button></header>
                <div class="notification-list">
                  @for (notification of notifications; track notification.id) {
                    <button type="button" class="notification-item" [class.unread]="!notification.isRead"
                      (click)="openNotification(notification)">
                      <span class="notification-item-dot" aria-hidden="true"></span>
                      <span><strong>{{ notification.title }}</strong><small>{{ formatNotificationTime(notification.createdAt) }}</small></span>
                    </button>
                  }
                  @if (!notifications.length) { <p class="notification-empty">Новых уведомлений нет</p> }
                </div>
              </section>
            }
          </div>
          <span class="top-divider"></span>
          <div class="user-profile">
            <div class="avatar">{{ userInitial }}</div>
            <div class="user-meta">
              <strong>{{ userTitle }}</strong>
              <small>{{ userRole }}</small>
            </div>
            <span class="chevron" aria-hidden="true">⌄</span>
          </div>
          @if (isMultiRole) {
            <button
              class="role-switch"
              type="button"
              (click)="switchRole()"
              aria-label="Сменить активную роль"
              title="Сменить активную роль">
              <svg viewBox="0 0 24 24" fill="none" aria-hidden="true">
                <path d="M7 7h10l-3-3m3 3-3 3M17 17H7l3 3m-3-3 3-3" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"/>
              </svg>
              <span class="role-switch-label">Сменить роль</span>
            </button>
          }
          <button class="icon-button" type="button" (click)="logout()" aria-label="Выйти" title="Выйти">
            <svg viewBox="0 0 24 24" fill="none" aria-hidden="true"><path d="M14 4h6v16h-6M3 12h11m-4-4 4 4-4 4" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"/></svg>
          </button>
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

      .brand-logo {
        width: 48px;
        height: 48px;
        flex: 0 0 48px;
        object-fit: contain;
      }

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
      .nav { display: flex; flex-direction: column; gap: 10px; margin-top: 10px; }
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
      .nav a:hover, .nav a.active { background: rgba(255, 255, 255, 0.08); color: #ffffff; }

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
      .top-actions { display: flex; align-items: center; gap: 16px; }
      .role-switch {
        display: inline-flex;
        align-items: center;
        gap: 8px;
        height: 34px;
        padding: 0 12px;
        border: 1px solid #d8e0ec;
        border-radius: 9px;
        background: #fff;
        color: #344054;
        font-size: 12px;
        font-weight: 600;
        cursor: pointer;
      }
      .role-switch:hover { border-color: #2563eb; color: #2563eb; }
      .role-switch:focus-visible { outline: 2px solid #2563eb; outline-offset: 2px; }
      .role-switch svg { width: 16px; height: 16px; }
      .role-switch-label { white-space: nowrap; }
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
      .notification-wrapper { position: relative; }
      .notification-badge {
        position: absolute;
        top: -4px;
        right: -5px;
        min-width: 18px;
        height: 18px;
        padding: 0 5px;
        border: 2px solid #fff;
        border-radius: 10px;
        background: #dc2626;
        color: #fff;
        font-size: 9px;
        font-weight: 800;
        line-height: 14px;
        text-align: center;
        box-sizing: border-box;
      }
      .notification-dropdown {
        position: absolute;
        top: 44px;
        right: 0;
        z-index: 4000;
        width: 360px;
        max-height: 480px;
        overflow: hidden;
        border: 1px solid #dce3ed;
        border-radius: 10px;
        background: #fff;
        color: #202734;
        box-shadow: 0 16px 44px rgba(15, 23, 42, .18);
      }
      .notification-dropdown header {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: 12px;
        min-height: 48px;
        padding: 10px 14px;
        border-bottom: 1px solid #edf0f4;
      }
      .notification-dropdown header strong { font-size: 13px; }
      .notification-dropdown header button {
        border: 0;
        background: transparent;
        color: #2563eb;
        font-size: 11px;
        font-weight: 700;
        cursor: pointer;
      }
      .notification-dropdown header button:disabled { color: #98a1af; cursor: default; }
      .notification-list { max-height: 420px; overflow-y: auto; }
      .notification-item {
        display: grid;
        grid-template-columns: 8px minmax(0, 1fr);
        align-items: start;
        gap: 10px;
        width: 100%;
        padding: 12px 14px;
        border: 0;
        border-bottom: 1px solid #edf0f4;
        background: #fff;
        color: #344256;
        text-align: left;
        cursor: pointer;
      }
      .notification-item:hover { background: #f4f7fb; }
      .notification-item.unread { background: #eef5ff; }
      .notification-item-dot { width: 7px; height: 7px; margin-top: 5px; border-radius: 50%; background: #b7c0cd; }
      .notification-item.unread .notification-item-dot { background: #2563eb; }
      .notification-item span:last-child { display: grid; gap: 5px; min-width: 0; }
      .notification-item strong { font-size: 12px; line-height: 1.35; }
      .notification-item small { color: #7b8798; font-size: 10px; }
      .notification-empty { margin: 0; padding: 28px 14px; color: #7b8798; font-size: 12px; text-align: center; }
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
      .main-panel { flex: 1; display: flex; flex-direction: column; min-width: 0; }
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
        .brand-logo { width: 48px; height: 48px; flex-basis: 48px; }
        .nav-caption, .sidebar-spacer, .workspace-note { display: none; }
        .nav { flex-direction: row; overflow-x: auto; }
        .nav a { min-height: 36px; padding: 8px 10px; white-space: nowrap; }
        .nav a svg { width: 15px; height: 15px; }
        .topbar { height: 62px; flex-basis: 62px; padding: 0 16px; }
        .breadcrumb { font-size: 11px; }
        .top-actions { gap: 8px; }
        .top-actions .user-meta, .top-divider, .chevron { display: none; }
        .role-switch { padding: 0 8px; }
        .role-switch-label { display: none; }
      }

      @media (max-width: 420px) {
        .breadcrumb span:first-child, .crumb-divider { display: none; }
      }
      .user-meta { display: flex; flex-direction: column; line-height: 1.2; }
      .user-meta small { color: #6b7280; }
      .content { flex: 1; padding: 24px; overflow: auto; }
    `,
  ],
})
export class AppLayoutComponent implements OnInit, OnDestroy {
  private expiryTimer: ReturnType<typeof setTimeout> | null = null;
  private notificationTimer: ReturnType<typeof setInterval> | null = null;
  private readonly baseUrl = environment.apiBaseUrl;

  isAdmin = false;
  isAdminOrAgent = false;
  isTenantAdmin = false;
  isMultiRole = false;
  userInitial = 'П';
  userTitle = 'Пользователь';
  userRole = 'Пользователь';
  notifications: NotificationItem[] = [];
  unreadCount = 0;
  notificationOpen = false;

  constructor(
    private readonly authService: AuthService,
    private readonly router: Router,
    private readonly http: HttpClient,
  ) { }

  ngOnInit(): void {
    this.isAdmin = this.authService.isAdmin();
    this.isAdminOrAgent = this.authService.canRead();
    this.isTenantAdmin = this.authService.isTenantAdmin();
    this.isMultiRole = this.authService.hasMultipleRoles();
    const roleLabels = {
      admin: 'Администратор',
      tenant_owner: 'Владелец тенанта',
      agent: 'Инженер поддержки',
      manager: 'Руководитель',
      portal_user: 'Пользователь',
      tenant_admin: 'Администратор организации',
    };
    const login = this.authService.getLogin();
    this.userRole = roleLabels[this.authService.getRole()];
    this.userTitle = login || this.userRole;
    this.userInitial = (login || this.userRole).charAt(0).toLocaleUpperCase();
    this.scheduleExpiry();
    this.loadNotifications();
    this.notificationTimer = setInterval(() => this.loadNotifications(), 45_000);
  }

  ngOnDestroy(): void {
    if (this.expiryTimer !== null) {
      clearTimeout(this.expiryTimer);
      this.expiryTimer = null;
    }
    if (this.notificationTimer !== null) {
      clearInterval(this.notificationTimer);
      this.notificationTimer = null;
    }
  }

  toggleNotifications(): void {
    this.notificationOpen = !this.notificationOpen;
  }

  loadNotifications(): void {
    this.http.get<NotificationItem[]>(`${this.baseUrl}/notifications?skip=0&take=30`)
      .subscribe({
        next: items => {
          this.notifications = items;
          this.unreadCount = items[0]?.unreadCount
            ?? items.filter(item => !item.isRead).length;
        },
        error: () => undefined,
      });
  }

  markAllNotificationsRead(): void {
    this.http.post<{ markedRead: number }>(`${this.baseUrl}/notifications/read-all`, {})
      .subscribe({
        next: () => {
          this.notifications = this.notifications.map(item => ({ ...item, isRead: true, unreadCount: 0 }));
          this.unreadCount = 0;
        },
        error: () => undefined,
      });
  }

  openNotification(notification: NotificationItem): void {
    if (!notification.isRead) {
      this.http.patch<NotificationItem>(
        `${this.baseUrl}/notifications/${notification.id}/read`,
        {},
      ).subscribe({
        next: updated => {
          this.notifications = this.notifications.map(item =>
            item.id === notification.id ? updated : item);
          this.unreadCount = updated.unreadCount;
        },
        error: () => undefined,
      });
    }

    this.notificationOpen = false;
    if (notification.ticketId) {
      this.router.navigate(['/tickets'], { queryParams: { ticketId: notification.ticketId } });
    }
  }

  formatNotificationTime(value: string): string {
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return '';
    return new Intl.DateTimeFormat('ru-RU', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    }).format(date);
  }

  /** Автоматически завершает сессию, когда истекает срок действия токена. */
  private scheduleExpiry(): void {
    const remaining = this.authService.getTokenRemainingMs();
    if (remaining === null) return;

    if (remaining <= 0) {
      this.logout();
      return;
    }

    this.expiryTimer = setTimeout(() => this.logout(), remaining);
  }

  switchRole(): void {
    this.router.navigateByUrl('/select-role?switch=1');
  }

  logout(): void {
    this.authService.logout();
    this.router.navigateByUrl('/login');
  }
}