import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { WorkspaceService } from '../../core/workspace/workspace.service';
import { AdminSettings, EscalationHistoryEntry, EscalationPolicy, FormFieldDefinition, ReportSummary, TicketFormDefinition, TicketType } from '../../core/workspace/workspace.models';

type AdminSection = 'settings' | 'forms' | 'escalations' | 'reports';

@Component({
    selector: 'app-admin-workspace',
    standalone: true,
    imports: [CommonModule, FormsModule, RouterLink],
    template: `
    <section class="page">
      <header class="heading"><div><p class="eyebrow">Администрирование</p><h1>{{ pageTitle }}</h1></div></header>
      <nav class="admin-nav"><a routerLink="/admin/settings" routerLinkActive="active">Настройки</a><a routerLink="/admin/forms" routerLinkActive="active">Конструктор форм</a><a routerLink="/admin/escalation-policies" routerLinkActive="active">Эскалация</a><a routerLink="/admin/reports" routerLinkActive="active">Отчёты</a><a routerLink="/dictionaries">Справочники</a><a routerLink="/asset-classes">Классы активов</a></nav>
      @if (error) { <p class="feedback error">{{ error }}</p> }
      @if (notice) { <p class="feedback success">{{ notice }}</p> }

      @if (section === 'settings' && settings) {
        <form class="settings" (ngSubmit)="saveSettings()">
          <section class="panel"><header><h2>Общие параметры</h2><p>Назначение, режим SLA и доступ портала</p></header><div class="field-grid">
            <label>Назначение исполнителя<select name="assignment" [(ngModel)]="settings.assignmentMode"><option value="automatic">Автоматическое: минимальная открытая очередь</option><option value="manual">Ручное</option></select></label>
            <label>Руководитель (инженер ТП)<select name="manager" [(ngModel)]="settings.manager">@for (engineer of settings.engineers ?? []; track engineer) { <option [value]="engineer">{{ engineer }}</option> }</select></label>
            <label>Поставка<select name="tenantMode" [(ngModel)]="settings.tenantMode"><option value="on-premise">On-premise</option><option value="saas">SaaS с изоляцией тенантов</option></select></label>
            <label>Учёт SLA<select name="slaMode" [(ngModel)]="settings.slaMode"><option value="24/7">Круглосуточно</option><option value="working-hours">По рабочему графику</option></select></label>
            @if (settings.slaMode === 'working-hours') { <label>Рабочий график<input name="workingHours" [(ngModel)]="settings.workingHours" /></label> }
            <label class="check"><input type="checkbox" name="slaEnabled" [(ngModel)]="settings.slaEnabled" /> SLA включён в системе</label>
            <label class="check"><input type="checkbox" name="reportAdmin" [checked]="settings.reportAccessRoles?.includes('admin')" (change)="toggleReportRole('admin',$event)" /> Отчёты доступны администратору</label>
            <label class="check"><input type="checkbox" name="reportManager" [checked]="settings.reportAccessRoles?.includes('manager')" (change)="toggleReportRole('manager',$event)" /> Отчёты доступны руководителю</label>
          </div></section>

          <section class="panel"><header><h2>Критичность</h2><p>От 3 до 5 уровней; уровень задаёт автор, инженер может изменить</p></header>
            <div class="list-editor">@for (level of settings.criticalities; track $index; let index = $index) { <label>Уровень {{ index + 1 }}<div class="inline"><input [name]="'priority-' + index" [(ngModel)]="settings.criticalities[index]" /><button class="icon-action" type="button" aria-label="Удалить уровень" [disabled]="settings.criticalities.length <= 3" (click)="removeCriticality(index)">×</button></div></label> }</div>
            <div class="inline add-row"><input name="newCriticality" [(ngModel)]="newCriticality" placeholder="Новый уровень критичности" /><button class="quiet" type="button" [disabled]="settings.criticalities.length >= 5 || !newCriticality.trim()" (click)="addCriticality()">Добавить уровень</button></div>
          </section>

          <section class="panel"><header><h2>Статусы и SLA</h2><p>Стандартные статусы обязательны; дополнительные статусы могут приостанавливать SLA</p></header>
            <div class="status-list">@for (status of settings.statuses; track $index; let index = $index) { <div class="status-row"><input [name]="'status-' + index" [(ngModel)]="status.name" [disabled]="status.immutable" /><label class="check"><input type="checkbox" [name]="'pause-' + index" [(ngModel)]="status.pausesSla" /> Приостанавливает SLA</label>@if (!status.immutable) { <button class="icon-action" type="button" aria-label="Удалить статус" (click)="removeStatus(index)">×</button> }</div> }</div>
            <div class="inline add-row"><input name="newStatus" [(ngModel)]="newStatus" placeholder="Дополнительный статус" /><button class="quiet" type="button" [disabled]="!newStatus.trim()" (click)="addStatus()">Добавить статус</button></div>
          </section>

          <section class="panel"><header><h2>Типы заявок и SLA</h2><p>Нормативы времени реакции и решения задаются для сочетания типа и критичности</p></header>
            <div class="type-list">@for (type of ticketTypes; track type.id) { <article class="type-row">
              <div class="type-top"><label>Тип заявки<input [name]="'type-' + type.id" [(ngModel)]="type.name" [disabled]="type.repair" /></label><label class="check"><input type="checkbox" [name]="'portal-' + type.id" [(ngModel)]="type.portalAvailable" [disabled]="type.repair" /> Доступен на портале</label><label class="check"><input type="checkbox" [name]="'type-sla-' + type.id" [(ngModel)]="type.slaEnabled" /> SLA включён</label><span class="builtin">{{ type.repair ? 'Встроенный тип' : '' }}</span></div>
              <div class="sla-grid">@for (level of settings.criticalities; track level) { <div class="sla-level"><strong>{{ level }}</strong><label>Реакция<input [name]="'reaction-' + type.id + '-' + level" [ngModel]="type.slaByCriticality?.[level]?.reaction || type.reactionSla" (ngModelChange)="setTypeSla(type,level,'reaction',$event)" /></label><label>Решение<input [name]="'resolution-' + type.id + '-' + level" [ngModel]="type.slaByCriticality?.[level]?.resolution || type.resolutionSla" (ngModelChange)="setTypeSla(type,level,'resolution',$event)" /></label></div> }</div>
              @if (!type.repair) { <button class="remove-type" type="button" (click)="removeType(type)">Удалить тип</button> }
            </article> }</div>
            <div class="inline add-row"><input name="newType" [(ngModel)]="newType" placeholder="Название нового типа заявки" /><button class="quiet" type="button" [disabled]="!newType.trim()" (click)="addType()">Создать тип</button></div>
          </section>

          <section class="panel"><header><h2>Портальные учётные записи</h2><p>Учётные записи создаёт администратор; пароль проверяется по настроенной политике</p></header>
            <div class="account-grid"><label>Новый логин<input name="portalUsername" [(ngModel)]="newUsername" autocomplete="off" /></label><label>Временный пароль<input name="portalPassword" type="password" [(ngModel)]="newPassword" autocomplete="new-password" /></label><button class="quiet align-end" type="button" [disabled]="!newUsername.trim() || !newPassword" (click)="createPortalAccount()">Создать учётную запись</button></div>
            <p class="account-list">{{ (settings.portalAccounts ?? []).join(' · ') || 'Учётных записей нет' }}</p>
          </section>

          <section class="panel"><header><h2>Пароли и блокировка</h2><p>Правила применяются при создании пользовательских учётных записей</p></header>
            @if (settings.passwordPolicy; as passwordPolicy) { <div class="field-grid"><label>Минимальная длина<input type="number" min="8" name="minLength" [(ngModel)]="passwordPolicy.minLength" /></label><label>Срок действия, дней<input type="number" min="0" name="expiryDays" [(ngModel)]="passwordPolicy.expiryDays" /></label><label>Неудачных попыток до блокировки<input type="number" min="1" name="maxAttempts" [(ngModel)]="passwordPolicy.maxFailedAttempts" /></label><label>Блокировка, минут<input type="number" min="1" name="lockoutMinutes" [(ngModel)]="passwordPolicy.lockoutMinutes" /></label><label class="check"><input type="checkbox" name="uppercase" [(ngModel)]="passwordPolicy.requireUppercase" /> Требовать заглавную букву</label><label class="check"><input type="checkbox" name="special" [(ngModel)]="passwordPolicy.requireSpecial" /> Требовать спецсимвол</label></div> }
          </section>
          <section class="panel"><header><h2>Сроки SLA на портале</h2><p>Выберите показатели, видимые автору заявки</p></header><div class="field-grid"><label class="check"><input type="checkbox" [checked]="settings.portalSlaFields.includes('Время реакции')" (change)="togglePortalSla('Время реакции',$event)" /> Время реакции</label><label class="check"><input type="checkbox" [checked]="settings.portalSlaFields.includes('Время решения')" (change)="togglePortalSla('Время решения',$event)" /> Время решения</label></div></section>
          <button class="primary save-button" type="submit">Сохранить настройки</button>
        </form>
      }

      @if (section === 'forms') {
        <section class="panel"><header><h2>Формы по типам заявок</h2><p>Одна форма на тип; встроенные поля нельзя удалить</p></header><label class="form-select">Тип заявки<select name="formId" [(ngModel)]="formId" (ngModelChange)="error = ''; notice = ''">@for (form of forms; track form.ticketTypeId) { <option [value]="form.ticketTypeId">{{ form.ticketTypeName }}</option> }</select></label>
          @if (activeForm; as form) { <div class="fields">
            @for (field of form.fields; track field.id; let index = $index) { <article class="field-row">
              <div class="field-main"><label>Название<input [name]="'field-name-' + field.id" [(ngModel)]="field.name" [disabled]="field.immutable ?? false" /></label><label>Тип поля<select [name]="'field-type-' + field.id" [(ngModel)]="field.type" [disabled]="field.immutable ?? false"><option value="string">Строка</option><option value="textarea">Многострочный текст</option><option value="number">Число</option><option value="date">Дата</option><option value="dictionary">Справочник</option><option value="asset">Выбор актива</option><option value="file">Вложение</option><option value="boolean">Да/нет</option></select></label><label class="check"><input type="checkbox" [name]="'required-' + field.id" [(ngModel)]="field.required" [disabled]="field.immutable ?? false" /> Обязательное</label><label class="check"><input type="checkbox" [name]="'visible-' + field.id" [(ngModel)]="field.visibleToRequester" /> Видно автору</label></div>
              <div class="field-options"><label>Значение по умолчанию<input [name]="'default-' + field.id" [(ngModel)]="field.defaultValue" /></label><label>Проверка ввода<input [name]="'validation-' + field.id" [(ngModel)]="field.validation" placeholder="Например, регулярное выражение" /></label><label>Подсказка<input [name]="'hint-' + field.id" [(ngModel)]="field.hint" /></label>
                @if (field.type === 'dictionary') { <label>Значения списка<input [name]="'options-' + field.id" [ngModel]="field.options?.join(', ') || ''" (ngModelChange)="setOptions(field,$event)" placeholder="Значение 1, Значение 2" /></label> }
                @if (index > 2) { <label>Показывать, если поле равно<input [name]="'condition-' + field.id" [(ngModel)]="field.conditionValue" placeholder="Значение условия" /></label><label>Зависит от поля<select [name]="'condition-field-' + field.id" [(ngModel)]="field.conditionFieldId"><option value="">Всегда показывать</option>@for (other of form.fields; track other.id) { @if (other.id !== field.id) { <option [value]="other.id">{{ other.name }}</option> } }</select></label> }
              </div>
              @if (!field.immutable) { <button class="icon-action" type="button" aria-label="Удалить поле" (click)="removeField(form,field)">Удалить поле</button> }
            </article> }
            <div class="add-field"><label>Новое поле<input name="newFieldName" [(ngModel)]="newFieldName" placeholder="Название поля" /></label><label>Тип<select name="newFieldType" [(ngModel)]="newFieldType"><option value="string">Строка</option><option value="textarea">Многострочный текст</option><option value="number">Число</option><option value="date">Дата</option><option value="dictionary">Справочник</option><option value="asset">Выбор актива</option><option value="file">Вложение</option><option value="boolean">Да/нет</option></select></label><button class="quiet align-end" type="button" [disabled]="!newFieldName.trim()" (click)="addField(form)">Добавить поле</button></div>
            <button class="primary save-button" type="button" (click)="saveForm(form)">Сохранить форму</button>
          </div> }
        </section>
      }

      @if (section === 'escalations') {
        <section class="panel"><header><h2>Правила эскалации по типам</h2><p>Запуск по нарушению времени решения или вручную; угроза SLA и время реакции эскалацию не запускают</p></header>
          <div class="policy-list">@for (policy of policies; track policy.ticketTypeId) { <article class="policy-row"><div><h3>{{ policy.ticketTypeName }}</h3><small>Руководитель: {{ policy.manager }}</small></div><label class="check"><input type="checkbox" [name]="'breach-' + policy.ticketTypeId" [(ngModel)]="policy.onResolutionBreach" /> Нарушение времени решения</label><label class="check"><input type="checkbox" [name]="'requester-' + policy.ticketTypeId" [(ngModel)]="policy.allowManualByRequester" /> Вручную автором</label><label class="check"><input type="checkbox" [name]="'agent-' + policy.ticketTypeId" [(ngModel)]="policy.allowManualByAgent" /> Вручную инженером</label><label>Руководитель<select [name]="'manager-' + policy.ticketTypeId" [(ngModel)]="policy.manager">@for (engineer of settings?.engineers ?? []; track engineer) { <option [value]="engineer">{{ engineer }}</option> }</select></label></article> }</div>
          <button class="primary save-button" type="button" (click)="savePolicies()">Сохранить правила</button>
        </section>
        <section class="panel history-panel"><header><h2>История эскалаций</h2><p>Инициатор, время, повышение критичности и получатели внутреннего уведомления</p></header>@for (entry of escalationHistory; track $index) { <article><strong>{{ entry.ticketId }} · {{ entry.previousPriority }} → {{ entry.newPriority }}</strong><small>{{ entry.createdAt }} · {{ entry.initiator }} · {{ entry.recipients.join(', ') }}</small></article> } @if (!escalationHistory.length) { <p class="empty">Эскалаций пока нет</p> }</section>
      }

      @if (section === 'reports') {
        <section class="panel"><header><h2>Отчёты Service Desk</h2><p>Все отчёты из технического задания</p></header><div class="period"><label>С даты<input type="date" name="from" [(ngModel)]="reportFrom" /></label><label>По дату<input type="date" name="to" [(ngModel)]="reportTo" /></label><button class="quiet" type="button" (click)="loadReports()">Обновить</button><button class="quiet" type="button" (click)="downloadCsv()">↓ CSV</button></div>
          <div class="report-list">@for (report of reports; track report.id) { <article><span class="report-icon" aria-hidden="true">▤</span><div><strong>{{ report.title }}</strong><p>{{ report.description }}</p></div><b>{{ report.value }}</b></article> }</div>
        </section>
      }
    </section>
  `,
    styles: [`
      :host { display:block; color:#202734; } .page { max-width:1300px; margin:auto; padding:25px 28px 38px; }
      .heading { margin-bottom:16px; } .eyebrow { margin:0 0 4px; color:#8993a1; font-size:10px; } h1 { margin:0; font-size:23px; }
      h2 { margin:0; font-size:13px; } h3 { margin:0; font-size:11px; }
      .admin-nav { display:flex; gap:4px; margin-bottom:17px; overflow-x:auto; border-bottom:1px solid #e2e7ed; }
      .admin-nav a { min-height:36px; display:flex; align-items:center; padding:0 10px; border-bottom:2px solid transparent; color:#687484; font-size:10px; text-decoration:none; white-space:nowrap; }
      .admin-nav a.active { border-color:#3475d1; color:#2864bf; font-weight:650; }
      .panel { margin-bottom:13px; border:1px solid #e2e7ed; border-radius:7px; background:#fff; }
      .panel > header { padding:15px 16px 12px; border-bottom:1px solid #edf0f4; } .panel > header p { margin:5px 0 0; color:#8993a1; font-size:10px; }
      label { display:grid; gap:5px; color:#657181; font-size:9px; } input,select,button { font:inherit; }
      input:not([type=checkbox]),select { min-width:0; min-height:32px; padding:6px 8px; border:1px solid #dfe4ea; border-radius:4px; background:#fff; color:#414c5a; font-size:10px; }
      input:disabled { background:#f5f6f8; color:#818b98; }
      .field-grid { display:grid; grid-template-columns:repeat(3,minmax(0,1fr)); gap:13px; padding:15px 16px; }
      .check { display:flex; align-items:center; gap:6px; min-height:30px; color:#5e6978; font-size:9px; }
      .list-editor,.status-list,.type-list,.policy-list,.fields,.report-list { padding:4px 16px 12px; }
      .list-editor { display:grid; grid-template-columns:repeat(3,minmax(0,1fr)); gap:11px; padding-top:13px; }
      .inline { display:flex; gap:7px; align-items:center; } .inline input { flex:1; }
      .icon-action { width:28px; height:28px; border:0; border-radius:4px; background:#fff2f1; color:#a94343; cursor:pointer; }
      .icon-action:disabled { opacity:.4; cursor:not-allowed; }
      .add-row { margin:0 16px 15px; } .quiet { min-height:32px; padding:0 10px; border:1px solid #dfe4ea; border-radius:4px; background:#f8f9fb; color:#536173; font-size:9px; cursor:pointer; white-space:nowrap; }
      .quiet:disabled { opacity:.5; cursor:not-allowed; } .status-row { display:grid; grid-template-columns:minmax(140px,1fr) 1fr 28px; align-items:center; gap:12px; padding:8px 0; border-bottom:1px solid #f0f2f5; }
      .type-row { padding:14px 0; border-bottom:1px solid #e9edf1; } .type-top { display:grid; grid-template-columns:1.5fr auto auto auto; align-items:end; gap:14px; }
      .type-top input { width:100%; } .builtin { color:#7d8794; font-size:9px; }
      .sla-grid { display:grid; grid-template-columns:repeat(3,minmax(0,1fr)); gap:8px; margin-top:12px; }
      .sla-level { display:grid; grid-template-columns:1fr 1fr; gap:6px; padding:9px; border-radius:5px; background:#f7f9fb; } .sla-level strong { grid-column:1/-1; color:#546173; font-size:10px; }
      .remove-type { margin-top:8px; padding:4px 0; border:0; background:none; color:#ad4a4a; font-size:9px; cursor:pointer; }
      .account-grid { display:grid; grid-template-columns:1fr 1fr auto; gap:10px; align-items:end; padding:14px 16px 0; } .align-end { align-self:end; }
      .account-list { padding:0 16px 12px; color:#84909d; font-size:9px; }
      .save-button { min-height:36px; margin:0 16px 16px; padding:0 13px; border:0; border-radius:5px; background:#2563eb; color:#fff; font-size:10px; font-weight:650; cursor:pointer; }
      .feedback { padding:9px 12px; border-radius:4px; font-size:10px; } .error { color:#a93232; background:#fff0ef; } .success { color:#176f52; background:#edf8f2; }
      .form-select { max-width:380px; margin:14px 16px; } .fields { padding-top:0; } .field-row { position:relative; padding:14px 0; border-bottom:1px solid #edf0f4; }
      .field-main { display:grid; grid-template-columns:1.5fr 1fr auto auto; align-items:end; gap:10px; }
      .field-options { display:grid; grid-template-columns:repeat(2,minmax(0,1fr)); gap:9px; margin-top:10px; }
      .field-row > .icon-action { width:auto; padding:0 8px; margin-top:8px; }
      .add-field { display:grid; grid-template-columns:1.5fr 1fr auto; gap:10px; align-items:end; margin:14px 0; padding:12px; background:#f7f9fb; }
      .policy-row { display:grid; grid-template-columns:1.2fr 1.3fr 1fr 1fr 1.2fr; align-items:center; gap:11px; padding:12px 0; border-bottom:1px solid #edf0f4; }
      .policy-row small { display:block; margin-top:4px; color:#8993a1; font-size:9px; } .policy-row select { width:100%; }
      .history-panel > article { display:grid; gap:5px; padding:10px 16px; border-bottom:1px solid #eef1f4; } .history-panel article strong { font-size:10px; } .history-panel article small { color:#8792a0; font-size:9px; }
      .period { display:flex; align-items:end; gap:9px; padding:13px 16px; } .period label { min-width:140px; }
      .report-list article { display:flex; align-items:center; gap:12px; padding:12px 0; border-bottom:1px solid #eef1f4; }
      .report-icon { width:30px; height:30px; display:grid; place-items:center; border-radius:5px; background:#edf4ff; color:#3270cb; }
      .report-list article div { min-width:0; flex:1; } .report-list article strong { font-size:10px; } .report-list article p { margin:4px 0 0; color:#84909d; font-size:9px; } .report-list article b { color:#4a5665; font-size:10px; white-space:nowrap; }
      .empty { padding:10px 16px; color:#8993a1; font-size:10px; }
      @media(max-width:950px) { .policy-row { grid-template-columns:repeat(2,minmax(0,1fr)); } .type-top { grid-template-columns:1fr 1fr; } .sla-grid { grid-template-columns:1fr; } }
      @media(max-width:650px) { .page { padding:20px 13px 30px; } .field-grid,.list-editor,.field-main,.field-options,.account-grid,.add-field { grid-template-columns:1fr; } .status-row { grid-template-columns:1fr auto; } .status-row .check { grid-column:1; } .policy-row { grid-template-columns:1fr; } .period { align-items:stretch; flex-direction:column; } .period label { min-width:0; } }
    `],
})
export class AdminWorkspaceComponent implements OnInit {
    private readonly workspace = inject(WorkspaceService);
    private readonly route = inject(ActivatedRoute);

