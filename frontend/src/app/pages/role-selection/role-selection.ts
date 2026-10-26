import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService, UserRole } from '../../core/auth/auth.service';

@Component({
    selector: 'app-role-selection',
    standalone: true,
    template: `
    <section class="page">
      <div class="card">
        <h1>Выберите роль сеанса</h1>
        <p>Доступные интерфейсы определяют, какие разделы можно открыть.</p>

        <div class="roles" role="radiogroup" aria-label="Активная роль">
          @for (role of roles; track role) {
            <label class="role" [class.selected]="selectedRole === role">
              <input type="radio" name="active-role" [checked]="selectedRole === role" (change)="selectedRole = role" />
              <span><strong>{{ roleLabels[role] }}</strong><small>{{ roleDescriptions[role] }}</small></span>
            </label>
          }
        </div>

        @if (error) { <p class="error" role="alert">{{ error }}</p> }
        <button type="button" [disabled]="!selectedRole" (click)="confirm()">Продолжить</button>
      </div>
    </section>
  `,
    styles: [`
      :host { display:block; min-height:100vh; background:#eef2f7; color:#202734; }
      .page { min-height:100vh; display:grid; place-items:center; padding:24px; }
      .card { width:min(520px,100%); box-sizing:border-box; padding:32px; border:1px solid #e1e6ed; border-radius:10px; background:#fff; box-shadow:0 18px 50px #1f293708; }
      h1 { margin:0; font-size:24px; }
      p { margin:8px 0 20px; color:#778292; font-size:12px; line-height:1.55; }
      .roles { display:grid; gap:8px; }
      .role { display:flex; align-items:flex-start; gap:10px; padding:13px; border:1px solid #dde3ea; border-radius:7px; cursor:pointer; }
      .role.selected { border-color:#78a7e9; background:#f5f9ff; }
      .role input { margin:2px 0 0; accent-color:#2864bf; }
      .role span { display:grid; gap:4px; }
      .role strong { color:#3a4554; font-size:12px; }
      .role small { color:#8792a0; font-size:10px; }
      button { width:100%; min-height:38px; margin-top:18px; border:0; border-radius:5px; background:#2563eb; color:#fff; font-size:12px; font-weight:650; cursor:pointer; }
      button:disabled { opacity:.55; cursor:not-allowed; }
      .error { color:#ad3f38; }
    `],
})
export class RoleSelectionComponent {
    private readonly auth = inject(AuthService);
    private readonly router = inject(Router);

    readonly roles = this.auth.getRoles();
    readonly roleLabels: Record<UserRole, string> = {
        admin: 'Администратор',
        agent: 'Инженер технической поддержки',
        manager: 'Руководитель',
        portal_user: 'Пользователь портала',
        tenant_admin: 'Администратор тенанта',
    };
    readonly roleDescriptions: Record<UserRole, string> = {
        admin: 'Администрирование пользователей, справочников и настроек',
        agent: 'Рабочее пространство заявок, активов и эскалаций',
        manager: 'Управление заявками и командой',
        portal_user: 'Просмотр и создание собственных заявок',
        tenant_admin: 'Управление тенантами',
    };

    selectedRole: UserRole | null = this.roles.length === 1 ? this.roles[0] : null;
    error = '';

    confirm(): void {
        if (!this.selectedRole) return;
        if (!this.auth.completeRoleSelection(this.selectedRole)) {
            this.error = 'Выбранная роль недоступна для этой сессии.';
            return;
        }
        void this.router.navigateByUrl(this.destinationFor(this.selectedRole));
    }

    private destinationFor(role: UserRole): string {
        if (role === 'tenant_admin') return '/admin/tenants';
        if (role === 'portal_user') return '/portal';
        return '/dashboard';
    }
}
