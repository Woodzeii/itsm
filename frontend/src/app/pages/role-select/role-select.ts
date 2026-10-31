import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService, type UserRole } from '../../core/auth/auth.service';
import { SessionResponse } from '../../core/auth/auth.models';
import { WorkspaceOption, workspaceOptionsFor } from '../../core/auth/workspace-options';

const styles = [`
  .page {
    min-height: 100vh;
    display: flex;
    align-items: center;
    justify-content: center;
    background: #eef4ff;
    font-family: Arial, sans-serif;
    padding: 24px;
  }
  .card {
    width: 100%;
    max-width: 620px;
    background: #fff;
    border-radius: 16px;
    box-shadow: 0 10px 30px rgba(15, 23, 42, 0.12);
    padding: 32px;
    display: flex;
    flex-direction: column;
    gap: 20px;
  }
  .head { display: flex; flex-direction: column; gap: 8px; }
  .eyebrow {
    color: #2563eb;
    font-size: 11px;
    font-weight: 700;
    letter-spacing: 0.08em;
    text-transform: uppercase;
  }
  h1 { margin: 0; font-size: 24px; color: #111827; }
  .muted { margin: 0; color: #667085; font-size: 14px; line-height: 1.45; }
  .muted strong { color: #344054; }
  .small { font-size: 12px; }
  .status { margin: 0; color: #667085; font-size: 14px; }
  .options { display: flex; flex-direction: column; gap: 12px; }
  .option {
    display: flex;
    align-items: center;
    gap: 14px;
    width: 100%;
    text-align: left;
    padding: 16px;
    border: 1px solid #d8e0ec;
    border-radius: 12px;
    background: #fff;
    cursor: pointer;
    transition: border-color 0.16s ease, box-shadow 0.16s ease, transform 0.16s ease;
  }
  .option:hover:not(:disabled) {
    border-color: #2563eb;
    box-shadow: 0 6px 16px rgba(37, 99, 235, 0.12);
    transform: translateY(-1px);
  }
  .option.selected { border-color: #2563eb; background: #f5f8ff; }
  .option:disabled { cursor: progress; opacity: 0.75; }
  .option-icon {
    flex: 0 0 42px;
    width: 42px;
    height: 42px;
    display: grid;
    place-items: center;
    border-radius: 11px;
    background: #eff5ff;
    font-size: 20px;
  }
  .option-copy { display: flex; flex-direction: column; gap: 4px; flex: 1; min-width: 0; }
  .option-copy strong { color: #111827; font-size: 15px; }
  .option-copy small { color: #667085; font-size: 13px; line-height: 1.4; }
  .option-arrow { color: #2563eb; font-size: 18px; }
  .err {
    padding: 12px 14px;
    border-radius: 10px;
    background: #fef3f2;
    border: 1px solid #fecdca;
    color: #b42318;
    font-size: 13px;
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 12px;
  }
  .retry, .quiet {
    border: 0;
    background: transparent;
    color: #2563eb;
    font-size: 13px;
    font-weight: 600;
    cursor: pointer;
    padding: 4px 0;
  }
  .retry { color: #b42318; text-decoration: underline; }
  .foot {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 12px;
    border-top: 1px solid #eef0f4;
    padding-top: 16px;
  }
  button:focus-visible { outline: 2px solid #2563eb; outline-offset: 2px; }
`];

/**
 * Промежуточный экран после успешной 2FA.
 * Показывается, когда у пользователя несколько ролей: бэкенд сообщает об этом
 * в GET /api/auth/session, а выбранная роль фиксируется в POST /api/auth/session/role.
 */
@Component({
    selector: 'app-role-select',
    standalone: true,
    template: `
    <section class="page">
      <div class="card">
        <header class="head">
          <span class="eyebrow">Безопасный вход</span>
          <h1>Выберите рабочее пространство</h1>
          @if (login) {
            <p class="muted">
              Вы вошли как <strong>{{ login }}</strong>. Учётная запись имеет несколько ролей —
              выберите активную для текущей сессии.
            </p>
          }
        </header>

        @if (loading) {
          <p class="status">Загрузка доступных ролей…</p>
        }

        @if (error) {
          <div class="err" role="alert">
            {{ error }}
            <button type="button" class="retry" (click)="load()" [disabled]="busy">Повторить</button>
          </div>
        }

        @if (!loading) {
          <div class="options">
            @for (option of options; track option.role) {
              <button
                type="button"
                class="option"
                [class.selected]="selectedRole === option.role"
                [attr.aria-pressed]="selectedRole === option.role"
                [disabled]="busy"
                (click)="choose(option.role)">
                <span class="option-icon" aria-hidden="true">{{ option.icon }}</span>
                <span class="option-copy">
                  <strong>{{ option.title }}</strong>
                  <small>{{ option.description }}</small>
                </span>
                <span class="option-arrow" aria-hidden="true">
                  {{ busy && selectedRole === option.role ? '…' : '→' }}
                </span>
              </button>
            } @empty {
              @if (!error) {
                <p class="status">Для этой учётной записи не настроены доступные роли.</p>
              }
            }
          </div>
        }

        <footer class="foot">
          <span class="muted small">Роль можно будет сменить при следующем входе.</span>
          <button type="button" class="quiet" (click)="logout()" [disabled]="busy">Выйти</button>
        </footer>
      </div>
    </section>
  `,
    styles,
})
export class RoleSelectComponent implements OnInit {
    private readonly auth = inject(AuthService);
    private readonly router = inject(Router);
    private readonly cdr = inject(ChangeDetectorRef);

    loading = true;
    busy = false;
    error = '';
    session: SessionResponse | null = null;
    selectedRole: UserRole | null = null;

    get login(): string {
        return this.session?.login ?? this.auth.getLogin();
    }

    get options(): WorkspaceOption[] {
        return workspaceOptionsFor(this.session?.roles ?? []);
    }

    ngOnInit(): void {
        this.load();
    }

    load(): void {
        this.loading = true;
        this.error = '';

        this.auth.loadSession().subscribe({
            next: (session) => {
                this.session = session;
                this.loading = false;

                if (!session.requiresRoleSelection) {
                    this.router.navigateByUrl(
                        this.auth.homeUrlForRole(session.activeRole ?? session.roles[0]),
                    );
                    return;
                }

                this.cdr.detectChanges();
            },
            error: (err) => {
                this.loading = false;
                this.error = messageFrom(err, 'Не удалось загрузить доступные роли.');
                this.cdr.detectChanges();
            },
        });
    }

    choose(role: UserRole): void {
        if (this.busy) return;

        this.busy = true;
        this.selectedRole = role;
        this.error = '';

        this.auth.selectRole(role).subscribe({
            next: () => {
                this.busy = false;
                this.router.navigateByUrl(this.auth.homeUrlForRole(role));
            },
            error: (err) => {
                this.busy = false;
                this.selectedRole = null;
                this.error = messageFrom(err, 'Не удалось выбрать роль. Повторите попытку.');
                this.cdr.detectChanges();
            },
        });
    }

    logout(): void {
        this.auth.logout();
        this.router.navigateByUrl('/login');
    }
}

function messageFrom(error: unknown, fallback: string): string {
    if (typeof error === 'object' && error !== null) {
        const payload = error as { error?: { message?: unknown }; message?: unknown };
        if (typeof payload.error?.message === 'string' && payload.error.message) {
            return payload.error.message;
        }
        if (typeof payload.message === 'string' && payload.message) {
            return payload.message;
        }
    }
    return fallback;
}

