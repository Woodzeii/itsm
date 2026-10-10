import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/auth/auth.service';
import { WorkspaceService } from '../../core/workspace/workspace.service';
import { AdminSettings, AssetRecord, FormFieldDefinition, PortalTicketRequest, Ticket, TicketFormDefinition, TicketType } from '../../core/workspace/workspace.models';

@Component({
    selector: 'app-self-service-portal',
    standalone: true,
    imports: [CommonModule, FormsModule],
    template: `
    <section class="portal">
      <header class="portal-head"><div><p>ITSM NC · Портал самообслуживания</p><h1>{{ active ? active.id : view === 'create' ? 'Новая заявка' : 'Мои заявки' }}</h1></div><div class="account"><span>{{ loginInitial }}</span>{{ login }}</div></header>
      @if (error) { <p class="feedback error">{{ error }}</p> }
      @if (notice) { <p class="feedback success">{{ notice }}</p> }

      @if (view === 'list') {
        <div class="toolbar"><div><h2>Мои заявки</h2><p>Здесь отображаются только ваши обращения</p></div><button class="primary" type="button" (click)="openCreate()">＋ Создать заявку</button></div>
        <section class="ticket-list">
          @for (ticket of tickets; track ticket.id) {
            <button class="ticket-row" type="button" (click)="openTicket(ticket)"><span class="ticket-id">{{ ticket.id }}</span><span class="ticket-summary"><strong>{{ ticket.title }}</strong><small>{{ ticket.type }} · {{ ticket.createdOn || ticket.createdAt }}</small></span><span class="status">{{ ticket.status }}</span><span class="priority">{{ ticket.priority }}</span><span class="open" aria-hidden="true">→</span></button>
          }
          @if (!tickets.length) { <p class="empty">У вас пока нет заявок.</p> }
        </section>
      }

      @if (view === 'create') {
        <section class="form-page">
          <button class="back" type="button" (click)="view = 'list'">← Мои заявки</button>
          <form class="request-form" (ngSubmit)="submitTicket()">
            <div class="form-title"><p>Новая заявка</p><h2>Опишите, что случилось</h2></div>
            <label>Тип заявки<select name="ticketType" [(ngModel)]="typeId" (ngModelChange)="resetForm()">@for (type of portalTypes; track type.id) { <option [value]="type.id">{{ type.name }}</option> }</select></label>
            @if (activeForm; as form) {
              @for (field of visibleFields(form); track field.id) {
                @if (!['Тема', 'Описание', 'Критичность'].includes(field.name)) {
                  <label [class.wide]="field.type === 'textarea' || field.type === 'file'">{{ field.name }}@if (field.required) { <span class="required">*</span> }
                    @switch (field.type) {
                      @case ('textarea') { <textarea [name]="field.id" [required]="field.required" [ngModel]="values[field.id]" (ngModelChange)="values[field.id] = $event" [placeholder]="field.hint || ''"></textarea> }
                      @case ('number') { <input type="number" [name]="field.id" [required]="field.required" [ngModel]="values[field.id]" (ngModelChange)="values[field.id] = $event" [placeholder]="field.hint || ''" /> }
                      @case ('date') { <input type="date" [name]="field.id" [required]="field.required" [ngModel]="values[field.id]" (ngModelChange)="values[field.id] = $event" /> }
                      @case ('dictionary') { <select [name]="field.id" [required]="field.required" [ngModel]="values[field.id]" (ngModelChange)="values[field.id] = $event"><option value="">Выберите значение</option>@for (option of field.options ?? settings?.criticalities ?? []; track option) { <option [value]="option">{{ option }}</option> }</select> }
                      @case ('asset') { <select [name]="field.id" [required]="field.required" [ngModel]="values[field.id]" (ngModelChange)="values[field.id] = $event"><option value="">Без привязки</option>@for (asset of assets; track asset.id) { <option [value]="asset.id">{{ asset.id }} · {{ asset.name }}</option> }</select> }
                      @case ('file') { <input type="file" [name]="field.id" multiple (change)="pickFiles($event)" /> }
                      @case ('boolean') { <span class="check"><input type="checkbox" [name]="field.id" [ngModel]="values[field.id]" (ngModelChange)="values[field.id] = $event" /> Да</span> }
                      @default { <input type="text" [name]="field.id" [required]="field.required" [ngModel]="values[field.id]" (ngModelChange)="values[field.id] = $event" [placeholder]="field.hint || ''" /> }
                    }
                    @if (field.hint && field.type !== 'string' && field.type !== 'textarea') { <small>{{ field.hint }}</small> }
                  </label>
                }
              }
              <label>Тема <span class="required">*</span><input name="title" required [(ngModel)]="title" placeholder="Кратко сформулируйте вопрос" /></label>
              <label>Критичность <span class="required">*</span><select name="priority" required [(ngModel)]="priority">@for (level of settings?.criticalities ?? []; track level) { <option [value]="level">{{ level }}</option> }</select></label>
              <label class="wide">Описание <span class="required">*</span><textarea name="description" required [(ngModel)]="description" placeholder="Подробно опишите ситуацию и ожидаемый результат"></textarea></label>
            }
            <footer class="form-footer"><button class="primary" type="submit" [disabled]="!title.trim() || !description.trim()">Отправить заявку</button><button class="quiet" type="button" (click)="view = 'list'">Отмена</button></footer>
          </form>
        </section>
      }

      @if (view === 'detail' && active) {
        <section class="detail-page">
          <button class="back" type="button" (click)="view = 'list'">← Мои заявки</button>
          <div class="detail-grid"><article class="detail-main">
            <div class="ticket-header"><div><p>{{ active.type }} · {{ active.createdOn || active.createdAt }}</p><h2>{{ active.title }}</h2></div><span class="status">{{ active.status }}</span></div>
            <div class="ticket-meta"><span>Критичность <strong>{{ active.priority }}</strong></span><span>Исполнитель <strong>{{ active.assignee || 'Назначается' }}</strong></span></div>
            @if (settings?.portalSlaFields?.includes('Время реакции')) { <p class="sla-line">Время реакции: <strong>{{ active.reactionSla || 'ожидается' }}</strong></p> }
            @if (settings?.portalSlaFields?.includes('Время решения')) { <p class="sla-line">Время решения: <strong>{{ active.resolutionSla || active.slaRemaining }}</strong></p> }
            <section class="conversation"><h3>Переписка</h3>@for (comment of publicComments; track comment.id) { <article><strong>{{ comment.author }}</strong><p>{{ comment.message }}</p><small>{{ comment.createdAt }}</small>@for (file of comment.attachments; track file) { <span class="attachment">{{ file }}</span> }</article> }
              <form (ngSubmit)="sendComment()"><textarea name="reply" [(ngModel)]="reply" placeholder="Напишите сообщение" required></textarea><label class="file-label">Прикрепить файлы<input type="file" multiple (change)="pickCommentFiles($event)" /></label><button class="primary" type="submit" [disabled]="!reply.trim()">Отправить сообщение</button></form>
            </section>
          </article>
          <aside class="actions-panel"><h3>Действия</h3>
            @if (active.status === 'Проверка') { <p>Исполнитель завершил работу. Проверьте результат.</p><button class="primary full" type="button" (click)="review(true)">Закрыть заявку</button><button class="outline full" type="button" (click)="review(false)">Вернуть на доработку</button> }
            @if (active.status !== 'Закрыта' && canEscalate) { <button class="outline full" type="button" (click)="escalate()">Эскалировать заявку</button> }
            @if (active.status === 'Закрыта') { <p>Заявка закрыта. Повторное открытие не предусмотрено.</p> }
          </aside></div>
        </section>
      }
    </section>
  `,
    styles: [`
      :host { display:block; min-height:100%; color:#222b38; background:#f5f7fa; }
      .portal { max-width:1160px; margin:auto; padding:26px 28px 40px; }
      .portal-head { display:flex; align-items:center; justify-content:space-between; min-height:60px; margin-bottom:28px; padding-bottom:15px; border-bottom:1px solid #e2e7ed; }
      .portal-head p,.ticket-header p { margin:0 0 5px; color:#778392; font-size:10px; } h1 { margin:0; font-size:20px; } h2 { margin:0; font-size:17px; } h3 { margin:0 0 13px; font-size:13px; }
      .account { display:flex; align-items:center; gap:8px; color:#566171; font-size:11px; } .account span { width:29px; height:29px; display:grid; place-items:center; border-radius:50%; background:#e6efff; color:#2867c4; font-weight:700; }
      .toolbar { display:flex; align-items:end; justify-content:space-between; gap:15px; margin-bottom:14px; } .toolbar h2 { font-size:15px; } .toolbar p { margin:5px 0 0; color:#8792a0; font-size:10px; }
      button,input,select,textarea { font:inherit; } .primary,.quiet,.outline { min-height:36px; padding:0 13px; border:0; border-radius:5px; cursor:pointer; font-size:10px; font-weight:650; }
      .primary { background:#2563eb; color:#fff; } .quiet { background:#edf0f4; color:#5d6875; } .outline { border:1px solid #dfe5ed; background:#fff; color:#4f5d6e; }
      .ticket-list { overflow:hidden; border:1px solid #e1e6ed; border-radius:8px; background:#fff; }
      .ticket-row { width:100%; min-height:68px; display:flex; align-items:center; gap:17px; padding:12px 16px; border:0; border-bottom:1px solid #edf0f4; background:#fff; text-align:left; cursor:pointer; }
      .ticket-row:hover { background:#f8fafd; } .ticket-id { color:#3475d1; font-size:10px; font-weight:700; }
      .ticket-summary { min-width:0; flex:1; } .ticket-summary strong,.ticket-summary small { display:block; overflow:hidden; text-overflow:ellipsis; white-space:nowrap; }
      .ticket-summary strong { color:#3c4654; font-size:11px; } .ticket-summary small { margin-top:5px; color:#929baa; font-size:9px; }
      .status,.priority { padding:5px 7px; border-radius:4px; background:#eff4fc; color:#3a69a5; font-size:9px; white-space:nowrap; }
      .priority { background:#f2f3f5; color:#697586; } .open { color:#3475d1; } .empty { padding:28px; color:#8993a1; font-size:11px; }
      .form-page,.detail-page { max-width:900px; margin:auto; } .back { margin-bottom:14px; padding:5px 0; border:0; background:none; color:#3475d1; font-size:10px; cursor:pointer; }
      .request-form,.detail-main,.actions-panel { padding:23px; border:1px solid #e2e7ed; border-radius:8px; background:#fff; }
      .request-form { display:grid; grid-template-columns:1fr 1fr; gap:16px; } .form-title,.wide,.form-footer { grid-column:1/-1; }
      .form-title { padding-bottom:13px; border-bottom:1px solid #edf0f4; } .form-title p { margin:0 0 5px; color:#7c8794; font-size:10px; }
      label { display:grid; gap:7px; color:#566171; font-size:10px; font-weight:600; } label small { color:#8792a0; font-weight:400; line-height:1.4; }
      input:not([type=checkbox]):not([type=file]),select,textarea { width:100%; min-height:38px; box-sizing:border-box; padding:8px 10px; border:1px solid #dce2e9; border-radius:5px; background:#fff; color:#394352; font-size:11px; font-weight:400; }
      textarea { min-height:100px; resize:vertical; } .required { color:#c84c4c; } .check { display:flex; gap:7px; align-items:center; font-size:10px; }
      .form-footer { display:flex; gap:8px; padding-top:15px; border-top:1px solid #edf0f4; }
      .detail-grid { display:grid; grid-template-columns:minmax(0,1fr) 230px; align-items:start; gap:14px; }
      .ticket-header { display:flex; justify-content:space-between; gap:12px; align-items:flex-start; } .ticket-header h2 { line-height:1.45; }
      .ticket-meta { display:flex; gap:20px; margin:17px 0; padding:12px 0; border-top:1px solid #edf0f4; border-bottom:1px solid #edf0f4; color:#7e8997; font-size:10px; }
      .ticket-meta strong { display:block; margin-top:4px; color:#414c5a; } .sla-line { color:#667281; font-size:10px; }
      .conversation { margin-top:20px; } .conversation article { margin:9px 0; padding:11px; border-radius:6px; background:#f4f7fb; }
      .conversation article strong { font-size:10px; } .conversation article p { margin:5px 0; color:#586575; font-size:11px; white-space:pre-wrap; }
      .conversation article small { color:#929baa; font-size:9px; } .attachment { display:block; margin-top:4px; color:#3475d1; font-size:9px; }
      .conversation form { display:grid; gap:9px; margin-top:15px; } .conversation textarea { min-height:75px; }
      .file-label { font-weight:400; } .file-label input { font-size:9px; }
      .actions-panel p { color:#7b8694; font-size:10px; line-height:1.5; } .actions-panel button { margin-top:8px; }
      .full { width:100%; } .feedback { padding:10px 12px; border-radius:5px; font-size:10px; } .error { color:#a93232; background:#fff0ef; } .success { color:#176f52; background:#eaf7f1; }
      @media(max-width:720px) { .portal { padding:18px 13px 30px; } .ticket-row { align-items:flex-start; flex-wrap:wrap; gap:8px 12px; } .ticket-summary { flex-basis:calc(100% - 100px); } .ticket-row .status { margin-left:auto; } .request-form { grid-template-columns:1fr; padding:16px; } .form-title,.wide,.form-footer { grid-column:auto; } .detail-grid { grid-template-columns:1fr; } .ticket-meta { flex-wrap:wrap; } .toolbar { align-items:flex-start; flex-direction:column; } }
    `],
})
export class SelfServicePortalComponent implements OnInit {
    private readonly workspace = inject(WorkspaceService);
    private readonly auth = inject(AuthService);

