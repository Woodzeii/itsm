import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { WorkspaceService } from '../../core/workspace/workspace.service';
import { AssetRecord, Escalation, ServiceCategory, Ticket } from '../../core/workspace/workspace.models';

type WorkspaceSection = 'tickets' | 'assets' | 'catalog' | 'escalations';

@Component({
    selector: 'app-workspace-section',
    standalone: true,
    imports: [CommonModule, FormsModule, RouterLink],
    template: `
    <section class="page">
      <header class="page-heading">
        <div><p class="eyebrow">Рабочее пространство / Service Desk</p><h1>{{ title }}</h1></div>
        @if (section === 'tickets') { <button class="primary-button" type="button" (click)="showCreateForm = !showCreateForm"><span aria-hidden="true">+</span> Новая заявка</button> }
      </header>

      @if (error) { <p class="notice error">{{ error }}</p> }
      @if (success) { <p class="notice success">{{ success }}</p> }
      @if (showCreateForm) {
        <form class="create-form" (ngSubmit)="createTicket()">
          <label>Тема заявки<input name="title" [(ngModel)]="newTitle" required /></label>
          <label>Заявитель<input name="requester" [(ngModel)]="newRequester" required /></label>
          <label>Категория<input name="category" [(ngModel)]="newCategory" /></label>
          <button class="primary-button" type="submit" [disabled]="!newTitle.trim() || !newRequester.trim()">Создать</button>
        </form>
      }

      @if (section === 'tickets') {
        <section class="panel">
          <header class="panel-header"><div><h2>Очередь заявок</h2><p>Обращения и текущий статус выполнения</p></div></header>
          <div class="tools"><label class="search">⌕<input type="search" [(ngModel)]="query" placeholder="Номер, тема или заявитель" /></label><select aria-label="Фильтр по статусу" [(ngModel)]="selectedStatus"><option value="">Все статусы</option>@for (status of ticketStatuses; track status) { <option [value]="status">{{ status }}</option> }</select></div>
          <div class="table-scroll"><table><thead><tr><th>Заявка</th><th>Приоритет</th><th>Статус</th><th>Команда</th><th>SLA</th></tr></thead><tbody>
            @for (ticket of filteredTickets; track ticket.id) { <tr><td><strong class="linkish">{{ ticket.id }}</strong><span class="primary-text">{{ ticket.title }}</span><small>{{ ticket.requester }} · {{ ticket.createdAt }}</small></td><td><span class="tag" [class.critical]="ticket.priority === 'Критический'" [class.high]="ticket.priority === 'Высокий'">{{ ticket.priority }}</span></td><td>{{ ticket.status }}</td><td>{{ ticket.team }}</td><td>{{ ticket.slaRemaining }}</td></tr> }
            @if (!filteredTickets.length) { <tr><td colspan="5" class="empty">Заявки не найдены</td></tr> }
          </tbody></table></div>
        </section>
      }

      @if (section === 'assets') {
        <section class="panel"><header class="panel-header"><div><h2>Реестр активов</h2><p>Оборудование, программное обеспечение и инфраструктура</p></div></header>
          <div class="tools"><label class="search">⌕<input type="search" [(ngModel)]="query" placeholder="Инвентарный номер, название или владелец" /></label><select aria-label="Фильтр по типу" [(ngModel)]="selectedType"><option value="">Все типы</option>@for (type of assetTypes; track type) { <option [value]="type">{{ type }}</option> }</select></div>
          <div class="table-scroll"><table><thead><tr><th>Актив</th><th>Ответственный</th><th>Статус</th><th>Тип</th></tr></thead><tbody>
            @for (asset of filteredAssets; track asset.id) { <tr><td><strong class="linkish">{{ asset.id }}</strong><span class="primary-text">{{ asset.name }}</span></td><td>{{ asset.owner }}</td><td><span class="tag" [class.attention]="asset.status.includes('Истекает')">{{ asset.status }}</span></td><td>{{ asset.type }}</td></tr> }
            @if (!filteredAssets.length) { <tr><td colspan="4" class="empty">Активы не найдены</td></tr> }
          </tbody></table></div>
        </section>
      }

      @if (section === 'catalog') {
        <section class="catalog-intro"><p class="eyebrow">Service catalog</p><h2>Чем можем помочь?</h2><p>Выберите направление и создайте обращение.</p><label class="search"><span aria-hidden="true">⌕</span><input type="search" [(ngModel)]="query" placeholder="Поиск по каталогу услуг" /></label></section>
        <div class="category-grid">@for (category of filteredCategories; track category.id) {
          <article class="category"><div class="category-symbol" aria-hidden="true">{{ category.title.slice(0, 1) }}</div><h2>{{ category.title }}</h2><p>{{ category.description }}</p><footer><span>{{ category.serviceCount }} услуг</span><a routerLink="/tickets" [queryParams]="{ create: true, category: category.title }">Оставить заявку <span aria-hidden="true">→</span></a></footer></article>
        } @if (!filteredCategories.length) { <p class="empty">Категории не найдены</p> }</div>
      }

      @if (section === 'escalations') {
        <section class="panel"><header class="panel-header"><div><h2>Активные эскалации</h2><p>Заявки, требующие внимания руководителя</p></div><span class="count">{{ escalations.length }}</span></header>
          <div class="tools"><label class="search">⌕<input type="search" [(ngModel)]="query" placeholder="Номер, тема или команда" /></label><select aria-label="Фильтр по приоритету" [(ngModel)]="selectedPriority"><option value="">Все приоритеты</option>@for (priority of escalationPriorities; track priority) { <option [value]="priority">{{ priority }}</option> }</select></div>
          <div class="escalation-list">@for (item of filteredEscalations; track item.id) {
            <article class="escalation"><span class="alert-icon" aria-hidden="true">!</span><div class="escalation-copy"><strong>{{ item.id }} <span>{{ item.title }}</span></strong><small>{{ item.team }} · SLA осталось {{ item.slaRemaining }}</small></div><span class="tag" [class.critical]="item.priority === 'Критический'" [class.high]="item.priority === 'Высокий'">{{ item.priority }}</span><button class="escalate-button" type="button" (click)="triggerEscalation(item,$event)">Эскалировать</button><a routerLink="/tickets" [queryParams]="{ q: item.id }">Заявка <span aria-hidden="true">→</span></a></article>
          } @if (!filteredEscalations.length) { <p class="empty">Эскалации не найдены</p> }</div>
        </section>
      }

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
      .primary-button { min-height:38px; padding:0 13px; border:0; border-radius:6px; background:#2563eb; color:white; font:inherit; font-size:12px; font-weight:650; cursor:pointer; }
      .primary-button span { margin-right:5px; font-size:18px; font-weight:400; }
      .primary-button:disabled { opacity:.55; cursor:not-allowed; }
      .create-form { display:grid; grid-template-columns:2fr 1fr 1fr auto; align-items:end; gap:12px; margin-bottom:16px; padding:16px; border:1px solid #dce5f2; border-radius:8px; background:#fff; }
      .create-form label { display:grid; gap:6px; color:#6c7786; font-size:10px; }
      .create-form input,.tools input,.tools select,.catalog-intro input { height:36px; min-width:0; padding:0 10px; border:1px solid #dfe4eb; border-radius:5px; background:#fff; color:#394252; font:inherit; font-size:11px; }
      .tools { display:flex; gap:10px; padding:12px 16px; border-bottom:1px solid #edf0f4; }
      .search { min-width:0; height:36px; flex:1; display:flex; align-items:center; gap:8px; padding:0 10px; border:1px solid #dfe4eb; border-radius:5px; background:#f8f9fb; color:#8390a0; font-size:19px; }
      .search input { width:100%; padding:0; border:0; outline:0; background:transparent; }
      .tools select { min-width:145px; }
      .table-scroll { overflow-x:auto; }
      table { width:100%; min-width:680px; border-collapse:collapse; text-align:left; }
      th { padding:10px; border-bottom:1px solid #edf0f4; color:#929baa; font-size:9px; font-weight:650; text-transform:uppercase; }
      td { padding:12px 10px; border-bottom:1px solid #f0f2f5; color:#5e6876; font-size:11px; white-space:nowrap; }
      td:first-child { min-width:250px; white-space:normal; }
      .linkish { margin-right:8px; color:#3475d1; font-size:10px; }
      .primary-text { color:#394352; font-weight:600; }
      td small { display:block; margin-top:4px; color:#99a2af; font-size:10px; }
      .tag { display:inline-flex; padding:4px 7px; border-radius:4px; background:#f1f3f6; color:#687385; font-size:10px; white-space:nowrap; }
      .tag.critical { background:#fff0f0; color:#c84c4c; } .tag.high { background:#fff2e8; color:#c66c30; } .tag.attention { background:#fff7e8; color:#a97a18; }
      .empty,.notice { padding:16px; color:#778291; font-size:12px; } .error { color:#a93232; background:#fff2f1; } .success { color:#176f52; background:#edf8f2; }
      .catalog-intro { max-width:720px; margin-bottom:20px; padding:24px; border-radius:8px; background:#202936; color:#fff; }
      .catalog-intro .eyebrow { color:#89b7ff; text-transform:uppercase; } .catalog-intro h2 { margin-top:8px; font-size:23px; }
      .catalog-intro > p:not(.eyebrow) { color:#b3bdca; font-size:12px; }
      .catalog-intro .search { height:40px; margin-top:17px; border-color:#ffffff22; background:#ffffff12; color:#c1cbd7; }
      .catalog-intro input { height:38px; border:0; color:#fff; background:transparent; } .catalog-intro input::placeholder { color:#b3bdca; }
      .category-grid { display:grid; grid-template-columns:repeat(2,minmax(0,1fr)); gap:13px; }
      .category { padding:18px; border:1px solid #e3e7ed; border-radius:8px; background:white; }
      .category-symbol { width:34px; height:34px; display:grid; place-items:center; border-radius:7px; background:#eaf2ff; color:#2866c5; font-weight:700; }
      .category h2 { margin-top:15px; } .category p { min-height:34px; color:#778291; font-size:11px; line-height:1.55; }
      .category footer { display:flex; align-items:center; justify-content:space-between; gap:12px; margin-top:15px; color:#8b95a3; font-size:10px; }
      .category a,.escalation > a { color:#3475d1; font-size:10px; font-weight:600; text-decoration:none; white-space:nowrap; }
      .count { display:grid; min-width:27px; height:27px; place-items:center; border-radius:5px; background:#fff0f0; color:#be4545; font-size:11px; font-weight:700; }
      .escalation-list { padding:4px 16px; }
      .escalation { display:flex; align-items:center; gap:13px; min-height:70px; border-bottom:1px solid #f0f2f5; }
      .escalation:last-child { border-bottom:0; }
      .alert-icon { width:30px; height:30px; flex:0 0 30px; display:grid; place-items:center; border-radius:6px; background:#fff0f0; color:#c84c4c; font-weight:700; }
      .escalation-copy { min-width:0; flex:1; } .escalation-copy strong { color:#3270cb; font-size:10px; }
      .escalation-copy strong span { margin-left:7px; color:#394252; font-weight:600; }
      .escalation-copy small { display:block; margin-top:5px; color:#939dab; font-size:10px; }
      .escalate-button { padding:5px 7px; border:1px solid #f0d7d7; border-radius:4px; background:#fff; color:#b34a4a; font-size:9px; cursor:pointer; white-space:nowrap; }
      @media(max-width:760px) { .page { padding:22px 16px 30px; } .create-form { grid-template-columns:1fr 1fr; } }
      @media(max-width:560px) { .page-heading { align-items:flex-start; flex-direction:column; } .tools { flex-direction:column; } .tools select { width:100%; } .category-grid { grid-template-columns:1fr; } .escalation { align-items:flex-start; flex-wrap:wrap; padding:12px 0; } .escalation-copy { flex-basis:calc(100% - 50px); } .escalation > a { margin-left:auto; } .create-form { grid-template-columns:1fr; } }
    `],
})
export class WorkspaceSectionComponent implements OnInit {
    private readonly workspace = inject(WorkspaceService);
    private readonly route = inject(ActivatedRoute);
    private readonly router = inject(Router);
    private readonly auth = inject(AuthService);