    section: AdminSection = 'settings';
    settings: AdminSettings | null = null;
    ticketTypes: TicketType[] = [];
    forms: TicketFormDefinition[] = [];
    formId = '';
    policies: EscalationPolicy[] = [];
    escalationHistory: EscalationHistoryEntry[] = [];
    reports: ReportSummary[] = [];
    error = '';
    notice = '';
    newCriticality = '';
    newStatus = '';
    newType = '';
    newUsername = '';
    newPassword = '';
    newFieldName = '';
    newFieldType: FormFieldDefinition['type'] = 'string';
    reportFrom = '';
    reportTo = '';

    get pageTitle(): string { return { settings: 'Настройки Service Desk', forms: 'Конструктор форм', escalations: 'Процесс эскалации', reports: 'Отчёты' }[this.section]; }
    get activeForm(): TicketFormDefinition | undefined { return this.forms.find(form => form.ticketTypeId === this.formId); }

    ngOnInit(): void {
        this.section = this.route.snapshot.data['section'] as AdminSection;
        this.workspace.getAdminSettings().subscribe({ next: settings => this.settings = settings, error: err => this.error = err.message });
        if (this.section === 'settings') this.workspace.getTicketTypes().subscribe({ next: types => this.ticketTypes = types, error: err => this.error = err.message });
        if (this.section === 'forms') this.workspace.getForms().subscribe({ next: forms => { this.forms = forms; this.formId = forms[0]?.ticketTypeId ?? ''; }, error: err => this.error = err.message });
        if (this.section === 'escalations') {
            this.workspace.getEscalationPolicies().subscribe({ next: policies => this.policies = policies, error: err => this.error = err.message });
            this.workspace.getEscalationHistory().subscribe({ next: history => this.escalationHistory = history, error: err => this.error = err.message });
        }
        if (this.section === 'reports') this.loadReports();
    }