    view: 'list' | 'create' | 'detail' = 'list';
    tickets: Ticket[] = [];
    active: Ticket | null = null;
    ticketTypes: TicketType[] = [];
    forms: TicketFormDefinition[] = [];
    assets: AssetRecord[] = [];
    settings: AdminSettings | null = null;
    policies = this.workspace.getEscalationPolicies();
    policyList = [] as import('../../core/workspace/workspace.models').EscalationPolicy[];
    typeId = '';
    title = '';
    description = '';
    priority = '';
    values: Record<string, string | number | boolean | null> = {};
    attachments: File[] = [];
    commentFiles: File[] = [];
    reply = '';
    error = '';
    notice = '';

    get login(): string { return this.auth.getLogin() || 'Пользователь портала'; }
    get loginInitial(): string { return this.login.charAt(0).toLocaleUpperCase(); }
    get portalTypes(): TicketType[] { return this.ticketTypes.filter(type => type.portalAvailable && !type.repair); }
    get activeForm(): TicketFormDefinition | undefined { return this.forms.find(form => form.ticketTypeId === this.typeId); }
    get publicComments() { return (this.active?.comments ?? []).filter(comment => !comment.internal); }
    get canEscalate(): boolean { return !!this.policyList.find(policy => policy.ticketTypeName === this.active?.type)?.allowManualByRequester; }

