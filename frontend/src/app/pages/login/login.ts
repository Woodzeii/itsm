import { ChangeDetectorRef, Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';

@Component({
    selector: 'app-login',
    standalone: true,
    imports: [FormsModule],
    template: `
    <section class="page">
      <div class="card">
        <h1>Авторизация</h1>

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
          <p>Шаг 2: 6-значный код из логов backend</p>

          <input
            type="text"
            name="code"
            [(ngModel)]="code"
            maxlength="6"
            placeholder="000000" />

          <button type="button" (click)="doVerify()" [disabled]="busy">
            {{ busy ? '...' : 'Подтвердить' }}
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
      .err { color: #c00; text-align: center; font-size: 0.9rem; }
      .ok { color: #080; text-align: center; font-size: 0.9rem; }
    `,
    ],
})
export class LoginComponent {
    private readonly auth = inject(AuthService);
    private readonly router = inject(Router);
    private readonly cdr = inject(ChangeDetectorRef);

    step: 1 | 2 = 1;
    username = 'admin';
    password = 'admin123';
    code = '';
    userId = 0;
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

        console.log('[LOGIN] отправляю запрос');

        this.auth.login(u, p).subscribe({
            next: (res) => {
                console.log('[LOGIN] ответ', res);
                this.userId = res.userId;
                this.step = 2;
                this.busy = false;
                this.info = 'Код отправлен. Смотрите логи backend.';
                this.cdr.detectChanges();
            },
            error: (err) => {
                console.log('[LOGIN] ошибка', err);
                this.error = err.status === 401
                    ? 'Неверный логин или пароль'
                    : 'Ошибка: ' + err.status;
                this.busy = false;
                this.cdr.detectChanges();
            },
        });
    }

    doVerify(): void {
        if (this.busy) return;

        const c = this.code.trim();
        if (!c) {
            this.error = 'Введите код';
            return;
        }

        this.busy = true;
        this.error = '';
        this.info = '';

        console.log('[VERIFY] отправляю запрос', { userId: this.userId });

        this.auth.verifyTwoFactor(this.userId, c).subscribe({
            next: (res) => {
                console.log('[VERIFY] успех');
                this.busy = false;
                this.cdr.detectChanges();
                this.router.navigateByUrl('/dashboard');
            },
            error: (err) => {
                console.log('[VERIFY] ошибка', err);
                this.error = err.status === 401
                    ? 'Неверный или истёкший код'
                    : 'Ошибка: ' + err.status;
                this.busy = false;
                this.cdr.detectChanges();
            },
        });
    }
}