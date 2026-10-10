import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { TwoFactorService } from '../../core/auth/two-factor.service';
import {
    TwoFactorSetupResponse,
    TwoFactorStatusResponse,
} from '../../core/auth/two-factor.models';

@Component({
    selector: 'app-security',
    standalone: true,
    imports: [CommonModule, FormsModule],
    template: `
    <section class="security-page">
      <header class="page-header">
        <h1>Безопасность</h1>
        <p class="subtitle">Управление многофакторной аутентификацией (MFA / TOTP)</p>
      </header>

      @if (errorMessage) {
        <div class="error">{{ errorMessage }}</div>
      }
      @if (successMessage) {
        <div class="success">{{ successMessage }}</div>
      }

      <div class="card">
        <div class="card-header">
          <div>
            <h2>Двухфакторная аутентификация (TOTP)</h2>
            <p class="card-subtitle">
              Используйте Google Authenticator, Microsoft Authenticator или Authy.
            </p>
          </div>

          @if (status) {
            <span class="badge" [class.badge-on]="status.isEnabled" [class.badge-off]="!status.isEnabled">
              {{ status.isEnabled ? 'Включена' : 'Выключена' }}
            </span>
          }
        </div>

        @if (!status) {
          <p class="loading">Загрузка статуса...</p>
        }

        @if (status && !status.isEnabled && !setupData) {
          <div class="section">
            <p>
              2FA не настроена. Включите её, чтобы защитить учётную запись.
              Понадобится приложение-аутентификатор.
            </p>
            <button class="primary" (click)="startSetup()" [disabled]="busy">
              {{ busy ? 'Подготовка...' : 'Настроить 2FA' }}
            </button>
          </div>
        }

        @if (setupData) {
          <div class="section setup-section">
            <h3>Шаг 1. Отсканируйте QR-код</h3>
            <p>Откройте приложение-аутентификатор и добавьте новый аккаунт.</p>

            <div class="qr-wrap">
              <img
                [src]="qrUrl(setupData.otpAuthUri)"
                alt="QR-код для настройки 2FA"
                class="qr-image" />
            </div>

            <details class="secret-details">
              <summary>Не получается отсканировать? Показать секрет</summary>
              <code class="secret">{{ setupData.secret }}</code>
            </details>

            <h3>Шаг 2. Введите код из приложения</h3>
            <div class="code-form">
              <input
                type="text"
                inputmode="numeric"
                maxlength="6"
                placeholder="000000"
                [(ngModel)]="verificationCode"
                (keyup.enter)="confirmSetup()"
                class="code-input" />
              <button class="primary" (click)="confirmSetup()" [disabled]="busy || verificationCode.length !== 6">
                {{ busy ? 'Проверка...' : 'Включить 2FA' }}
              </button>
              <button class="secondary" (click)="cancelSetup()" [disabled]="busy">
                Отмена
              </button>
            </div>
          </div>
        }

        @if (status && status.isEnabled) {
          <div class="section">
            <p>
              <strong>2FA включена.</strong>
              @if (status.enabledAt) {
                <span class="muted">Дата включения: {{ status.enabledAt | date:'dd.MM.yyyy HH:mm' }}</span>
              }
            </p>
            <p class="muted">
              При следующем входе потребуется 6-значный код из приложения-аутентификатора.
            </p>

            @if (!showDisableForm) {
              <button class="danger" (click)="showDisableForm = true" [disabled]="busy">
                Отключить 2FA
              </button>
            } @else {
              <div class="disable-form">
                <p class="warn">Для отключения подтвердите пароль:</p>
                <input
                  type="password"
                  placeholder="Пароль"
                  [(ngModel)]="disablePassword"
                  (keyup.enter)="disableTwoFactor()"
                  class="code-input" />
                <button class="danger" (click)="disableTwoFactor()" [disabled]="busy || !disablePassword">
                  {{ busy ? 'Отключение...' : 'Подтвердить' }}
                </button>
                <button class="secondary" (click)="cancelDisable()" [disabled]="busy">
                  Отмена
                </button>
              </div>
            }
          </div>
        }
      </div>
    </section>
  `,
    styles: [
        `
      .security-page { padding: 24px; font-family: Arial, sans-serif; max-width: 720px; }
      .page-header h1 { margin: 0 0 4px; }
      .subtitle { margin: 0 0 20px; color: #6b7280; font-size: 0.9rem; }

      .card {
        background: #fff;
        border-radius: 12px;
        padding: 20px 24px;
        box-shadow: 0 2px 8px rgba(15, 23, 42, 0.06);
      }
      .card-header {
        display: flex;
        justify-content: space-between;
        align-items: flex-start;
        gap: 16px;
        margin-bottom: 16px;
      }
      .card-header h2 { margin: 0 0 4px; font-size: 1.15rem; }
      .card-subtitle { margin: 0; color: #6b7280; font-size: 0.85rem; }

      .badge { padding: 4px 12px; border-radius: 12px; font-size: 0.8rem; font-weight: 600; }
      .badge-on { background: #dcfce7; color: #166534; }
      .badge-off { background: #f3f4f6; color: #374151; }

      .section { margin-top: 12px; }
      .section p { margin: 8px 0; }
      .muted { color: #6b7280; font-size: 0.85rem; }

      button {
        padding: 9px 16px;
        border: none;
        border-radius: 8px;
        font-size: 0.95rem;
        cursor: pointer;
        margin-right: 8px;
        margin-top: 8px;
      }
      button:disabled { opacity: 0.5; cursor: not-allowed; }
      button.primary { background: #3f51b5; color: #fff; }
      button.secondary { background: #e5e7eb; color: #111827; }
      button.danger { background: #dc2626; color: #fff; }

      .setup-section { border-top: 1px solid #e5e7eb; padding-top: 16px; }
      .setup-section h3 { margin: 12px 0 8px; font-size: 1rem; }

      .qr-wrap {
        display: inline-block;
        padding: 12px;
        background: #fff;
        border: 1px solid #e5e7eb;
        border-radius: 12px;
        margin: 8px 0;
      }
      .qr-image { display: block; width: 220px; height: 220px; }

      .secret-details { margin: 8px 0 16px; font-size: 0.85rem; color: #374151; }
      .secret-details summary { cursor: pointer; }
      .secret {
        display: inline-block;
        margin-top: 6px;
        padding: 6px 10px;
        background: #f9fafb;
        border-radius: 6px;
        font-family: monospace;
        font-size: 0.85rem;
        word-break: break-all;
      }

      .code-form, .disable-form { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
      .code-input {
        padding: 10px 12px;
        border: 1px solid #d1d5db;
        border-radius: 8px;
        font-size: 1rem;
        font-family: monospace;
        letter-spacing: 4px;
        width: 150px;
        text-align: center;
      }
      .warn { color: #b45309; font-size: 0.9rem; width: 100%; margin: 4px 0; }

      .error { color: #b91c1c; padding: 10px 12px; background: #fef2f2; border-radius: 8px; margin-bottom: 12px; }
      .success { color: #166534; padding: 10px 12px; background: #f0fdf4; border-radius: 8px; margin-bottom: 12px; }
      .loading { color: #6b7280; }
    `,
    ],
})
export class SecurityComponent implements OnInit {
    private readonly api = inject(TwoFactorService);
    private readonly router = inject(Router);
    private readonly cdr = inject(ChangeDetectorRef);

