import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { WorkspaceService } from '../../core/workspace/workspace.service';
import { HttpWorkspaceApiService } from '../../core/workspace/http-workspace-api.service';
import { downloadTicketAttachment as saveTicketAttachment } from '../../core/workspace/download-ticket-attachment';
import { AdminSettings, PortalTicketRequest, PortalTicketType, RequesterOption, TicketAttachment, Ticket } from '../../core/workspace/workspace.models';


@Component({
    selector: 'app-ticket-queue',
    standalone: true,
    imports: [CommonModule, FormsModule, RouterLink],
    template: `
    <section class="page">
      <header class="heading"><div><p class="eyebrow">Service Desk / {{ isManager ? 'заявки отдела' : 'единая очередь' }}</p><h1>Заявки</h1></div><div class="heading-actions"><button class="primary" type="button" (click)="openCreate()">+ Новая заявка</button><a class="secondary" routerLink="/admin/settings">Типы заявок и SLA</a></div></header>
      @if (error) { <p class="notice error">{{ error }}</p> }
      @if (notice) { <p class="notice success">{{ notice }}</p> }
      @if (showCreateForm) {
        <form class="panel create-form" (ngSubmit)="createTicket()">
          <div class="create-heading"><div><p class="eyebrow">Новая заявка</p><h2>Заполните основные данные</h2></div><button class="icon-button" type="button" aria-label="Закрыть форму" (click)="showCreateForm = false">×</button></div>
          @if (loadingTypes) { <p class="empty">Загружаем доступные типы заявок...</p> }
          @else {
            <label>Тип заявки<select name="newTicketType" required [(ngModel)]="newTicketTypeId"><option value="">Выберите тип заявки</option>@for (type of ticketTypes; track type.id) { <option [value]="type.id">{{ type.name }}</option> }</select></label>
            @if (canSelectRequester) {
              <label>Заявитель<select name="newTicketRequester" [(ngModel)]="newTicketRequesterId"><option value="">Я — автор заявки</option>@for (requester of requesters; track requester.id) { <option [value]="requester.id">{{ requester.fullName }}@if (requester.department) { · {{ requester.department }} }</option> }</select></label>
            }
            <label>Тема<input name="newTicketTitle" required maxlength="300" [(ngModel)]="newTicketTitle" placeholder="Кратко сформулируйте вопрос" /></label>
            <label>Критичность<select name="newTicketPriority" required [(ngModel)]="newTicketPriority">@for (priority of criticalities; track priority) { <option [value]="priority">{{ priority }}</option> }</select></label>
            <label class="wide">Описание<textarea name="newTicketDescription" required maxlength="10000" [(ngModel)]="newTicketDescription" placeholder="Подробно опишите ситуацию и ожидаемый результат"></textarea></label>
            <footer class="create-actions"><button class="primary" type="submit" [disabled]="isSubmitting || !newTicketTypeId || !newTicketTitle.trim() || !newTicketDescription.trim()">Создать заявку</button><button class="secondary" type="button" (click)="showCreateForm = false">Отмена</button></footer>
          }
          @if (!loadingTypes && !ticketTypes.length) { <p class="notice error">Нет доступных типов заявок.</p> }
        </form>
      }
      <div class="queue-layout">
        <section class="panel queue">
          <div class="filters">
            <label class="search">⌕<input aria-label="Поиск по общей очереди" type="search" [(ngModel)]="query" placeholder="Номер, тема или автор" /></label>
            <select aria-label="Фильтр по типу" [(ngModel)]="typeFilter"><option value="">Все типы</option>@for (type of types; track type) { <option>{{ type }}</option> }</select>
            <select aria-label="Фильтр по исполнителю" [(ngModel)]="assigneeFilter"><option value="">Все исполнители</option>@for (assignee of assignees; track assignee) { <option>{{ assignee }}</option> }</select>
            <select aria-label="Фильтр по статусу" [(ngModel)]="statusFilter"><option value="">Все статусы</option>@for (status of statuses; track status) { <option>{{ status }}</option> }</select>
            <select aria-label="Фильтр по критичности" [(ngModel)]="priorityFilter"><option value="">Вся критичность</option>@for (priority of priorities; track priority) { <option>{{ priority }}</option> }</select>
          </div>
          <div class="table-scroll"><table><thead><tr><th>Заявка</th><th>Тип</th><th>Критичность</th><th>Статус</th><th>Исполнитель</th><th>SLA реакции</th><th>SLA решения</th></tr></thead><tbody>
            @for (ticket of filteredTickets; track ticket.id) {
              <tr [class.active]="selected?.id === ticket.id" (click)="select(ticket)"><td><strong>{{ ticket.id }}</strong><span>{{ ticket.title }}</span><small>{{ ticket.requester }} · {{ ticket.createdOn || ticket.createdAt }}</small></td><td>{{ ticket.type }}</td><td><span class="tag" [class.high]="ticket.priority === 'Высокий'">{{ ticket.priority }}</span></td><td>{{ ticket.status }}</td><td>{{ ticket.assignee || 'Не назначен' }}</td><td>{{ ticket.reactionSla || '—' }}</td><td>{{ ticket.resolutionSla || ticket.slaRemaining }}</td></tr>
            }
            @if (!filteredTickets.length) { <tr><td colspan="7" class="empty">Заявок по заданным фильтрам нет</td></tr> }
          </tbody></table></div>
          <footer>{{ filteredTickets.length }} из {{ tickets.length }} заявок · {{ isManager ? 'очередь вашего отдела' : 'общая очередь' }}</footer>
        </section>

        <aside class="panel ticket-detail">
          @if (selected) {
            <header class="detail-heading"><div><p class="eyebrow">{{ selected.id }} · {{ selected.type }}</p><h2>{{ selected.title }}</h2></div><button class="icon-button" type="button" aria-label="Закрыть карточку" (click)="selected = null">×</button></header>
            <div class="ticket-facts"><div><small>Автор</small><strong>{{ selected.creatorName || selected.requester }}</strong></div>@if (selected.creatorName) { <div><small>Заявитель</small><strong>{{ selected.requester }}</strong></div> }<div><small>Исполнитель</small><strong>{{ selected.assignee || 'Не назначен' }}</strong></div><div><small>Критичность</small><strong>{{ selected.priority }}</strong></div><div><small>Статус</small><strong>{{ selected.status }}</strong></div><div><small>Реакция</small><strong>{{ selected.reactionSla || '—' }}</strong></div><div><small>Решение</small><strong>{{ selected.resolutionSla || selected.slaRemaining }}</strong></div></div>
            @if (!isManager && settings?.assignmentMode === 'manual') { <div class="assignment"><label>Исполнитель<select name="assignee" [(ngModel)]="assigneeDraft"><option value="">Выбрать инженера</option>@for (engineer of engineerOptions; track engineer) { <option [value]="engineer">{{ engineer }}</option> }</select></label><button class="secondary" type="button" [disabled]="!assigneeDraft" (click)="assign()">Назначить</button></div> }
            @if (isEngineer && selected.status !== 'Закрыта') { <div class="priority-edit"><label>Критичность<select name="priority" [(ngModel)]="priorityDraft">@for (priority of settings?.criticalities ?? []; track priority) { <option [value]="priority">{{ priority }}</option> }</select></label><button class="secondary" type="button" [disabled]="priorityDraft === selected.priority" (click)="changePriority()">Изменить</button></div> }
            @if (!isManager) { @for (nextStatus of nextStatuses; track nextStatus) { <button class="primary transition" type="button" (click)="changeStatus(nextStatus.statusId)">Перевести в «{{ nextStatus.name }}»</button> } }
            @if (!isManager && isEngineer && selected.status !== 'Закрыта') { <button class="outline escalation-action" type="button" (click)="escalate()">Повысить критичность и эскалировать</button> }
            @if (!isManager && selected.type === 'Тикет на ремонт' && selected.status !== 'Закрыта') {
              <form class="repair-close" (ngSubmit)="closeRepair()"><label>Результат ремонта<select name="result" [(ngModel)]="repairResult"><option value="В эксплуатацию">Вернуть в эксплуатацию</option><option value="На склад">Перевести на склад</option><option value="Списать">Списать</option></select></label><label>Что ремонтировалось и что менялось<textarea name="work" [(ngModel)]="repairDescription" required></textarea></label><button class="primary" type="submit" [disabled]="!repairDescription.trim()">Закрыть ремонт</button></form>
            }
            <section class="history"><h3>История заявки</h3>@if (loadingHistory) { <p class="empty">Загрузка истории...</p> } @else if (selected.history?.length) { @for (item of selected.history; track item.id) { <article><strong>{{ item.event }}</strong><small>{{ item.createdAt }} · {{ item.actor }}</small></article> } } @else { <p class="empty">История изменений пока пуста.</p> }</section>
            <section class="comments"><h3>Переписка</h3>@if (loadingComments) { <p class="empty">Загрузка сообщений...</p> } @for (comment of visibleComments; track comment.id) { <article [class.internal]="comment.internal"><strong>{{ comment.author }} @if (comment.internal) { <span>Внутренний комментарий</span> }</strong><p>{{ comment.message }}</p><small>{{ comment.createdAt }}</small>@if (comment.attachmentRecords?.length) { @for (attachment of comment.attachmentRecords; track attachment.id) { <button class="attachment" type="button" (click)="downloadAttachment(attachment)">📎 {{ attachment.fileName }}</button> } } @else { @for (file of comment.attachments; track file) { <small class="attachment">📎 {{ file }}</small> } }</article> }
              @if (!isManager) { <form (ngSubmit)="addComment()"><textarea name="message" [(ngModel)]="commentText" placeholder="Сообщение по заявке" required></textarea><label class="file-picker">Прикрепить файлы<input type="file" multiple (change)="pickAttachments($event)" /></label>@if (canUseInternalComments) { <label class="internal-toggle"><input type="checkbox" name="internal" [(ngModel)]="internalComment" /> Внутренний комментарий</label> }<button class="primary" type="submit" [disabled]="isSendingComment || !commentText.trim()">Отправить</button></form> }
            </section>
          } @else { <div class="empty-detail"><h2>Откройте заявку</h2><p>Выберите строку в общей очереди, чтобы увидеть SLA, историю и переписку.</p></div> }
        </aside>
      </div>
    </section>
  `,
    styles: [`
      :host { display:block; color:#202734; } .page { max-width:1600px; margin:auto; padding:25px 26px 36px; }
      .heading,.detail-heading { display:flex; align-items:center; justify-content:space-between; gap:14px; }
      .heading { margin-bottom:17px; } h1 { margin:0; font-size:24px; } h2 { margin:0; font-size:15px; } h3 { margin:0 0 10px; font-size:12px; }
      .heading-actions { display:flex; align-items:center; gap:8px; }
      .create-form { display:grid; grid-template-columns:repeat(2,minmax(0,1fr)); gap:12px; margin-bottom:14px; padding:16px; }
      .create-heading,.wide,.create-actions { grid-column:1/-1; }
      .create-heading { display:flex; align-items:center; justify-content:space-between; }
      .create-form label { display:grid; gap:5px; color:#697586; font-size:10px; }
      .create-form input,.create-form select,.create-form textarea { min-width:0; box-sizing:border-box; width:100%; min-height:36px; padding:7px 9px; border:1px solid #dfe4ea; border-radius:5px; background:#fff; font-size:10px; }
      .create-form textarea { min-height:72px; resize:vertical; }
      .create-actions { display:flex; gap:8px; padding-top:10px; border-top:1px solid #edf0f4; }
      .eyebrow { margin:0 0 5px; color:#8a94a2; font-size:10px; } .panel { min-width:0; border:1px solid #e1e6ec; border-radius:8px; background:#fff; }
      .queue-layout { display:grid; grid-template-columns:minmax(0,1fr) 360px; gap:14px; align-items:start; }
      .queue { overflow:hidden; } .filters { display:grid; grid-template-columns:minmax(160px,1.5fr) repeat(4,minmax(120px,1fr)); gap:8px; padding:12px; border-bottom:1px solid #edf0f4; }
      input,select,textarea,button { font:inherit; } .filters select,.search { min-width:0; height:34px; padding:0 8px; border:1px solid #e0e5eb; border-radius:5px; background:#fff; color:#596474; font-size:10px; }
      .search { display:flex; align-items:center; gap:6px; background:#f8f9fb; color:#8792a0; font-size:18px; } .search input { width:100%; min-width:0; border:0; outline:0; background:transparent; font-size:10px; }
      .table-scroll { overflow:auto; } table { width:100%; min-width:900px; border-collapse:collapse; text-align:left; }
      th { padding:9px 8px; border-bottom:1px solid #edf0f4; color:#9099a6; font-size:9px; text-transform:uppercase; }
      td { padding:10px 8px; border-bottom:1px solid #f0f2f5; color:#606b79; font-size:9px; white-space:nowrap; }
      tbody tr { cursor:pointer; } tbody tr:hover,tbody tr.active { background:#f3f7fd; } td:first-child { min-width:205px; white-space:normal; }
      td:first-child strong { margin-right:6px; color:#3475d1; } td:first-child span { color:#394352; font-weight:600; } td:first-child small { display:block; margin-top:4px; color:#9aa3b1; }
      .tag { padding:3px 6px; border-radius:4px; background:#eff2f5; font-size:9px; } .tag.high { background:#fff2e8; color:#c66c30; }
      footer { padding:10px 13px; color:#8e98a5; font-size:9px; }
      .ticket-detail { position:sticky; top:12px; max-height:calc(100vh - 102px); overflow:auto; padding:15px; }
      .detail-heading { align-items:flex-start; } .detail-heading h2 { line-height:1.4; }
      .icon-button { width:27px; height:27px; border:0; border-radius:5px; background:#f2f4f7; color:#637080; cursor:pointer; }
      .ticket-facts { display:grid; grid-template-columns:1fr 1fr; gap:10px; margin:15px 0; padding:11px; border-radius:6px; background:#f8f9fb; }
      .assignment,.priority-edit { display:flex; align-items:end; gap:8px; margin:8px 0; }
      .assignment label,.priority-edit label { min-width:0; flex:1; display:grid; gap:5px; color:#697586; font-size:9px; }
      .ticket-facts small,.ticket-facts strong { display:block; } .ticket-facts small { margin-bottom:4px; color:#9099a6; font-size:9px; } .ticket-facts strong { color:#45505e; font-size:10px; }
      .primary,.outline,.secondary { min-height:34px; padding:0 10px; border:0; border-radius:5px; cursor:pointer; font-size:10px; font-weight:650; }
      .primary { background:#2563eb; color:white; } .outline { border:1px solid #e5dada; background:#fff; color:#b04e4e; }
      .transition,.escalation-action { width:100%; margin-bottom:8px; } .secondary { border:1px solid #dfe4ea; background:#fff; color:#566171; }
      .repair-close { display:grid; gap:9px; margin:8px 0 14px; padding:10px; border:1px solid #e6eaf0; border-radius:6px; }
      label { display:grid; gap:4px; color:#6c7785; font-size:9px; } textarea,select { min-height:34px; padding:7px; border:1px solid #dfe4ea; border-radius:5px; font-size:10px; }
      textarea { resize:vertical; } .repair-close textarea { min-height:58px; }
      .history,.comments { margin-top:14px; padding-top:13px; border-top:1px solid #edf0f4; }
      .history article { display:grid; gap:3px; padding:7px 0; } .history article strong,.comments article strong { color:#4b5664; font-size:10px; }
      .history article small { color:#98a1ae; font-size:9px; }
      .comments article { margin:8px 0; padding:9px; border-radius:6px; background:#f5f8fc; }
      .comments article.internal { background:#fff8eb; } .comments article strong span { margin-left:5px; color:#a77a27; font-weight:500; }
      .comments article p { margin:5px 0 0; color:#596474; font-size:10px; line-height:1.5; white-space:pre-wrap; }
      .attachment { display:block; margin-top:5px; padding:0; border:0; background:none; color:#5579af; font-size:9px; text-align:left; cursor:pointer; }
      .comments form { display:grid; gap:8px; } .comments form textarea { min-height:64px; }
      .file-picker input { font-size:9px; } .internal-toggle { display:flex; align-items:center; gap:5px; }
      .empty,.empty-detail { padding:25px 12px; color:#8a94a2; font-size:11px; } .empty-detail { padding:75px 18px; text-align:center; } .empty-detail p { color:#8a94a2; font-size:10px; line-height:1.5; }
      .notice { padding:9px 12px; border-radius:5px; font-size:10px; } .error { background:#fff1ef; color:#a93232; } .success { background:#edf8f2; color:#176f52; }
      .secondary { color:#536173; text-decoration:none; display:inline-flex; align-items:center; }
      @media(max-width:1150px) { .queue-layout { grid-template-columns:1fr; } .ticket-detail { position:static; max-height:none; } .filters { grid-template-columns:repeat(2,minmax(0,1fr)); } }
      @media(max-width:600px) { .page { padding:20px 12px 28px; } .heading { align-items:flex-start; flex-direction:column; } .heading-actions { flex-wrap:wrap; } .create-form { grid-template-columns:1fr; } .create-heading,.wide,.create-actions { grid-column:auto; } .filters { grid-template-columns:1fr; } }
    `],
})
export class TicketQueueComponent implements OnInit {
    private readonly workspace = inject(WorkspaceService);
    private readonly ticketsApi = inject(HttpWorkspaceApiService);
    private readonly auth = inject(AuthService);
    private readonly changeDetector = inject(ChangeDetectorRef);
    private readonly route = inject(ActivatedRoute);

