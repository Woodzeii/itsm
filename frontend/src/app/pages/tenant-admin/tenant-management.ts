import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { WorkspaceService } from '../../core/workspace/workspace.service';
import { WorkspaceApi } from '../../core/workspace/workspace-api';
import { HttpWorkspaceApiService } from '../../core/workspace/http-workspace-api.service';
import { TenantRecord } from '../../core/workspace/workspace.models';

@Component({
    selector: 'app-tenant-management',
    standalone: true,
    imports: [CommonModule, FormsModule],
    providers: [
        WorkspaceService,
        { provide: WorkspaceApi, useClass: HttpWorkspaceApiService },
    ],
    template: `
    <section class="page">
      <header>
        <p class="eyebrow">SaaS · Управление организациями</p>
        <h1>Тенанты</h1>
        <p class="intro">Управление учетными записями организаций, лимитами и блокировками.</p>
      </header>
      @if (error) { <p class="message error">{{ error }}</p> }
      @if (notice) { <p class="message success">{{ notice }}</p> }
      <form class="create" (ngSubmit)="create()">
        <h2>Создать тенант и первого администратора</h2>
        <label>Название организации<input name="name" [(ngModel)]="name" required maxlength="255" /></label>
        <label>Логин администратора<input name="adminLogin" [(ngModel)]="adminLogin" required minlength="3" maxlength="64" /></label>
        <label>Email администратора<input name="adminEmail" [(ngModel)]="adminEmail" type="email" required maxlength="255" /></label>
        <label>ФИО администратора<input name="adminFullName" [(ngModel)]="adminFullName" required maxlength="255" /></label>
        <label>Начальный пароль<input name="adminPassword" [(ngModel)]="adminPassword" type="password" required minlength="8" autocomplete="new-password" /></label>
        <button class="primary" type="submit" [disabled]="saving || !name.trim() || !adminLogin.trim() || !adminEmail.trim() || !adminFullName.trim() || adminPassword.length < 8">
          {{ saving ? 'Сохранение…' : 'Создать тенант' }}
        </button>
      </form>

      @if (editingTenant) {
        <form class="edit" (ngSubmit)="saveEdit()">
          <h2>Редактирование: {{ editingTenant.name }}</h2>
          <label>Название<input name="editName" [(ngModel)]="editName" required maxlength="255" /></label>
          <label>Лимит пользователей<input name="editMaxUsers" [(ngModel)]="editMaxUsers" type="number" min="1" required /></label>
          <label>Лимит активов<input name="editMaxAssets" [(ngModel)]="editMaxAssets" type="number" min="1" required /></label>
          <label>Лимит тикетов<input name="editMaxTickets" [(ngModel)]="editMaxTickets" type="number" min="1" required /></label>
          <label>Лимит хранилища, байт<input name="editMaxStorageBytes" [(ngModel)]="editMaxStorageBytes" type="number" min="1" required /></label>
          <div class="actions">
            <button class="primary" type="submit" [disabled]="saving || !editName.trim()">Сохранить</button>
            <button class="quiet" type="button" (click)="cancelEdit()">Отмена</button>
          </div>
        </form>
      }

      <section class="panel">
        <header><div><h2>Организации</h2><p>{{ tenants.length }} тенантов</p></div></header>
        <div class="table-scroll"><table>
          <thead><tr><th>Организация</th><th>Статус</th><th>Пользователи</th><th>Активы</th><th>Управление</th></tr></thead>
          <tbody>
            @for (tenant of tenants; track tenant.id) {
              <tr>
                <td><strong>{{ tenant.name }}</strong><small>#{{ tenant.id }}</small></td>
                <td><span class="status" [class.blocked]="tenant.isBlocked || !tenant.isActive">{{ tenant.isBlocked ? 'Заблокирован' : (tenant.isActive ? 'Активен' : 'Неактивен') }}</span></td>
                <td><div class="usage">{{ tenant.currentUserCount }} / {{ tenant.maxUsers }}<div class="bar"><div class="fill" [style.width.%]="tenant.usersPercentage"></div></div></div></td>
                <td><div class="usage">{{ tenant.currentAssetCount }} / {{ tenant.maxAssets }}<div class="bar"><div class="fill" [style.width.%]="tenant.assetsPercentage"></div></div></div></td>
                <td><div class="actions">
                  <button class="quiet" type="button" (click)="edit(tenant)">Редактировать</button>
                  @if (!tenant.isBlocked) {
                    <button class="quiet" type="button" (click)="block(tenant)">Заблокировать</button>
                  } @else {
                    <button class="quiet" type="button" (click)="unblock(tenant)">Разблокировать</button>
                  }
                  <button class="danger" type="button" (click)="remove(tenant)">Удалить</button>
                </div></td>
              </tr>
            }
            @if (!tenants.length && !loading) { <tr><td colspan="5" class="empty">Тенанты не созданы</td></tr> }
            @if (loading) { <tr><td colspan="5" class="empty">Загрузка…</td></tr> }
          </tbody>
        </table></div>
      </section>
    </section>
  `,
    styles: [`
      :host { display:block; color:#202734; } .page { max-width:1200px; margin:auto; padding:28px 30px 40px; }
      .eyebrow { margin:0 0 5px; color:#8792a0; font-size:10px; } h1 { margin:0; font-size:24px; }
      .intro { color:#778392; font-size:11px; } .panel,.create,.edit { border:1px solid #e2e7ed; border-radius:7px; background:#fff; }
      .create,.edit { display:grid; grid-template-columns:repeat(3,minmax(0,1fr)); align-items:end; gap:12px; margin:20px 0 14px; padding:16px; }
      .create h2,.edit h2 { grid-column:1/-1; margin:0; font-size:13px; } label { display:grid; gap:5px; color:#657181; font-size:10px; }
      input { min-height:36px; padding:7px 9px; border:1px solid #dfe4ea; border-radius:5px; font:inherit; font-size:11px; }
      .primary,.quiet,.danger { min-height:34px; padding:0 10px; border:0; border-radius:4px; cursor:pointer; font:inherit; font-size:9px; font-weight:650; white-space:nowrap; }
      .primary { background:#2563eb; color:#fff; } .quiet { border:1px solid #dfe4ea; background:#fff; color:#536173; } .danger { background:#fff0ef; color:#ae4545; }
      button:disabled { opacity:.55; cursor:default; }
      .panel { overflow:hidden; } .panel header { padding:14px 16px; border-bottom:1px solid #edf0f4; } .panel header h2 { margin:0; font-size:13px; } .panel header p { margin:4px 0 0; color:#8993a1; font-size:9px; }
      .table-scroll { overflow:auto; } table { width:100%; min-width:800px; border-collapse:collapse; text-align:left; }
      th,td { padding:12px; border-bottom:1px solid #eff1f4; font-size:10px; } th { color:#8993a1; font-size:9px; text-transform:uppercase; }
      td { color:#526070; } td strong,td small { display:block; } td strong { color:#394352; } td small { margin-top:4px; color:#8993a1; font-size:9px; }
      .status { padding:4px 7px; border-radius:4px; background:#edf8f2; color:#237957; font-size:9px; } .status.blocked { background:#fff0ef; color:#ae4545; }
      .usage { font-size:9px; } .bar { height:4px; background:#edf0f4; border-radius:2px; margin-top:4px; overflow:hidden; } .bar .fill { height:100%; background:#10b981; }
      .actions { display:flex; gap:6px; } .empty { color:#8993a1; text-align:center; } .message { padding:9px 12px; border-radius:4px; font-size:10px; } .error { color:#a93232; background:#fff0ef; } .success { color:#176f52; background:#edf8f2; }
      @media(max-width:900px) { .page { padding:20px 13px 30px; } .create,.edit { grid-template-columns:1fr; } .create h2,.edit h2 { grid-column:auto; } .actions { flex-wrap:wrap; } }
    `],
})
export class TenantManagementComponent implements OnInit {
    private readonly workspace = inject(WorkspaceService);
    tenants: TenantRecord[] = [];
    name = '';
    adminLogin = '';
    adminEmail = '';
    adminFullName = '';
    adminPassword = '';
    error = '';
    notice = '';
    loading = false;
    saving = false;
    editingTenant: TenantRecord | null = null;
    editName = '';
    editMaxUsers = 0;
    editMaxAssets = 0;
    editMaxTickets = 0;
    editMaxStorageBytes = 0;