    ngOnInit(): void {
        this.workspace.getPortalTickets().subscribe({ next: tickets => this.tickets = tickets, error: err => this.error = err.message });
        this.workspace.getTicketTypes().subscribe({ next: types => { this.ticketTypes = types; this.typeId = this.portalTypes[0]?.id ?? ''; }, error: err => this.error = err.message });
        this.workspace.getForms().subscribe({ next: forms => this.forms = forms, error: err => this.error = err.message });
        this.workspace.getAssets().subscribe({ next: assets => this.assets = assets.filter(asset => asset.lifecycle !== 'Списан'), error: err => this.error = err.message });
        this.workspace.getAdminSettings().subscribe({ next: settings => { this.settings = settings; this.priority = settings.criticalities[1] ?? ''; }, error: err => this.error = err.message });
        this.workspace.getEscalationPolicies().subscribe({ next: policies => this.policyList = policies, error: err => this.error = err.message });
    }

    visibleFields(form: TicketFormDefinition): FormFieldDefinition[] {
        return form.fields.filter(field => !field.conditionFieldId || this.values[field.conditionFieldId] === field.conditionValue);
    }
    openCreate(): void { this.resetForm(); this.view = 'create'; }
    resetForm(): void { this.values = {}; this.attachments = []; this.title = ''; this.description = ''; this.error = ''; }
    openTicket(ticket: Ticket): void { this.active = ticket; this.view = 'detail'; }
    pickFiles(event: Event): void { this.attachments = [...this.attachments, ...Array.from((event.target as HTMLInputElement).files ?? [])]; }
    pickCommentFiles(event: Event): void { this.commentFiles = Array.from((event.target as HTMLInputElement).files ?? []); }