    tickets: Ticket[] = [];
    settings: AdminSettings | null = null;
    selected: Ticket | null = null;
    isEngineer = false;
    isManager = false;
    canUseInternalComments = false;
    loadingComments = false;
    loadingHistory = false;
    query = '';
    typeFilter = '';
    assigneeFilter = '';
    statusFilter = '';
    priorityFilter = '';
    commentText = '';
    internalComment = false;
    attachments: File[] = [];
    isSendingComment = false;
    private attachmentInput?: HTMLInputElement;
    repairDescription = '';
    repairResult: 'В эксплуатацию' | 'На склад' | 'Списать' = 'В эксплуатацию';
    assigneeDraft = '';
    priorityDraft = '';
    ticketTypes: PortalTicketType[] = [];
    requesters: RequesterOption[] = [];
    newTicketRequesterId = '';
    loadingRequesters = false;
    criticalities = ['Низкий', 'Средний', 'Высокий'];
    newTicketTypeId = '';
    newTicketTitle = '';
    newTicketDescription = '';
    newTicketPriority = 'Средний';
    showCreateForm = false;
    loadingTypes = false;
    isSubmitting = false;
    notice = '';
    error = '';

    get types(): string[] { return [...new Set(this.tickets.map(ticket => ticket.type).filter((type): type is string => !!type))]; }
    get assignees(): string[] { return [...new Set(this.tickets.map(ticket => ticket.assignee).filter((value): value is string => !!value))]; }
    get statuses(): string[] { return [...new Set(this.tickets.map(ticket => ticket.status))]; }
    get priorities(): string[] { return [...new Set(this.tickets.map(ticket => ticket.priority))]; }
    get engineerOptions(): string[] {
        const known = this.ticketsApi.knownAssigneeNames();
        return known.length ? known : this.settings?.engineers ?? [];
    }
    get filteredTickets(): Ticket[] {
        const query = this.query.toLocaleLowerCase();
        return this.tickets.filter(ticket => (!this.typeFilter || ticket.type === this.typeFilter) && (!this.assigneeFilter || ticket.assignee === this.assigneeFilter) && (!this.statusFilter || ticket.status === this.statusFilter) && (!this.priorityFilter || ticket.priority === this.priorityFilter) && `${ticket.id} ${ticket.title} ${ticket.requester}`.toLocaleLowerCase().includes(query));
    }
    get nextStatuses(): { name: string; statusId: number }[] {
        return this.selected
            ? (this.settings?.statusTransitions[this.selected.status] ?? [])
                .map(name => ({
                    name,
                    statusId: this.ticketsApi.resolveStatusId(name)
                        ?? (this.settings?.statuses.findIndex(status => status.name === name) ?? -1) + 1,
                }))
                .filter(item => item.statusId > 0)
            : [];
    }
    get visibleComments() { return (this.selected?.comments ?? []).filter(comment => !comment.internal || this.canUseInternalComments); }
    get canSelectRequester(): boolean {
        if (!this.isEngineer || this.isManager) return false;
        const activeRole = this.auth.getActiveRole?.();
        return !activeRole || activeRole === 'admin' || activeRole === 'tenant_owner' || activeRole === 'agent' || activeRole === 'manager';
    }

