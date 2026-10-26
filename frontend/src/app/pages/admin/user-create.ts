import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CreateUserRequest, SystemRoleOption, UserRecord } from '../../core/workspace/workspace.models';
import { WorkspaceService } from '../../core/workspace/workspace.service';

@Component({
    selector: 'app-user-create',
    standalone: true,
    imports: [CommonModule, FormsModule],
    template: `
    <section class="page">
      <header class="heading">
        <div><p class="eyebrow">Администрирование</p><h1>Новый пользователь</h1><p class="intro">Создайте учётную запись и назначьте одну или несколько ролей.</p></div>
      </header>

      @if (error) { <p class="feedback error" role="alert">{{ error }}</p> }
      @if (notice) { <p class="feedback success" role="status">{{ notice }}</p> }

      <form class="user-form" (ngSubmit)="createUser()">
        <section class="panel">
          <header><span class="step">01</span><div><h2>Основные данные</h2><p>Контактные и организационные сведения</p></div></header>
          <div class="field-grid">
            <label class="wide">ФИО<input name="fullName" [(ngModel)]="form.fullName" autocomplete="name" required /></label>
            <label>Email (логин)<input name="email" type="email" [(ngModel)]="form.email" autocomplete="email" required /></label>
            <label>Username<input name="username" [(ngModel)]="form.username" autocomplete="username" required /></label>
            <label>Департамент<input name="department" [(ngModel)]="form.department" required /></label>
            <label>Руководитель
              <select name="managerId" [(ngModel)]="form.managerId">
                <option [ngValue]="null">Не назначен</option>
                @for (manager of managers; track manager.id) { <option [ngValue]="manager.id">{{ manager.fullName }} · {{ manager.department }}</option> }
              </select>
            </label>
          </div>
        </section>

        <section class="panel">
          <header><span class="step">02</span><div><h2>Авторизация и безопасность</h2><p>Первичный пароль и доступ к системе</p></div></header>
          <div class="security-grid">
            <label>Первичный пароль<input name="password" type="password" [(ngModel)]="form.password" autocomplete="new-password" minlength="12" required /><small>Не менее 12 символов, с заглавной буквой и спецсимволом.</small></label>
            <label class="active-toggle"><input name="isActive" type="checkbox" [(ngModel)]="isActive" /><span><strong>Учётная запись активна</strong><small>Отключите, чтобы создать пользователя заблокированным.</small></span></label>
          </div>
        </section>

        <section class="panel">
          <header><span class="step">03</span><div><h2>Назначение ролей</h2><p>Можно выбрать несколько ролей одновременно</p></div></header>
          <fieldset class="role-grid">
            <legend class="visually-hidden">Роли пользователя</legend>
            @for (role of roles; track role.id) {
              <label class="role-option">
                <input type="checkbox" [checked]="form.roleIds.includes(role.id)" (change)="toggleRole(role.id, $event)" />
                <span>{{ role.name }}</span>
              </label>
            }
          </fieldset>
          @if (roles.length && form.roleIds.length === 0) { <p class="role-hint">Выберите хотя бы одну роль.</p> }
        </section>

        <footer class="form-actions"><span>Пароль не отображается в списке пользователей.</span><button class="primary" type="submit" [disabled]="saving || roles.length === 0">{{ saving ? 'Создание…' : 'Создать пользователя' }}</button></footer>
      </form>
    </section>
  `,
    styles: [`
      :host { display:block; color:#202734; }
      .page { max-width:980px; margin:auto; padding:25px 28px 38px; }
      .heading { margin-bottom:20px; }
      .eyebrow { margin:0 0 4px; color:#8993a1; font-size:10px; }
      h1 { margin:0; font-size:23px; }
      .intro { margin:7px 0 0; color:#778292; font-size:12px; }
      .panel { margin-bottom:13px; border:1px solid #e2e7ed; border-radius:7px; background:#fff; }
      .panel > header { display:flex; align-items:center; gap:12px; padding:16px; border-bottom:1px solid #edf0f4; }
      .step { width:29px; height:29px; display:grid; place-items:center; border-radius:5px; background:#edf4ff; color:#2864bf; font-size:10px; font-weight:700; }
      h2 { margin:0; font-size:13px; }
      .panel > header p { margin:4px 0 0; color:#8993a1; font-size:10px; }
      .field-grid { display:grid; grid-template-columns:repeat(2,minmax(0,1fr)); gap:14px; padding:16px; }
      label { display:grid; gap:6px; color:#657181; font-size:10px; }
      input:not([type=checkbox]), select { width:100%; min-width:0; min-height:37px; padding:8px 10px; border:1px solid #dfe4ea; border-radius:5px; background:#fff; color:#303b49; font:inherit; font-size:12px; }
      input:focus, select:focus { outline:2px solid #8db7f3; outline-offset:1px; border-color:#4d88d9; }
      .wide { grid-column:1/-1; }
      .security-grid { display:grid; grid-template-columns:1fr 1fr; align-items:center; gap:18px; padding:16px; }
      .security-grid small { color:#8993a1; font-size:9px; line-height:1.5; }
      .active-toggle { display:flex; align-items:flex-start; gap:10px; padding:12px; border:1px solid #e6eaf0; border-radius:5px; }
      .active-toggle input, .role-option input { accent-color:#2872c8; }
      .active-toggle input { margin:2px 0 0; }
      .active-toggle span { display:grid; gap:4px; }
      .active-toggle strong { color:#414c5a; font-size:11px; font-weight:600; }
      .role-grid { display:grid; grid-template-columns:repeat(2,minmax(0,1fr)); gap:8px; margin:0; padding:16px; border:0; }
      .role-option { display:flex; align-items:center; gap:9px; min-height:42px; padding:0 11px; border:1px solid #e6eaf0; border-radius:5px; color:#414c5a; cursor:pointer; }
      .role-option:has(input:checked) { border-color:#8db7f3; background:#f5f9ff; }
      .role-option input { margin:0; }
      .role-hint { margin:0 16px 14px; color:#a34438; font-size:10px; }
      .form-actions { display:flex; align-items:center; justify-content:space-between; gap:12px; padding:4px 0; }
      .form-actions > span { color:#8993a1; font-size:10px; }
      .primary { min-height:38px; padding:0 15px; border:0; border-radius:5px; background:#2563eb; color:#fff; font-size:11px; font-weight:650; cursor:pointer; }
      .primary:disabled { opacity:.55; cursor:not-allowed; }
      .feedback { padding:10px 12px; border-radius:4px; font-size:11px; }
      .error { color:#a93232; background:#fff0ef; }
      .success { color:#176f52; background:#edf8f2; }
      .visually-hidden { position:absolute; width:1px; height:1px; padding:0; margin:-1px; overflow:hidden; clip:rect(0,0,0,0); white-space:nowrap; border:0; }
      @media(max-width:650px) {
        .page { padding:20px 13px 30px; }
        .field-grid,.security-grid,.role-grid { grid-template-columns:1fr; }
        .wide { grid-column:auto; }
        .form-actions { align-items:stretch; flex-direction:column; }
      }
    `],
})
export class UserCreateComponent implements OnInit {
    private readonly workspace = inject(WorkspaceService);

