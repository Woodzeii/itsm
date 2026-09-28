import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';

@Component({
    selector: 'app-login',
    standalone: true,
    imports: [FormsModule],
    template: `
    <section class="page">
      <form class="login-card" (ngSubmit)="login()">
        <h1>Авторизация</h1>
        <p>Введите данные для входа в систему</p>

        <label>
          <span>Логин</span>
          <input type="text" [(ngModel)]="loginValue" name="login" placeholder="Введите логин" required />
        </label>

        <label>
          <span>Пароль</span>
          <input type="password" [(ngModel)]="password" name="password" placeholder="Введите пароль" required />
        </label>

        <button type="submit" [disabled]="isSubmitting">
          {{ isSubmitting ? 'Вход...' : 'Войти' }}
        </button>

        @if (errorMessage) {
          <small class="error">{{ errorMessage }}</small>
        }
      </form>
    </section>
  `,
    styles: [
        `
      .page {
        min-height: 100vh;
        display: flex;
        align-items: center;
        justify-content: center;
        background: linear-gradient(135deg, #eef4ff 0%, #f5f7fb 100%);
        padding: 24px;
        font-family: Arial, sans-serif;
      }

      .login-card {
        width: 100%;
        max-width: 420px;
        background: #ffffff;
        border-radius: 16px;
        box-shadow: 0 12px 32px rgba(15, 23, 42, 0.08);
        padding: 32px 28px;
        display: flex;
        flex-direction: column;
        gap: 18px;
      }

      h1 {
        margin: 0;
        font-size: 2rem;
        text-align: center;
        color: #1f2937;
      }

      p {
        margin: 0;
        text-align: center;
        color: #4b5563;
      }

      label {
        display: flex;
        flex-direction: column;
        gap: 8px;
        font-size: 0.95rem;
        color: #374151;
      }

      input {
        padding: 12px 14px;
        border: 1px solid #d1d5db;
        border-radius: 10px;
        font-size: 1rem;
        outline: none;
        transition: border-color 0.2s ease, box-shadow 0.2s ease;
      }

      input:focus {
        border-color: #3f51b5;
        box-shadow: 0 0 0 3px rgba(63, 81, 181, 0.16);
      }

      button {
        margin-top: 8px;
        padding: 12px 16px;
        border: none;
        border-radius: 10px;
        background: #3f51b5;
        color: white;
        font-size: 1rem;
        cursor: pointer;
        transition: opacity 0.2s ease;
      }

      button:disabled {
        opacity: 0.7;
        cursor: not-allowed;
      }

      .error {
        color: #b91c1c;
        font-size: 0.85rem;
      }
    `,
    ],
})
export class LoginComponent {
    loginValue = '';
    password = '';
    errorMessage = '';
    isSubmitting = false;

    constructor(
        private readonly authService: AuthService,
        private readonly router: Router,
    ) { }

    login(): void {
        const loginTrimmed = this.loginValue.trim();
        const passwordTrimmed = this.password.trim();

        if (!loginTrimmed || !passwordTrimmed) {
            this.errorMessage = 'Введите логин и пароль';
            return;
        }

        this.isSubmitting = true;
        this.errorMessage = '';

        setTimeout(() => {
            this.authService.login();
            this.isSubmitting = false;
            this.router.navigateByUrl('/dashboard');
        }, 300);
    }
}
