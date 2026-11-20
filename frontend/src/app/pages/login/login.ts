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
        @if (step === 2) {
          <button type="button" class="logout-link" (click)="cancelLogin()" [disabled]="busy" aria-label="Выйти" title="Выйти">
            <svg viewBox="0 0 24 24" aria-hidden="true">
              <path d="M10 17l5-5-5-5M15 12H3M14 3h5a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2h-5" />
            </svg>
            <span>Выйти</span>
          </button>
        }
        <h1>
          @if (step === 2) {
            <span class="security-icon" aria-hidden="true">
              <svg viewBox="0 0 24 24">
                <path d="M12 3l7 3v5c0 4.8-2.9 8.3-7 10-4.1-1.7-7-5.2-7-10V6l7-3z" />
                <path d="M9.5 12l1.7 1.7 3.7-4" />
              </svg>
            </span>
            {{ requiresSetup ? 'Настройка двухфакторной аутентификации' : 'Двухфакторная аутентификация' }}
          } @else if (step === 3) {
            <span class="security-icon" aria-hidden="true">
              <svg viewBox="0 0 24 24">
                <path d="M12 3l7 3v5c0 4.8-2.9 8.3-7 10-4.1-1.7-7-5.2-7-10V6l7-3z" />
                <path d="M9.5 12l1.7 1.7 3.7-4" />
              </svg>
            </span>
            Сохраните резервные коды
          } @else {
            Вход в ITSM
          }
        </h1>
        @if (environment.useMockAuth) {
          <p>Тестовый вход: любые непустые логин и пароль; код — любые 6 цифр. Логины admin, tenant_owner, agent, manager и tenant_admin выбирают роль, логин multi — пользователь с двумя ролями (выбор рабочего пространства).</p>
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
            <p class="hint">Подойдет Google Authenticator, Яндекс.Ключ или 2FAS.</p>
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
            <p>
              {{ recoveryMode
                ? 'Введите сохраненный резервный код.'
                : 'Введите 6-значный код из приложения-аутентификатора.' }}
            </p>
          }

          @if (recoveryMode) {
            <input
              #recoveryInput
              type="text"
              name="recoveryCode"
              [(ngModel)]="recoveryCode"
              (ngModelChange)="onRecoveryCodeChange($event)"
              (keydown.enter)="doVerify()"
              autocomplete="off"
              autocapitalize="characters"
              maxlength="14"
              placeholder="XXXX-XXXX-XX" />

            <button type="button" (click)="doVerify()" [disabled]="busy || !recoveryCode.trim()">
              {{ busy ? '...' : 'Войти с резервным кодом' }}
            </button>
            <button type="button" class="link-button" (click)="setRecoveryMode(false)" [disabled]="busy">
              Вернуться к коду приложения
            </button>
          } @else {
            <div class="otp-row" role="group" aria-label="6-значный код подтверждения">
              @for (digit of otpDigits; track $index) {
                <input
                  #otpInput
                  class="otp-input"
                  type="text"
                  inputmode="numeric"
                  pattern="[0-9]*"
                  autocomplete="{{ $index === 0 ? 'one-time-code' : 'off' }}"
                  maxlength="1"
                  [attr.aria-label]="'Цифра ' + ($index + 1)"
                  [value]="digit"
                  (input)="onOtpInput($index, $event)"
                  (keydown)="onOtpKeydown($index, $event)"
                  (paste)="onOtpPaste($event)" />
              }
            </div>

            <button type="button" (click)="doVerify()" [disabled]="busy">
              {{ busy ? '...' : (requiresSetup ? 'Подтвердить и включить 2FA' : 'Подтвердить') }}
            </button>
          }

          @if (canSkipSetup) {
            <button type="button" class="link-button" (click)="skipSetup()" [disabled]="busy">
              {{ busy ? '...' : 'Пропустить настройку' }}
            </button>
          }

          @if (!requiresSetup && !recoveryMode) {
            <button type="button" class="link-button" (click)="setRecoveryMode(true)" [disabled]="busy">
              У меня нет доступа к приложению
            </button>
          }
        }

        @if (step === 3) {
          <p>Используйте их вместо кода приложения, если потеряете доступ к приложению-аутентификатору.</p>
          <div class="recovery-grid" aria-label="Резервные коды">
            @for (recoveryCode of recoveryCodes; track recoveryCode) {
              <code>{{ recoveryCode }}</code>
            }
          </div>
          <button type="button" (click)="downloadRecoveryCodes()">
            Скачать коды
          </button>
          <label class="saved-codes">
            <input type="checkbox" [(ngModel)]="recoveryCodesSaved" />
            Я сохранил коды в надежном месте
          </label>
          <button type="button" [disabled]="!recoveryCodesSaved" (click)="finishLogin()">
            Продолжить
          </button>
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
        background: var(--app-bg);
        font-family: Arial, sans-serif;
        padding: 20px;
      }
      .card {
        position: relative;
        background: var(--app-surface);
        padding: 30px;
        border-radius: 14px;
        box-shadow: 0 8px 24px rgba(15, 23, 42, 0.12);
        border: 1px solid var(--app-border);
        width: 100%;
        max-width: 380px;
        display: flex;
        flex-direction: column;
        gap: 14px;
      }
      h1 { margin: 0 0 10px; text-align: center; font-size: 1.6rem; }
      .security-icon {
        display: flex;
        align-items: center;
        justify-content: center;
        width: 48px;
        height: 48px;
        margin: 0 auto 10px;
        border-radius: 50%;
        background: var(--app-selected);
        color: var(--app-primary);
      }
      .security-icon svg { width: 28px; height: 28px; fill: none; stroke: currentColor; stroke-width: 1.8; stroke-linecap: round; stroke-linejoin: round; }
      .logout-link {
        position: absolute;
        top: 14px;
        left: 14px;
        display: inline-flex;
        align-items: center;
        gap: 5px;
        width: auto;
        padding: 6px;
        background: transparent;
        color: var(--app-muted-strong);
        font-size: 0.82rem;
      }
      .logout-link svg { width: 18px; height: 18px; fill: none; stroke: currentColor; stroke-width: 1.8; stroke-linecap: round; stroke-linejoin: round; }
      .logout-link:hover { color: var(--app-primary); }
      p { margin: 0; text-align: center; color: var(--app-muted); font-size: 0.9rem; }
      h2 { margin: 0; text-align: center; font-size: 1.2rem; }
      .hint { color: var(--app-muted-strong); font-size: 0.82rem; }
      .recovery-grid {
        display: grid;
        grid-template-columns: 1fr 1fr;
        gap: 8px;
        padding: 12px;
        background: var(--app-surface-2);
        border: 1px solid var(--app-border);
        border-radius: 8px;
      }
      .recovery-grid code { text-align: center; letter-spacing: 1px; }
      .saved-codes { display: flex; align-items: center; gap: 8px; font-size: 0.88rem; color: var(--app-muted-strong); }
      input {
        padding: 12px;
        border: 1px solid var(--app-border);
        background: var(--app-input-bg);
        color: var(--app-input-text);
        border-radius: 8px;
        font-size: 1rem;
      }
      input:focus { border-color: var(--app-primary); outline: 2px solid var(--app-selected); }
      .otp-row { display: grid; grid-template-columns: repeat(6, 1fr); gap: 8px; direction: ltr; }
      .otp-input {
        width: 100%;
        min-width: 0;
        height: 52px;
        box-sizing: border-box;
        padding: 0;
        text-align: center;
        font-size: 1.35rem;
        font-weight: 700;
        letter-spacing: 0;
        color: var(--app-input-text);
        caret-color: var(--app-primary);
      }
      .qr-code { width: 220px; height: 220px; align-self: center; }
      details { font-size: 0.85rem; color: var(--app-muted-strong); }
      details summary { cursor: pointer; }
      details code { display: block; word-break: break-all; }
      button {
        padding: 12px;
        border: none;
        border-radius: 8px;
        background: var(--app-primary);
        color: #fff;
        font-size: 1rem;
        cursor: pointer;
      }
      button:disabled { opacity: 0.6; cursor: wait; }
      button.secondary { background: var(--app-surface-2); color: var(--app-text); border: 1px solid var(--app-border); }
      button.link-button { background: transparent; color: var(--app-primary); padding: 6px; font-size: 0.88rem; }
      .err { color: var(--app-error-text); background: var(--app-error-bg); border: 1px solid var(--app-error-border); padding: 10px; border-radius: 8px; text-align: center; font-size: 0.9rem; }
      .ok { color: var(--app-success-text); background: var(--app-success-bg); border: 1px solid var(--app-success-border); padding: 10px; border-radius: 8px; text-align: center; font-size: 0.9rem; }
    `,
    ],
})
export class LoginComponent {
    private readonly auth = inject(AuthService);
    private readonly router = inject(Router);
    private readonly cdr = inject(ChangeDetectorRef);
    readonly environment = environment;

    step: 1 | 2 | 3 = 1;
    username = '';
    password = '';
    code = '';
    otpDigits = ['', '', '', '', '', ''];
    recoveryCode = '';
    recoveryMode = false;
    challengeId = '';
    requiresSetup = false;
    canSkipSetup = false;
    secret = '';
    qrCodeImageBase64 = '';
    busy = false;
    error = '';
    info = '';
    recoveryCodes: string[] = [];
    recoveryCodesSaved = false;

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
                this.info = '';
                this.cdr.detectChanges();
                setTimeout(() => {
                    const input = document.querySelector<HTMLInputElement>('.otp-input');
                    input?.focus();
                    input?.select();
                });
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

        const c = this.recoveryMode
            ? this.recoveryCode.trim().toUpperCase().replace(/[^A-Z0-9-]/g, '')
            : this.otpDigits.join('').trim();
        if (this.recoveryMode) {
            if (!/^[A-Z0-9]{4}-?[A-Z0-9]{4}-?[A-Z0-9]{2}$/i.test(c)) {
                this.error = 'Введите резервный код в формате XXXX-XXXX-XX';
                return;
            }
        } else if (!/^\d{6}$/.test(c)) {
            this.error = 'Введите 6-значный код';
            return;
        }

        this.busy = true;
        this.error = '';
        this.info = '';

        this.auth.verifyTwoFactor(this.challengeId, c, this.recoveryMode).subscribe({
            next: (res) => {
                if (this.requiresSetup && res.recoveryCodes?.length) {
                    this.recoveryCodes = res.recoveryCodes;
                    this.recoveryCodesSaved = false;
                    this.step = 3;
                    this.busy = false;
                    this.cdr.detectChanges();
                    return;
                }
                this.completeLogin();
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
                this.completeLogin();
            },
            error: (err) => {
                this.error = err.message || 'Не удалось пропустить настройку 2FA';
                this.busy = false;
                this.cdr.detectChanges();
            },
        });
    }

    onCodeChange(value: string): void {
        const digits = value.replace(/\D/g, '').slice(0, 6);
        this.code = digits;
        if (digits.length === 6) {
            this.doVerify();
        }
    }

    onOtpInput(index: number, event: Event): void {
        const input = event.target as HTMLInputElement;
        const digit = input.value.replace(/\D/g, '').slice(-1);
        this.otpDigits[index] = digit;
        input.value = digit;

        if (digit && index < 5) {
            const inputs = Array.from(document.querySelectorAll<HTMLInputElement>('.otp-input'));
            inputs[index + 1]?.focus();
            inputs[index + 1]?.select();
        }

        if (this.otpDigits.join('').length === 6) {
            this.doVerify();
        }
    }

    onOtpKeydown(index: number, event: KeyboardEvent): void {
        if (event.key === 'Backspace' && !this.otpDigits[index] && index > 0) {
            event.preventDefault();
            const inputs = Array.from(document.querySelectorAll<HTMLInputElement>('.otp-input'));
            inputs[index - 1]?.focus();
            inputs[index - 1]?.select();
        }
    }

    onOtpPaste(event: ClipboardEvent): void {
        event.preventDefault();
        const digits = (event.clipboardData?.getData('text') ?? '')
            .replace(/\D/g, '')
            .slice(0, 6);
        if (!digits) return;

        this.otpDigits = ['', '', '', '', '', ''];
        for (let i = 0; i < digits.length; i++) {
            this.otpDigits[i] = digits[i];
        }

        const inputs = Array.from(document.querySelectorAll<HTMLInputElement>('.otp-input'));
        inputs.forEach((input, i) => input.value = this.otpDigits[i]);
        inputs[Math.min(digits.length, 5)]?.focus();
        this.cdr.detectChanges();

        if (digits.length === 6) {
            this.doVerify();
        }
    }

    onRecoveryCodeChange(value: string): void {
        const normalized = value
            .toUpperCase()
            .replace(/[^A-Z0-9]/g, '')
            .slice(0, 10);
        this.recoveryCode = normalized.length > 8
            ? `${normalized.slice(0, 4)}-${normalized.slice(4, 8)}-${normalized.slice(8)}`
            : normalized.length > 4
                ? `${normalized.slice(0, 4)}-${normalized.slice(4)}`
                : normalized;
    }

    setRecoveryMode(enabled: boolean): void {
        this.recoveryMode = enabled;
        this.error = '';
        this.recoveryCode = '';
        this.cdr.detectChanges();
        setTimeout(() => {
            const input = document.querySelector<HTMLInputElement>(
                enabled ? 'input[name="recoveryCode"]' : 'input[name="code"]',
            );
            input?.focus();
            input?.select();
        });
    }

    cancelLogin(): void {
        this.auth.logout();
        this.step = 1;
        this.username = '';
        this.password = '';
        this.code = '';
        this.otpDigits = ['', '', '', '', '', ''];
        this.recoveryCode = '';
        this.recoveryMode = false;
        this.challengeId = '';
        this.requiresSetup = false;
        this.canSkipSetup = false;
        this.recoveryCodes = [];
        this.recoveryCodesSaved = false;
        this.error = '';
        this.info = '';
    }

    downloadRecoveryCodes(): void {
        const content = [
            'Резервные коды ITSM',
            '',
            ...this.recoveryCodes,
            '',
            'Каждый код можно использовать один раз.',
        ].join('\n');
        const blob = new Blob([content], { type: 'text/plain;charset=utf-8' });
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = 'itsm-recovery-codes.txt';
        link.click();
        URL.revokeObjectURL(url);
        this.recoveryCodesSaved = true;
    }

    finishLogin(): void {
        if (this.recoveryCodesSaved) {
            this.completeLogin();
        }
    }

    /**
     * После 2FA запрашиваем сессию у бэкенда: он сообщает все роли пользователя
     * и нужно ли показать промежуточный экран выбора активной роли.
     */
    private completeLogin(): void {
        this.auth.loadSession().subscribe({
            next: (session) => {
                this.busy = false;
                this.cdr.detectChanges();

                if (session.requiresRoleSelection) {
                    this.router.navigateByUrl('/select-role');
                    return;
                }

                this.router.navigateByUrl(
                    this.auth.homeUrlForRole(session.activeRole ?? session.roles[0]),
                );
            },
            error: () => {
                // Эндпоинт сессии недоступен — используем роли из уже выданного JWT.
                this.busy = false;
                this.cdr.detectChanges();
                this.navigateAfterLogin();
            },
        });
    }

    private navigateAfterLogin(): void {
        if (this.auth.needsRoleSelection()) {
            this.router.navigateByUrl('/select-role');
            return;
        }

        this.router.navigateByUrl(
            this.auth.homeUrlForRole(this.auth.getActiveRole() ?? this.auth.getRole()),
        );
    }
}