    ngOnInit(): void {
        this.isManager = this.auth.getRole() === 'manager';
        this.isEngineer = this.auth.canRead() && !this.isManager;
        this.canUseInternalComments = this.auth.getRole() === 'admin' || this.auth.getRole() === 'agent';
        this.load();
      this.workspace.getAdminSettings().subscribe({
          next: settings => { this.settings = settings; this.changeDetector.markForCheck(); },
          error: err => { this.error = err.message; this.changeDetector.markForCheck(); },
      });
        this.route.queryParamMap.subscribe(params => {
            this.query = params.get('q') ?? '';
            if (params.get('create') === 'true') this.openCreate();
            this.changeDetector.markForCheck();
        });
    }

    openCreate(): void {
        this.showCreateForm = true;
        this.newTicketTitle = '';
        this.newTicketDescription = '';
        this.newTicketRequesterId = '';
        this.newTicketPriority = this.settings?.criticalities?.[1] ?? 'Средний';
        this.error = '';
        if (this.isEngineer && !this.requesters.length && !this.loadingRequesters) this.loadRequesters();
        if (this.ticketTypes.length || this.loadingTypes) return;

        this.loadingTypes = true;
        this.ticketsApi.getPortalTicketTypes().subscribe({
            next: types => {
                this.ticketTypes = types;
                this.newTicketTypeId = String(types[0]?.id ?? '');
                this.loadingTypes = false;
                this.changeDetector.markForCheck();
            },
            error: err => {
                this.error = err.message;
                this.loadingTypes = false;
                this.changeDetector.markForCheck();
            },
        });
    }