    saveSettings(): void {
        if (!this.settings) return;
        this.workspace.saveAdminSettings(this.settings).subscribe({ next: settings => { this.settings = settings; this.notice = 'Настройки сохранены.'; }, error: err => this.error = err.message });
        this.workspace.saveTicketTypes(this.ticketTypes).subscribe({ next: types => this.ticketTypes = types, error: err => this.error = err.message });
    }
    addCriticality(): void { if (!this.settings || this.settings.criticalities.length >= 5 || !this.newCriticality.trim()) return; this.settings.criticalities.push(this.newCriticality.trim()); this.newCriticality = ''; }
    removeCriticality(index: number): void { if (this.settings && this.settings.criticalities.length > 3) this.settings.criticalities.splice(index, 1); }
    addStatus(): void { if (!this.settings || !this.newStatus.trim()) return; this.settings.statuses.push({ name: this.newStatus.trim(), pausesSla: false, immutable: false }); this.newStatus = ''; }
    removeStatus(index: number): void { if (this.settings && !this.settings.statuses[index].immutable) this.settings.statuses.splice(index, 1); }
    addType(): void {
        if (!this.settings || !this.newType.trim()) return;
        const id = `type-${Date.now()}`;
        const slaByCriticality = Object.fromEntries(this.settings.criticalities.map(level => [level, { reaction: '1 ч', resolution: '8 ч' }]));
        const type: TicketType = { id, name: this.newType.trim(), portalAvailable: true, repair: false, slaEnabled: true, reactionSla: '1 ч', resolutionSla: '8 ч', slaByCriticality };
        this.ticketTypes = [...this.ticketTypes, type];
        this.workspace.saveTicketTypes(this.ticketTypes).subscribe({
            next: () => {
                this.workspace.saveForm({ ticketTypeId: id, ticketTypeName: type.name, fields: this.defaultFields(id) }).subscribe({ next: form => { this.forms.push(form); this.newType = ''; this.notice = 'Тип и базовая форма созданы.'; }, error: err => this.error = err.message });
            },
            error: err => this.error = err.message,
        });
    }
    removeType(type: TicketType): void { this.ticketTypes = this.ticketTypes.filter(item => item.id !== type.id); }
    setTypeSla(type: TicketType, level: string, metric: 'reaction' | 'resolution', value: string): void {
        type.slaByCriticality ??= {};
        type.slaByCriticality[level] ??= { reaction: type.reactionSla, resolution: type.resolutionSla };
        type.slaByCriticality[level][metric] = value;
    }
    togglePortalSla(field: string, event: Event): void { if (!this.settings) return; this.settings.portalSlaFields = this.toggleString(this.settings.portalSlaFields, field, (event.target as HTMLInputElement).checked); }
    toggleReportRole(role: string, event: Event): void { if (!this.settings) return; this.settings.reportAccessRoles = this.toggleString(this.settings.reportAccessRoles ?? [], role, (event.target as HTMLInputElement).checked); }
    createPortalAccount(): void {
        this.workspace.createPortalAccount({ username: this.newUsername, temporaryPassword: this.newPassword }).subscribe({
            next: username => { if (this.settings) this.settings.portalAccounts = [...(this.settings.portalAccounts ?? []), username]; this.newUsername = ''; this.newPassword = ''; this.notice = 'Портальная учётная запись создана.'; },
            error: err => this.error = err.message,
        });
    }
    savePolicies(): void { this.workspace.saveEscalationPolicies(this.policies).subscribe({ next: policies => { this.policies = policies; this.notice = 'Правила эскалации сохранены.'; }, error: err => this.error = err.message }); }
    addField(form: TicketFormDefinition): void {
        const name = this.newFieldName.trim();
        if (!name) return;
        form.fields.push({ id: `field-${Date.now()}`, name, type: this.newFieldType, required: false, visibleToRequester: true });
        this.newFieldName = '';
    }
    removeField(form: TicketFormDefinition, field: FormFieldDefinition): void { form.fields = form.fields.filter(item => item.id !== field.id); }
    setOptions(field: FormFieldDefinition, value: string): void { field.options = value.split(',').map(item => item.trim()).filter(Boolean); }
    saveForm(form: TicketFormDefinition): void { this.workspace.saveForm(form).subscribe({ next: saved => { this.forms = this.forms.map(item => item.ticketTypeId === saved.ticketTypeId ? saved : item); this.notice = 'Форма сохранена и будет использоваться для новых и существующих заявок.'; }, error: err => this.error = err.message }); }
    loadReports(): void { this.workspace.getReports(this.reportFrom || undefined, this.reportTo || undefined).subscribe({ next: reports => this.reports = reports, error: err => this.error = err.message }); }
    downloadCsv(): void {
        const rows = [['Отчёт', 'Описание', 'Результат'], ...this.reports.map(item => [item.title, item.description, item.value])];
        const csv = rows.map(row => row.map(value => `"${value.replaceAll('"', '""')}"`).join(';')).join('\r\n');
        const url = URL.createObjectURL(new Blob(['\uFEFF', csv], { type: 'text/csv;charset=utf-8' }));
        const anchor = document.createElement('a');
        anchor.href = url;
        anchor.download = 'itsm-reports.csv';
        anchor.click();
        URL.revokeObjectURL(url);
    }

    private toggleString(values: string[], value: string, enabled: boolean): string[] { return enabled ? [...new Set([...values, value])] : values.filter(item => item !== value); }
    private defaultFields(id: string): FormFieldDefinition[] { return [
        { id: `${id}-title`, name: 'Тема', type: 'string', required: true, visibleToRequester: true, immutable: true },
        { id: `${id}-description`, name: 'Описание', type: 'textarea', required: true, visibleToRequester: true, immutable: true },
        { id: `${id}-priority`, name: 'Критичность', type: 'dictionary', required: true, visibleToRequester: true, options: this.settings?.criticalities, immutable: true },
    ]; }
}