    section: WorkspaceSection = 'tickets';
    tickets: Ticket[] = [];
    assets: AssetRecord[] = [];
    categories: ServiceCategory[] = [];
    escalations: Escalation[] = [];
    query = '';
    selectedStatus = '';
    selectedType = '';
    selectedPriority = '';
    loading = true;
    error = '';
    success = '';
    showCreateForm = false;
    newTitle = '';
    newRequester = '';
    newCategory = '';
    isEngineer = false;

    get title(): string {
        return { tickets: 'Заявки', assets: 'Активы', catalog: 'Каталог услуг', escalations: 'Эскалации' }[this.section];
    }
    get ticketStatuses(): string[] { return [...new Set(this.tickets.map(ticket => ticket.status))]; }
    get assetTypes(): string[] { return [...new Set(this.assets.map(asset => asset.type))]; }
    get escalationPriorities(): string[] { return [...new Set(this.escalations.map(item => item.priority))]; }
    get filteredTickets(): Ticket[] {
        const query = this.query.toLocaleLowerCase();
        return this.tickets.filter(item => (!this.selectedStatus || item.status === this.selectedStatus) && `${item.id} ${item.title} ${item.requester} ${item.team}`.toLocaleLowerCase().includes(query));
    }
    get filteredAssets(): AssetRecord[] {
        const query = this.query.toLocaleLowerCase();
        return this.assets.filter(item => (!this.selectedType || item.type === this.selectedType) && `${item.id} ${item.name} ${item.owner}`.toLocaleLowerCase().includes(query));
    }
    get filteredCategories(): ServiceCategory[] {
        const query = this.query.toLocaleLowerCase();
        return this.categories.filter(item => `${item.title} ${item.description}`.toLocaleLowerCase().includes(query));
    }
    get filteredEscalations(): Escalation[] {
        const query = this.query.toLocaleLowerCase();
        return this.escalations.filter(item => (!this.selectedPriority || item.priority === this.selectedPriority) && `${item.id} ${item.title} ${item.team}`.toLocaleLowerCase().includes(query));
    }