    private loadRequesters(): void {
        this.loadingRequesters = true;
        this.ticketsApi.getRequesters().subscribe({
            next: requesters => {
                this.requesters = requesters;
                this.loadingRequesters = false;
                this.changeDetector.markForCheck();
            },
            error: err => {
                this.error = err.message;
                this.loadingRequesters = false;
                this.changeDetector.markForCheck();
            },
        });
    }

    createTicket(): void {
        const typeId = Number(this.newTicketTypeId);
        const selectedType = this.ticketTypes.find(type => type.id === typeId);
        if (!selectedType || !Number.isInteger(typeId) || !this.newTicketTitle.trim() || !this.newTicketDescription.trim()) {
            this.error = 'Заполните тему, описание и выберите тип заявки.';
            return;
        }

        const request: PortalTicketRequest = {
            title: this.newTicketTitle.trim(),
            description: this.newTicketDescription.trim(),
            requester: this.auth.getLogin(),
            type: selectedType.name,
            typeId,
            priority: this.newTicketPriority,
            ...(this.isEngineer && this.newTicketRequesterId ? { requesterId: Number(this.newTicketRequesterId) } : {}),
        };
        this.isSubmitting = true;
        this.error = '';
        this.ticketsApi.createPortalTicket(request).subscribe({
            next: ticket => {
                this.tickets = [ticket, ...this.tickets];
                this.selected = ticket;
                this.showCreateForm = false;
                this.notice = `Заявка ${ticket.id} создана.`;
                this.isSubmitting = false;
                this.changeDetector.markForCheck();
            },
            error: err => {
                this.error = err.message;
                this.isSubmitting = false;
                this.changeDetector.markForCheck();
            },
        });
    }

