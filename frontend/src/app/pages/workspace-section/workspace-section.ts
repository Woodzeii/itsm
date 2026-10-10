import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { WorkspaceService } from '../../core/workspace/workspace.service';
import { Escalation } from '../../core/workspace/workspace.models';

@Component({
    selector: 'app-workspace-section',
    standalone: true,
    imports: [CommonModule, FormsModule, RouterLink],
    template: `
    <section class="page">
      <header class="page-heading">
        <div><p class="eyebrow">Service Desk / SLA</p><h1>Эскалации</h1></div>
      </header>

      @if (error) { <p class="notice error">{{ error }}</p> }
      @if (success) { <p class="notice success">{{ success }}</p> }
      <section class="panel"><header class="panel-header"><div><h2>Активные эскалации</h2><p>Заявки, требующие внимания руководителя</p></div><span class="count">{{ escalations.length }}</span></header>
          <div class="tools"><label class="search">⌕<input type="search" [(ngModel)]="query" placeholder="Номер, тема или команда" /></label><select aria-label="Фильтр по приоритету" [(ngModel)]="selectedPriority"><option value="">Все приоритеты</option>@for (priority of escalationPriorities; track priority) { <option [value]="priority">{{ priority }}</option> }</select></div>
          <div class="escalation-list">@for (item of filteredEscalations; track item.id) {
            <article class="escalation"><span class="alert-icon" aria-hidden="true">!</span><div class="escalation-copy"><strong>{{ item.id }} <span>{{ item.title }}</span></strong><small>{{ item.team }} · SLA осталось {{ item.slaRemaining }}</small></div><span class="tag" [class.critical]="item.priority === 'Критический'" [class.high]="item.priority === 'Высокий'">{{ item.priority }}</span><button class="escalate-button" type="button" (click)="triggerEscalation(item,$event)">Эскалировать</button><a routerLink="/tickets" [queryParams]="{ q: item.id }">Заявка <span aria-hidden="true">→</span></a></article>
          } @if (!filteredEscalations.length) { <p class="empty">Эскалации не найдены</p> }</div>
        </section>

      @if (loading) { <p class="notice">Загрузка данных...</p> }
    </section>
  `,
    styles: [`
      :host { display:block; color:#202734; }
      .page { max-width:1440px; margin:auto; padding:28px 32px 40px; }
      .page-heading,.panel-header { display:flex; align-items:center; justify-content:space-between; gap:16px; }
      .page-heading { margin-bottom:22px; } .eyebrow { margin:0 0 5px; color:#8b95a3; font-size:11px; }
      h1 { margin:0; font-size:24px; } h2 { margin:0; font-size:14px; }
      .panel { overflow:hidden; border:1px solid #e3e7ed; border-radius:8px; background:#fff; }
      .panel-header { min-height:70px; padding:15px 18px; border-bottom:1px solid #edf0f4; }
      .panel-header p { margin:4px 0 0; color:#929baa; font-size:11px; }
      .tools input,.tools select { height:36px; min-width:0; padding:0 10px; border:1px solid #dfe4eb; border-radius:5px; background:#fff; color:#394252; font:inherit; font-size:11px; }
      .tools { display:flex; gap:10px; padding:12px 16px; border-bottom:1px solid #edf0f4; }
      .search { min-width:0; height:36px; flex:1; display:flex; align-items:center; gap:8px; padding:0 10px; border:1px solid #dfe4eb; border-radius:5px; background:#f8f9fb; color:#8390a0; font-size:19px; }
      .search input { width:100%; padding:0; border:0; outline:0; background:transparent; }
      .tools select { min-width:145px; }
      .table-scroll { overflow-x:auto; }
      .tag { display:inline-flex; padding:4px 7px; border-radius:4px; background:#f1f3f6; color:#687385; font-size:10px; white-space:nowrap; }
      .tag.critical { background:#fff0f0; color:#c84c4c; } .tag.high { background:#fff2e8; color:#c66c30; } .tag.attention { background:#fff7e8; color:#a97a18; }
      .empty,.notice { padding:16px; color:#778291; font-size:12px; } .error { color:#a93232; background:#fff2f1; } .success { color:#176f52; background:#edf8f2; }
      .escalation > a { color:#3475d1; font-size:10px; font-weight:600; text-decoration:none; white-space:nowrap; }
      .count { display:grid; min-width:27px; height:27px; place-items:center; border-radius:5px; background:#fff0f0; color:#be4545; font-size:11px; font-weight:700; }
      .escalation-list { padding:4px 16px; }
      .escalation { display:flex; align-items:center; gap:13px; min-height:70px; border-bottom:1px solid #f0f2f5; }
      .escalation:last-child { border-bottom:0; }
      .alert-icon { width:30px; height:30px; flex:0 0 30px; display:grid; place-items:center; border-radius:6px; background:#fff0f0; color:#c84c4c; font-weight:700; }
      .escalation-copy { min-width:0; flex:1; } .escalation-copy strong { color:#3270cb; font-size:10px; }
      .escalation-copy strong span { margin-left:7px; color:#394252; font-weight:600; }
      .escalation-copy small { display:block; margin-top:5px; color:#939dab; font-size:10px; }
      .escalate-button { padding:5px 7px; border:1px solid #f0d7d7; border-radius:4px; background:#fff; color:#b34a4a; font-size:9px; cursor:pointer; white-space:nowrap; }
      @media(max-width:560px) { .page-heading { align-items:flex-start; flex-direction:column; } .tools { flex-direction:column; } .tools select { width:100%; } .escalation { align-items:flex-start; flex-wrap:wrap; padding:12px 0; } .escalation-copy { flex-basis:calc(100% - 50px); } .escalation > a { margin-left:auto; } }
    `],
})
export class WorkspaceSectionComponent implements OnInit {
    private readonly workspace = inject(WorkspaceService);
    private readonly auth = inject(AuthService);

    escalations: Escalation[] = [];
    query = '';
    selectedPriority = '';
    loading = true;
    error = '';
    success = '';
    isEngineer = false;

    get escalationPriorities(): string[] { return [...new Set(this.escalations.map(item => item.priority))]; }
    get filteredEscalations(): Escalation[] {
        const query = this.query.toLocaleLowerCase();
        return this.escalations.filter(item => (!this.selectedPriority || item.priority === this.selectedPriority) && `${item.id} ${item.title} ${item.team}`.toLocaleLowerCase().includes(query));
    }

    ngOnInit(): void {
      this.isEngineer = this.auth.canRead();
      this.workspace.getEscalations().subscribe({ next: value => { this.escalations = value; this.loading = false; }, error: () => this.loadError() });
    }

    triggerEscalation(item: Escalation, event: Event): void {
      event.stopPropagation();
      this.workspace.escalateTicket(item.id, this.isEngineer ? 'Инженер ТП' : 'Автор заявки').subscribe({
        next: entry => { this.success = `Эскалация ${item.id}: ${entry.previousPriority} → ${entry.newPriority}. Уведомлены ${entry.recipients.join(', ')}.`; this.workspace.getEscalations().subscribe({ next: items => this.escalations = items }); },
        error: err => this.error = err.message,
      });
    }

    private loadError(): void {
        this.error = 'Не удалось загрузить данные раздела.';
        this.loading = false;
    }
}