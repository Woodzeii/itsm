import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { WorkspaceService } from '../../core/workspace/workspace.service';
import { AgentGroupOption, AssignmentHistoryItem } from '../../core/workspace/workspace.models';

@Component({
    selector: 'app-assignment-history',
    standalone: true,
    imports: [CommonModule, FormsModule, RouterLink],
    template: `
    <section class="page">
      <header class="page-heading">
        <div><p class="eyebrow">Service Desk / Назначения</p><h1>История автораспределения</h1><p class="subtitle">Кому и когда система автоматически передала заявки.</p></div>
        <div class="heading-actions">
          <button class="secondary" type="button" (click)="load()" [disabled]="loading">Обновить</button>
          <a class="secondary" routerLink="/tickets">К очереди</a>
        </div>
      </header>

      @if (error) { <p class="notice error">{{ error }}</p> }

      <section class="panel groups">
        <header class="panel-header"><div><h2>Группы специализации</h2><p>Группа агентов, закреплённая за типом заявки.</p></div><span class="count">{{ groups.length }}</span></header>
        <div class="group-list">
          @for (group of groups; track group.id) {
            <article class="group"><strong>{{ group.name }}</strong><small>{{ group.code }} · участников: {{ group.memberCount }}</small></article>
          }
          @if (!groups.length) { <p class="empty">Группы пока не настроены</p> }
        </div>
      </section>

      <section class="panel">
        <header class="panel-header">
          <div><h2>Журнал назначений</h2><p>События автораспределения, включая перераспределение при деактивации.</p></div>
          <span class="count">{{ history.length }}</span>
        </header>
        <div class="tools">
          <label class="search">⌕<input type="search" [(ngModel)]="query" placeholder="Номер заявки, тема или исполнитель" /></label>
        </div>
        <div class="table-scroll">
          <table>
            <thead><tr><th>Заявка</th><th>Исполнитель</th><th>Стратегия</th><th>Инициатор</th><th>Когда</th></tr></thead>
            <tbody>
              @for (item of filtered; track item.ticketId + '-' + item.createdAt) {
                <tr>
                  <td><strong>{{ item.ticketNumber || item.ticketId }}</strong><span>{{ item.title }}</span></td>
                  <td>{{ item.newAssigneeName || 'не назначен' }}</td>
                  <td><span class="tag">{{ item.strategy }}</span></td>
                  <td>{{ item.actorName }}</td>
                  <td>{{ item.createdAt }}</td>
                </tr>
              }
              @if (!filtered.length) { <tr><td colspan="5" class="empty">Событий назначения нет</td></tr> }
            </tbody>
          </table>
        </div>
        @if (loading) { <p class="empty">Загрузка истории...</p> }
      </section>
    </section>
  `,
    styles: [`
      :host { display:block; color:#202734; }
      .page { max-width:1440px; margin:auto; padding:28px 32px 40px; }
      .page-heading,.panel-header { display:flex; align-items:center; justify-content:space-between; gap:16px; }
      .page-heading { margin-bottom:22px; } .eyebrow { margin:0 0 5px; color:#8b95a3; font-size:11px; }
      .subtitle { margin:6px 0 0; color:#8b95a3; font-size:12px; }
      .heading-actions { display:flex; gap:8px; }
      h1 { margin:0; font-size:24px; } h2 { margin:0; font-size:14px; }
      .panel { margin-bottom:16px; overflow:hidden; border:1px solid #e3e7ed; border-radius:8px; background:#fff; }
      .panel-header { min-height:70px; padding:15px 18px; border-bottom:1px solid #edf0f4; }
      .panel-header p { margin:4px 0 0; color:#929baa; font-size:11px; }
      .count { display:grid; min-width:27px; height:27px; place-items:center; border-radius:5px; background:#eef2ff; color:#3355b5; font-size:11px; font-weight:700; }
      .group-list { display:flex; flex-wrap:wrap; gap:10px; padding:14px 18px; }
      .group { display:grid; gap:4px; min-width:190px; padding:10px 12px; border-radius:6px; background:#f7f9fc; }
      .group strong { font-size:12px; } .group small { color:#8b95a3; font-size:10px; }
      .tools { padding:12px 16px; border-bottom:1px solid #edf0f4; }
      .search { height:36px; display:flex; align-items:center; gap:8px; padding:0 10px; border:1px solid #dfe4eb; border-radius:5px; background:#f8f9fb; color:#8390a0; font-size:19px; }
      .search input { width:100%; border:0; outline:0; background:transparent; font-size:12px; }
      .table-scroll { overflow-x:auto; } table { width:100%; border-collapse:collapse; text-align:left; }
      th { padding:10px 12px; border-bottom:1px solid #edf0f4; color:#9099a6; font-size:10px; text-transform:uppercase; }
      td { padding:11px 12px; border-bottom:1px solid #f0f2f5; color:#5c6673; font-size:11px; }
      td strong { display:block; color:#2f6fd0; } td span { color:#394352; }
      .tag { padding:3px 7px; border-radius:4px; background:#f1f3f6; color:#687385; font-size:10px; }
      .empty,.notice { padding:16px; color:#778291; font-size:12px; } .error { color:#a93232; background:#fff2f1; }
      .secondary { min-height:34px; padding:0 10px; border:1px solid #dfe4ea; border-radius:5px; background:#fff; color:#566171; font:inherit; font-size:11px; cursor:pointer; text-decoration:none; display:inline-flex; align-items:center; }
    `],
})
export class AssignmentHistoryComponent implements OnInit {
    private readonly workspace = inject(WorkspaceService);
    private readonly changeDetector = inject(ChangeDetectorRef);

    history: AssignmentHistoryItem[] = [];
    groups: AgentGroupOption[] = [];
    query = '';
    loading = true;
    error = '';

    get filtered(): AssignmentHistoryItem[] {
        const query = this.query.trim().toLocaleLowerCase();
        return this.history.filter(item =>
            !query || `${item.ticketNumber} ${item.title} ${item.newAssigneeName ?? ''}`.toLocaleLowerCase().includes(query));
    }

    ngOnInit(): void {
        this.load();
    }

    load(): void {
        this.loading = true;
        this.error = '';
        this.workspace.getAssignmentHistory().subscribe({
            next: items => {
                this.history = items;
                this.loading = false;
                this.changeDetector.markForCheck();
            },
            error: err => {
                this.error = err?.error?.detail ?? err.message ?? 'Не удалось загрузить историю назначений.';
                this.loading = false;
                this.changeDetector.markForCheck();
            },
        });
        this.workspace.getAgentGroups().subscribe({
            next: groups => {
                this.groups = groups;
                this.changeDetector.markForCheck();
            },
            error: () => this.groups = [],
        });
    }
}