    managers: UserRecord[] = [];
    roles: SystemRoleOption[] = [];
    saving = false;
    error = '';
    notice = '';
    form = this.emptyForm();
    isActive = true;

    ngOnInit(): void {
        this.workspace.getUsers().subscribe({
            next: users => this.managers = users.filter(user => user.isActive),
            error: err => this.error = err.message || 'Не удалось загрузить список пользователей.',
        });
        this.workspace.getSystemRoles().subscribe({
            next: roles => this.roles = roles,
            error: err => this.error = err.message || 'Не удалось загрузить справочник ролей.',
        });
    }

    toggleRole(roleId: number, event: Event): void {
        const checked = (event.target as HTMLInputElement).checked;
        this.form.roleIds = checked
            ? [...new Set([...this.form.roleIds, roleId])]
            : this.form.roleIds.filter(id => id !== roleId);
    }

    createUser(): void {
        if (this.saving || !this.form.roleIds.length) return;
        this.saving = true;
        this.error = '';
        this.notice = '';

        const request: CreateUserRequest = {
            ...this.form,
            managerId: this.form.managerId,
        };

        this.workspace.createUser(request).subscribe({
            next: user => {
                this.managers = [user, ...this.managers];
                this.notice = `Пользователь ${user.fullName} создан.`;
                this.form = this.emptyForm();
                this.isActive = true;
                this.saving = false;
            },
            error: err => {
                this.error = err.message || 'Не удалось создать пользователя.';
                this.saving = false;
            },
        });
    }

    private emptyForm(): CreateUserRequest {
        return { fullName: '', email: '', username: '', password: '', department: '', managerId: null, tenantId: null, isManager: false, roleIds: [] };
    }
}