    ngOnInit(): void { this.load(); }

    create(): void {
        this.error = '';
        this.notice = '';
        this.saving = true;
        this.workspace.createTenant({
            name: this.name.trim(),
            firstAdminLogin: this.adminLogin.trim(),
            firstAdminEmail: this.adminEmail.trim(),
            firstAdminFullName: this.adminFullName.trim(),
            firstAdminPassword: this.adminPassword,
        }).subscribe({
            next: (tenant: TenantRecord) => {
                this.tenants = [tenant, ...this.tenants];
                this.name = '';
                this.adminLogin = '';
                this.adminEmail = '';
                this.adminFullName = '';
                this.adminPassword = '';
                this.notice = `Тенант «${tenant.name}» и первый администратор созданы.`;
                this.saving = false;
            },
            error: (err: unknown) => {
                this.error = this.getErrorMessage(err);
                this.saving = false;
            },
        });
    }

    edit(tenant: TenantRecord): void {
        this.editingTenant = tenant;
        this.editName = tenant.name;
        this.editMaxUsers = tenant.maxUsers;
        this.editMaxAssets = tenant.maxAssets;
        this.editMaxTickets = tenant.maxTickets;
        this.editMaxStorageBytes = tenant.maxStorageBytes;
        this.error = '';
        this.notice = '';
    }

