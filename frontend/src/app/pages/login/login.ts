import { ChangeDetectorRef, Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [FormsModule],
  template: `
    <section class="page">
      <div class="card">
        <h1>Авторизация</h1>
        @if (environment.useMockAuth) {
          <p>Тестовый вход: любые непустые логин и пароль; код — любые 6 цифр. Логины admin, agent, manager и tenant_admin выбирают роль.</p>
        }

        @if (step === 1) {
          <p>Шаг 1: логин и пароль</p>

          <input
            type="text"
            name="username"
            [(ngModel)]="username"
            placeholder="Логин" />

          <input
            type="password"
            name="password"
            [(ngModel)]="password"
            placeholder="Пароль" />

          <button type="button" (click)="doLogin()" [disabled]="busy">
            {{ busy ? '...' : 'Войти' }}
          </button>
        }

        @if (step === 2) {
          @if (requiresSetup) {
            <p>Отсканируйте QR-код в приложении-аутентификаторе, затем введите полученный код.</p>
            @if (qrCodeImageBase64) {
              <img
                class="qr-code"
                [src]="'data:image/png;base64,' + qrCodeImageBase64"
                alt="QR-код для настройки 2FA" />
            } @else if (environment.useMockAuth) {
              <p>В тестовом режиме настройка QR-кода имитируется.</p>
            }
            @if (secret) {
              <details>
                <summary>Не получается отсканировать? Показать секрет</summary>
                <code>{{ secret }}</code>
              </details>
            }
          } @else {
            <p>Введите 6-значный код из приложения-аутентификатора.</p>
          }

          <input
            type="text"
            name="code"
            [(ngModel)]="code"
            inputmode="numeric"
            autocomplete="one-time-code"
            maxlength="6"
            placeholder="000000" />

          <button type="button" (click)="doVerify()" [disabled]="busy">
            {{ busy ? '...' : (requiresSetup ? 'Подтвердить и включить 2FA' : 'Подтвердить') }}
          </button>

          @if (canSkipSetup) {
            <button type="button" class="secondary" (click)="skipSetup()" [disabled]="busy">
              {{ busy ? '...' : 'Пропустить настройку' }}
            </button>
          }
        }

        @if (error) {
          <div class="err">{{ error }}</div>
        }

        @if (info) {
          <div class="ok">{{ info }}</div>
        }
      </div>
    </section>
  `,
  styles: [
    `
      .page {
        min-height: 100vh;
        display: flex;
        align-items: center;
        justify-content: center;
        background: #eef4ff;
        font-family: Arial, sans-serif;
        padding: 20px;
      }
      .card {
        background: #fff;
        padding: 30px;
        border-radius: 14px;
        box-shadow: 0 8px 24px rgba(0, 0, 0, 0.08);
        width: 100%;
        max-width: 380px;
        display: flex;
        flex-direction: column;
        gap: 14px;
      }
      h1 { margin: 0 0 10px; text-align: center; font-size: 1.6rem; }
      p { margin: 0; text-align: center; color: #666; font-size: 0.9rem; }
      input {
        padding: 12px;
        border: 1px solid #ccc;
        border-radius: 8px;
        font-size: 1rem;
      }
      input:focus { border-color: #3f51b5; outline: none; }
      .qr-code { width: 220px; height: 220px; align-self: center; }
      details { font-size: 0.85rem; color: #555; }
      details summary { cursor: pointer; }
      details code { display: block; word-break: break-all; }
      button {
        padding: 12px;
        border: none;
        border-radius: 8px;
        background: #3f51b5;
        color: #fff;
        font-size: 1rem;
        cursor: pointer;
      }
      button:disabled { opacity: 0.6; cursor: wait; }
      button.secondary { background: #e5e7eb; color: #111827; }
      .err { color: #c00; text-align: center; font-size: 0.9rem; }
      .ok { color: #080; text-align: center; font-size: 0.9rem; }
    `,
  ],
})
export class LoginComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly cdr = inject(ChangeDetectorRef);
  readonly environment = environment;

  step: 1 | 2 = 1;
  username = '';
  password = '';
  code = '';
  challengeId = '';
  requiresSetup = false;
  canSkipSetup = false;
  secret = '';
  qrCodeImageBase64 = '';
  busy = false;
  error = '';
  info = '';

  doLogin(): void {
    if (this.busy) return;

    const u = this.username.trim();
    const p = this.password.trim();

    if (!u || !p) {
      this.error = 'Введите логин и пароль';
      return;
    }

    this.busy = true;
    this.error = '';
    this.info = '';

    this.auth.login(u, p).subscribe({
      next: (res) => {
        this.challengeId = res.challengeId;
        this.requiresSetup = res.requiresTwoFactorSetup;
        this.canSkipSetup = res.canSkipTwoFactorSetup;
        this.secret = res.secret;
        this.qrCodeImageBase64 = res.qrCodeImageBase64;
        this.step = 2;
        this.busy = false;
        this.info = res.message || (environment.useMockAuth
          ? 'Введите шестизначный тестовый код.'
          : 'Введите код из приложения-аутентификатора.');
        this.cdr.detectChanges();
      },
      error: (err) => {
        this.error = err.status === 401
          ? 'Неверный логин или пароль'
          : err.status === 423 || err.status === 429
            ? 'Учетная запись временно заблокирована. Повторите попытку позже.'
            : err.message || 'Ошибка: ' + err.status;
        this.busy = false;
        this.cdr.detectChanges();
      },
    });
  }

  doVerify(): void {
    if (this.busy) return;

    const c = this.code.trim();
    if (!/^\d{6}$/.test(c)) {
      this.error = 'Введите 6-значный код';
      return;
    }

    this.busy = true;
    this.error = '';
    this.info = '';

    this.auth.verifyTwoFactor(this.challengeId, c).subscribe({
      next: () => {
        this.busy = false;
        this.cdr.detectChanges();
        this.navigateAfterLogin();
      },
      error: (err) => {
        this.error = err.status === 401
          ? 'Неверный или истёкший код'
          : err.message || 'Ошибка: ' + err.status;
        this.busy = false;
        this.cdr.detectChanges();
      },
    });
  }

  skipSetup(): void {
    if (this.busy || !this.canSkipSetup) return;

    this.busy = true;
    this.error = '';
    this.info = '';
    this.auth.skipTwoFactorSetup(this.challengeId).subscribe({
      next: () => {
        this.busy = false;
        this.navigateAfterLogin();
      },
      error: (err) => {
        this.error = err.message || 'Не удалось пропустить настройку 2FA';
        this.busy = false;
        this.cdr.detectChanges();
      },
    });
  }

  private navigateAfterLogin(): void {
    const destination = this.auth.requiresRoleSelection()
      ? '/select-role'
      : this.auth.isTenantAdmin() ? '/admin/tenants' : this.auth.isPortalUser() ? '/portal' : '/dashboard';
    this.router.navigateByUrl(destination);
  }
}