    ngOnInit(): void {
      this.isEngineer = this.auth.canRead();
        this.section = this.route.snapshot.data['section'] as WorkspaceSection;
        this.route.queryParamMap.subscribe(params => {
            this.query = params.get('q') ?? '';
            this.newCategory = params.get('category') ?? '';
            this.showCreateForm = params.get('create') === 'true';
        });
        const load = {
            tickets: () => this.workspace.getTickets().subscribe({ next: value => { this.tickets = value; this.loading = false; }, error: () => this.loadError() }),
            assets: () => this.workspace.getAssets().subscribe({ next: value => { this.assets = value; this.loading = false; }, error: () => this.loadError() }),
            catalog: () => this.workspace.getServiceCategories().subscribe({ next: value => { this.categories = value; this.loading = false; }, error: () => this.loadError() }),
            escalations: () => this.workspace.getEscalations().subscribe({ next: value => { this.escalations = value; this.loading = false; }, error: () => this.loadError() }),
        }[this.section];
        load();
    }

    createTicket(): void {
        this.workspace.createTicket({ title: this.newTitle.trim(), requester: this.newRequester.trim(), category: this.newCategory.trim() || undefined }).subscribe({
            next: ticket => {
                this.tickets = [ticket, ...this.tickets];
                this.success = `Заявка ${ticket.id} создана.`;
                this.showCreateForm = false;
                this.newTitle = '';
                this.newRequester = '';
                this.newCategory = '';
                void this.router.navigate([], { relativeTo: this.route, queryParams: { create: null, category: null }, queryParamsHandling: 'merge' });
            },
            error: () => this.error = 'Не удалось создать заявку.',
        });
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