import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { WorkspaceService } from '../../core/workspace/workspace.service';
import { TenantRecord } from '../../core/workspace/workspace.models';

@Component({
    selector: 'app-tenant-management',
    standalone: true,
    imports: [CommonModule, FormsModule],
    template: `
    <section class="page">
      <header><p class="eyebrow">SaaS · Управление организациями</p><h1>Тенанты</h1><p class="intro">Управление учетными записями организаций. Данные тенантов не доступны для просмотра.</p></header>
      @if (error) { <p class="message error">{{ error }}</p> }
      @if (notice) { <p class="message success">{{ notice }}</p> }
      <form class="create" (ngSubmit)="create()"><h2>Создать тенант</h2><label>Название организации<input name="name" [(ngModel)]="name" required /></label><label>Логин первого администратора<input name="adminLogin" [(ngModel)]="adminLogin" required /></label><button class="primary" type="submit" [disabled]="!name.trim() || !adminLogin.trim()">Создать тенант</button></form>
      <section class="panel"><header><div><h2>Организации</h2><p>{{ tenants.length }} тенантов</p></div></header><div class="table-scroll"><table><thead><tr><th>Организация</th><th>Первый администратор</th><th>Состояние</th><th>Управление</th></tr></thead><tbody>
        @for (tenant of tenants; track tenant.id) { <tr><td><strong>{{ tenant.name }}</strong><small>Учётная запись {{ tenant.id }}</small></td><td>{{ tenant.firstAdminLogin }}</td><td><span class="status" [class.blocked]="!tenant.active">{{ tenant.active ? 'Активна' : 'Заблокирована' }}</span></td><td><div class="actions"><button class="quiet" type="button" (click)="setActive(tenant,!tenant.active)">{{ tenant.active ? 'Заблокировать' : 'Разблокировать' }}</button><button class="danger" type="button" (click)="remove(tenant)">Удалить</button></div></td></tr> }
        @if (!tenants.length) { <tr><td colspan="4" class="empty">Тенанты не созданы</td></tr> }
      </tbody></table></div></section>
    </section>
  `,
    styles: [`
      :host { display:block; color:#202734; } .page { max-width:1080px; margin:auto; padding:28px 30px 40px; }
      .eyebrow { margin:0 0 5px; color:#8792a0; font-size:10px; } h1 { margin:0; font-size:24px; }
      .intro { color:#778392; font-size:11px; } .panel,.create { border:1px solid #e2e7ed; border-radius:7px; background:#fff; }
      .create { display:grid; grid-template-columns:1fr 1fr auto; align-items:end; gap:12px; margin:20px 0 14px; padding:16px; }
      .create h2 { grid-column:1/-1; margin:0; font-size:13px; } label { display:grid; gap:5px; color:#657181; font-size:10px; }
      input { min-height:36px; padding:7px 9px; border:1px solid #dfe4ea; border-radius:5px; font:inherit; font-size:11px; }
      .primary,.quiet,.danger { min-height:34px; padding:0 10px; border:0; border-radius:4px; cursor:pointer; font:inherit; font-size:9px; font-weight:650; white-space:nowrap; }
      .primary { background:#2563eb; color:#fff; } .quiet { border:1px solid #dfe4ea; background:#fff; color:#536173; } .danger { background:#fff0ef; color:#ae4545; }
      .panel { overflow:hidden; } .panel header { padding:14px 16px; border-bottom:1px solid #edf0f4; } .panel header h2 { margin:0; font-size:13px; } .panel header p { margin:4px 0 0; color:#8993a1; font-size:9px; }
      .table-scroll { overflow:auto; } table { width:100%; min-width:680px; border-collapse:collapse; text-align:left; }
      th,td { padding:12px; border-bottom:1px solid #eff1f4; font-size:10px; } th { color:#8993a1; font-size:9px; text-transform:uppercase; }
      td { color:#526070; } td strong,td small { display:block; } td strong { color:#394352; } td small { margin-top:4px; color:#8993a1; font-size:9px; }
      .status { padding:4px 7px; border-radius:4px; background:#edf8f2; color:#237957; font-size:9px; } .status.blocked { background:#fff0ef; color:#ae4545; }
      .actions { display:flex; gap:6px; } .empty { color:#8993a1; text-align:center; } .message { padding:9px 12px; border-radius:4px; font-size:10px; } .error { color:#a93232; background:#fff0ef; } .success { color:#176f52; background:#edf8f2; }
      @media(max-width:700px) { .page { padding:20px 13px 30px; } .create { grid-template-columns:1fr; } .create h2 { grid-column:auto; } .actions { flex-wrap:wrap; } }
    `],
})
export class TenantManagementComponent implements OnInit {
    private readonly workspace = inject(WorkspaceService);
    tenants: TenantRecord[] = [];
    name = '';
    adminLogin = '';
    error = '';
    notice = '';

    ngOnInit(): void { this.load(); }
    create(): void {
        this.workspace.createTenant({ name: this.name.trim(), firstAdminLogin: this.adminLogin.trim() }).subscribe({ next: tenant => { this.tenants = [tenant, ...this.tenants]; this.name = ''; this.adminLogin = ''; this.notice = `Тенант «${tenant.name}» создан, учётная запись администратора подготовлена.`; }, error: err => this.error = err.message });
    }
    setActive(tenant: TenantRecord, active: boolean): void { this.workspace.setTenantActive(tenant.id, active).subscribe({ next: updated => { this.tenants = this.tenants.map(item => item.id === updated.id ? updated : item); this.notice = active ? 'Тенант разблокирован.' : 'Тенант заблокирован.'; }, error: err => this.error = err.message }); }
    remove(tenant: TenantRecord): void {
        if (!confirm(`Удалить тенант «${tenant.name}»?`)) return;
        this.workspace.deleteTenant(tenant.id).subscribe({ next: () => { this.tenants = this.tenants.filter(item => item.id !== tenant.id); this.notice = 'Тенант удалён.'; }, error: err => this.error = err.message });
    }
    private load(): void { this.workspace.getTenants().subscribe({ next: tenants => this.tenants = tenants, error: err => this.error = err.message }); }
}