    saveEdit(): void {
        if (!this.editingTenant) return;
        this.error = '';
        this.notice = '';
        this.saving = true;
        this.workspace.updateTenant(this.editingTenant.id, {
            name: this.editName.trim(),
            maxUsers: this.editMaxUsers,
            maxAssets: this.editMaxAssets,
            maxTickets: this.editMaxTickets,
            maxStorageBytes: this.editMaxStorageBytes,
        }).subscribe({
            next: (updated: TenantRecord) => {
                this.tenants = this.tenants.map(item => item.id === updated.id ? updated : item);
                this.notice = `Настройки тенанта «${updated.name}» сохранены.`;
                this.editingTenant = null;
                this.saving = false;
            },
            error: (err: unknown) => {
                this.error = this.getErrorMessage(err);
                this.saving = false;
            },
        });
    }

    cancelEdit(): void { this.editingTenant = null; }

    block(tenant: TenantRecord): void {
        const reason = prompt('Причина блокировки:');
        if (reason === null) return;

        this.error = '';
        this.notice = '';
        this.workspace.blockTenant(tenant.id, { reason: reason.trim() }).subscribe({
            next: (updated: TenantRecord) => {
                this.tenants = this.tenants.map(item => item.id === updated.id ? updated : item);
                this.notice = 'Тенант заблокирован.';
            },
            error: (err: unknown) => this.error = this.getErrorMessage(err),
        });
    }

    unblock(tenant: TenantRecord): void {
        this.error = '';
        this.notice = '';
        this.workspace.unblockTenant(tenant.id).subscribe({
            next: (updated: TenantRecord) => {
                this.tenants = this.tenants.map(item => item.id === updated.id ? updated : item);
                this.notice = 'Тенант разблокирован.';
            },
            error: (err: unknown) => this.error = this.getErrorMessage(err),
        });
    }

    remove(tenant: TenantRecord): void {
        if (!confirm(`Удалить тенант «${tenant.name}»?`)) return;
        this.error = '';
        this.notice = '';
        this.workspace.deleteTenant(tenant.id).subscribe({
            next: () => {
                this.tenants = this.tenants.filter(item => item.id !== tenant.id);
                this.notice = 'Тенант удалён.';
            },
            error: (err: unknown) => this.error = this.getErrorMessage(err),
        });
    }

    private load(): void {
        this.loading = true;
        this.workspace.getTenants().subscribe({
            next: (tenants: TenantRecord[]) => {
                this.tenants = tenants;
                this.loading = false;
            },
            error: (err: unknown) => {
                this.error = this.getErrorMessage(err);
                this.loading = false;
            },
        });
    }

    private getErrorMessage(error: unknown): string {
        if (error instanceof HttpErrorResponse) {
            const body = error.error as { message?: unknown } | null;
            if (body && typeof body.message === 'string') return body.message;
        }
        return error instanceof Error ? error.message : 'Не удалось выполнить запрос.';
    }
}