    select(ticket: Ticket): void {
        this.selected = { ...ticket, comments: [], history: [] };
        this.repairDescription = '';
        this.assigneeDraft = '';
        this.priorityDraft = ticket.priority;
        this.loadingComments = true;
        this.loadHistory(ticket.id);
        this.ticketsApi.getTicketComments(ticket.id).subscribe({
            next: comments => {
                if (this.selected?.id === ticket.id) {
                    this.selected.comments = comments;
                    this.loadingComments = false;
                    this.changeDetector.markForCheck();
                }
            },
            error: err => {
                if (this.selected?.id === ticket.id) {
                    this.error = err.message;
                    this.loadingComments = false;
                    this.changeDetector.markForCheck();
                }
            },
        });
    }
    assign(): void {
      if (!this.selected || !this.assigneeDraft) return;
      const assigneeId = this.ticketsApi.resolveAssigneeId(this.assigneeDraft);
      if (assigneeId == null) {
        this.error = 'Не удалось определить ID исполнителя из очереди.';
        return;
      }
      this.ticketsApi.assignTicket(this.selected.id, assigneeId).subscribe({ next: ticket => { this.updateTicket(ticket); this.notice = `Назначен исполнитель: ${ticket.assignee}.`; this.changeDetector.markForCheck(); }, error: err => { this.error = err.message; this.changeDetector.markForCheck(); } });
    }
    changePriority(): void {
      if (!this.selected || !this.priorityDraft) return;
      this.ticketsApi.changeTicketPriority(this.selected.id, this.priorityDraft).subscribe({ next: ticket => { this.updateTicket(ticket); this.notice = `Критичность изменена на «${ticket.priority}».`; this.changeDetector.markForCheck(); }, error: err => { this.error = err.message; this.changeDetector.markForCheck(); } });
    }
    changeStatus(statusId: number): void {
      if (!this.selected || !this.nextStatuses.some(status => status.statusId === statusId)) return;
      this.ticketsApi.changeTicketStatus(this.selected.id, statusId).subscribe({ next: ticket => { this.updateTicket(ticket); this.notice = `Статус изменен на «${ticket.status}».`; this.changeDetector.markForCheck(); }, error: err => { this.error = err.message; this.changeDetector.markForCheck(); } });
    }
    addComment(): void {
        if (!this.selected || !this.commentText.trim() || this.isSendingComment) return;
        const ticketId = this.selected.id;
        this.isSendingComment = true;
        this.ticketsApi.addTicketComment(ticketId, { author: this.auth.getLogin() || 'Инженер ТП', message: this.commentText.trim(), internal: this.internalComment }, [...this.attachments]).subscribe({
            next: comment => {
                if (this.selected?.id === ticketId) {
                    this.selected.comments = [...(this.selected.comments ?? []), comment];
                    this.selected.history = [{ id: comment.id, event: comment.internal ? 'Внутренний комментарий добавлен' : 'Комментарий добавлен', actor: comment.author, createdAt: comment.createdAt }, ...(this.selected.history ?? [])];
                    this.commentText = '';
                    this.attachments = [];
                    this.internalComment = false;
                    if (this.attachmentInput) this.attachmentInput.value = '';
                }
                this.isSendingComment = false;
                this.changeDetector.markForCheck();
            },
            error: err => {
                this.error = err.message;
                this.isSendingComment = false;
                this.changeDetector.markForCheck();
            },
        });
    }
    pickAttachments(event: Event): void {
        this.attachmentInput = event.target as HTMLInputElement;
        this.attachments = Array.from(this.attachmentInput.files ?? []);
    }
    downloadAttachment(attachment: TicketAttachment): void {
        if (!this.selected) return;
        this.ticketsApi.downloadTicketAttachment(this.selected.id, attachment).subscribe({
            next: blob => saveTicketAttachment(blob, attachment.fileName),
            error: err => { this.error = err.message; this.changeDetector.markForCheck(); },
        });
    }
    escalate(): void {
        if (!this.selected) return;
        this.ticketsApi.escalateTicket(this.selected.id, 'Инженер ТП').subscribe({ next: entry => { this.selected!.priority = entry.newPriority; this.selected!.status = 'В работе'; this.selected!.history = [{ id: entry.createdAt, event: `Эскалация ${entry.previousPriority} → ${entry.newPriority}; уведомлены ${entry.recipients.join(', ')}`, actor: entry.initiator, createdAt: entry.createdAt }, ...(this.selected!.history ?? [])]; this.notice = `Эскалация создана. Уведомление: ${entry.recipients.join(', ')}.`; this.changeDetector.markForCheck(); this.load(false); }, error: err => { this.error = err.message; this.changeDetector.markForCheck(); } });
    }
    closeRepair(): void {
        if (!this.selected) return;
        this.ticketsApi.closeRepairTicket(this.selected.id, { result: this.repairResult, workDescription: this.repairDescription }).subscribe({ next: ticket => { this.updateTicket(ticket); this.notice = 'Тикет на ремонт закрыт.'; this.changeDetector.markForCheck(); }, error: err => { this.error = err.message; this.changeDetector.markForCheck(); } });
    }

    private updateTicket(ticket: Ticket): void {
        this.tickets = this.tickets.map(item => item.id === ticket.id ? ticket : item);
        this.selected = {
            ...ticket,
            comments: this.selected?.comments ?? [],
            history: this.selected?.history ?? [],
        };
        this.loadHistory(ticket.id);
    }
    private load(resetSelection = true): void { this.ticketsApi.getTickets().subscribe({ next: tickets => { this.tickets = tickets; if (resetSelection) this.selected = null; this.changeDetector.markForCheck(); }, error: err => { this.error = err.message; this.changeDetector.markForCheck(); } }); }
    private loadHistory(ticketId: string): void {
        this.loadingHistory = true;
        this.ticketsApi.getTicketHistory(ticketId).subscribe({
            next: history => {
                if (this.selected?.id === ticketId) {
                    this.selected.history = history;
                    this.loadingHistory = false;
                    this.changeDetector.markForCheck();
                }
            },
            error: err => {
                if (this.selected?.id === ticketId) {
                    this.error = err.message;
                    this.loadingHistory = false;
                    this.changeDetector.markForCheck();
                }
            },
        });
    }
}