    submitTicket(): void {
        const form = this.activeForm;
        const assetField = form?.fields.find(field => field.type === 'asset' && this.values[field.id]);
        const request: PortalTicketRequest = {
            title: this.title.trim(), description: this.description.trim(), requester: this.login,
            type: this.portalTypes.find(type => type.id === this.typeId)?.name ?? '', priority: this.priority,
            assetId: assetField ? String(this.values[assetField.id]) : undefined,
            fields: { ...this.values }, attachments: [...this.attachments],
        };
        this.workspace.createPortalTicket(request).subscribe({
            next: ticket => { this.tickets = [ticket, ...this.tickets]; this.active = ticket; this.view = 'detail'; this.notice = `Заявка ${ticket.id} зарегистрирована.`; },
            error: err => this.error = err.message,
        });
    }

    sendComment(): void {
        if (!this.active) return;
        this.workspace.addPortalComment(this.active.id, this.reply.trim(), this.commentFiles).subscribe({
            next: comment => { this.active!.comments = [...(this.active!.comments ?? []), comment]; this.reply = ''; this.commentFiles = []; },
            error: err => this.error = err.message,
        });
    }

    review(accepted: boolean): void {
        if (!this.active) return;
        this.workspace.reviewPortalTicket(this.active.id, accepted).subscribe({ next: ticket => { this.active = ticket; this.tickets = this.tickets.map(item => item.id === ticket.id ? ticket : item); this.notice = accepted ? 'Заявка закрыта.' : 'Заявка возвращена в работу.'; }, error: err => this.error = err.message });
    }

    escalate(): void {
        if (!this.active) return;
        this.workspace.escalateTicket(this.active.id, 'Автор заявки').subscribe({ next: result => { this.active!.priority = result.newPriority; this.active!.history = [{ id: result.createdAt, event: `Эскалация ${result.previousPriority} → ${result.newPriority}`, actor: result.initiator, createdAt: result.createdAt }, ...(this.active!.history ?? [])]; this.notice = `Руководитель уведомлен: ${result.recipients.join(', ')}.`; }, error: err => this.error = err.message });
    }
}