    status: TwoFactorStatusResponse | null = null;
    setupData: TwoFactorSetupResponse | null = null;

    verificationCode = '';
    disablePassword = '';
    showDisableForm = false;

    busy = false;
    errorMessage = '';
    successMessage = '';

    ngOnInit(): void {
        this.loadStatus();
    }

    private clearMessages(): void {
        this.errorMessage = '';
        this.successMessage = '';
    }

    private loadStatus(): void {
        this.api.status().subscribe({
            next: s => {
                this.status = s;
                this.cdr.detectChanges();
            },
            error: err => {
                this.errorMessage = `Не удалось получить статус: ${err.status}`;
                this.cdr.detectChanges();
            },
        });
    }

    startSetup(): void {
        this.clearMessages();
        this.busy = true;
        this.api.setup().subscribe({
            next: data => {
                this.setupData = data;
                this.verificationCode = '';
                this.busy = false;
                this.cdr.detectChanges();
            },
            error: err => {
                this.errorMessage = `Ошибка: ${err.status} ${err.error?.message ?? ''}`;
                this.busy = false;
                this.cdr.detectChanges();
            },
        });
    }

    confirmSetup(): void {
        if (this.verificationCode.length !== 6) return;
        this.clearMessages();
        this.busy = true;
        this.api.verifySetup({ code: this.verificationCode }).subscribe({
            next: () => {
                this.successMessage = '2FA успешно включена';
                this.setupData = null;
                this.verificationCode = '';
                this.busy = false;
                this.loadStatus();
            },
            error: err => {
                this.errorMessage = `Неверный код или ошибка: ${err.status} ${err.error?.message ?? ''}`;
                this.busy = false;
                this.cdr.detectChanges();
            },
        });
    }

    cancelSetup(): void {
        this.setupData = null;
        this.verificationCode = '';
        this.clearMessages();
    }

    disableTwoFactor(): void {
        if (!this.disablePassword) return;
        this.clearMessages();
        this.busy = true;
        this.api.disable({ password: this.disablePassword }).subscribe({
            next: () => {
                this.successMessage = '2FA отключена';
                this.disablePassword = '';
                this.showDisableForm = false;
                this.busy = false;
                this.loadStatus();
            },
            error: err => {
                this.errorMessage = err.status === 401
                    ? 'Неверный пароль'
                    : `Ошибка: ${err.status} ${err.error?.message ?? ''}`;
                this.busy = false;
                this.cdr.detectChanges();
            },
        });
    }

    cancelDisable(): void {
        this.showDisableForm = false;
        this.disablePassword = '';
        this.clearMessages();
    }

    qrUrl(otpAuthUri: string): string {
        return `https://api.qrserver.com/v1/create-qr-code/?size=220x220&data=${encodeURIComponent(otpAuthUri)}`;
    }
}