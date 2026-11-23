import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { CdkDragDrop, DragDropModule, moveItemInArray } from '@angular/cdk/drag-drop';
import { finalize } from 'rxjs';
import { WorkspaceService } from '../../core/workspace/workspace.service';
import { AdminSettings, EscalationHistoryEntry, EscalationPolicy, FormFieldDefinition, ReportDetail, ReportSummary, TenantUserQuota, TicketFormDefinition, TicketFormType, TicketType } from '../../core/workspace/workspace.models';
import { AdminService } from '../../core/admin/admin.service';
import { RoleSummary, UpdateAdminUserRequest, UserSummary } from '../../core/admin/admin.models';
import { TenantUserQuotaService } from '../../core/workspace/tenant-user-quota.service';
import { AuthService } from '../../core/auth/auth.service';
import { HttpWorkspaceApiService } from '../../core/workspace/http-workspace-api.service';

type AdminSection = 'settings' | 'forms' | 'escalations' | 'reports' | 'users';

@Component({
    selector: 'app-admin-workspace',
    standalone: true,
    imports: [CommonModule, DragDropModule, FormsModule, RouterLink],
    template: `
    <section class="page">
      <header class="heading"><div><p class="eyebrow">Администрирование</p><h1>{{ pageTitle }}</h1></div></header>
      <nav class="admin-nav"><a routerLink="/admin/settings" routerLinkActive="active">Настройки</a><a routerLink="/admin/forms" routerLinkActive="active">Конструктор форм</a><a routerLink="/admin/escalation-policies" routerLinkActive="active">Эскалация</a><a routerLink="/admin/reports" routerLinkActive="active">Отчёты</a><a routerLink="/admin/users" routerLinkActive="active">Пользователи</a><a routerLink="/dictionaries">Справочники</a><a routerLink="/asset-classes">Классы активов</a></nav>
      @if (error) { <p class="feedback error">{{ error }}</p> }
      @if (notice) { <p class="feedback success">{{ notice }}</p> }

      @if (section === 'settings' && settings) {
        <form class="settings" (ngSubmit)="saveSettings()">
          <section class="panel tenant-admin-policy">
            <header><h2>Права администраторов</h2><p>Управление созданием новых учётных записей администраторами тенанта</p></header>
            <div class="tenant-admin-policy-content">
              @if (tenantPolicyLoading) {
                <p class="field-note" role="status">Загружаем текущее значение настройки…</p>
              } @else if (tenantPolicyError) {
                <p class="field-note error" role="alert">{{ tenantPolicyError }}</p>
              } @else {
                <label class="tenant-admin-policy-toggle">
                  <input type="checkbox" name="allowAdminsCreateAdmins" [checked]="allowAdminsCreateAdmins" (change)="onTenantPolicyToggle($event)" [disabled]="!canManageAdminCreationSetting || savingTenantPolicy" />
                  <span><strong>Разрешить администраторам создавать администраторов</strong><small>Если выключено, создать пользователя с ролью «Администратор» сможет только владелец тенанта.</small></span>
                </label>
                @if (!canManageAdminCreationSetting) {
                  <p class="field-note">Настройку может изменять только владелец тенанта.</p>
                } @else {
                  <button class="primary" type="button" [disabled]="savingTenantPolicy" (click)="saveTenantAdminCreationPolicy()">
                    {{ savingTenantPolicy ? 'Сохранение…' : 'Сохранить настройку' }}
                  </button>
                }
              }
            </div>
          </section>
          <section class="panel tenant-security-policy">
            <header><h2>Политики безопасности</h2><p>Двухфакторная аутентификация для сотрудников организации</p></header>
            <div class="field-grid">
              <label>Режим 2FA
                <select name="twoFactorMode" [(ngModel)]="twoFactorMode" [disabled]="!canManageAdminCreationSetting || savingTenantPolicy">
                  <option value="all">Обязательно для всех сотрудников (рекомендуется)</option>
                  <option value="admins">Обязательно только для администраторов</option>
                  <option value="optional">Необязательно, по желанию пользователя</option>
                </select>
              </label>
              <label>Льготный период
                <select name="twoFactorGraceDays" [(ngModel)]="twoFactorGraceDays" [disabled]="twoFactorMode === 'optional' || !canManageAdminCreationSetting || savingTenantPolicy">
                  <option [ngValue]="0">Без отсрочки — мгновенно</option>
                  <option [ngValue]="3">3 дня</option>
                  <option [ngValue]="7">7 дней</option>
                </select>
              </label>
            </div>
            @if (canManageAdminCreationSetting) {
              <button class="primary" type="button" [disabled]="savingTenantPolicy" (click)="saveTenantSecurityPolicy()">
                {{ savingTenantPolicy ? 'Сохранение…' : 'Сохранить политики безопасности' }}
              </button>
            } @else {
              <p class="field-note">Политики безопасности может изменять только владелец тенанта.</p>
            }
          </section>

          <section class="panel"><header><h2>Общие параметры</h2><p>Назначение, режим SLA и доступ портала</p></header><div class="field-grid">
            <label>Назначение исполнителя<select name="assignment" [(ngModel)]="settings.assignmentMode"><option value="automatic">Автоматическое: минимальная открытая очередь</option><option value="manual">Ручное</option></select></label>
            @if (settings.assignmentMode === 'manual') {
              <label>Ответственный за ручное назначение<select name="manualAssigner" [(ngModel)]="manualAssignerUserId"><option [ngValue]="null">Любой администратор или менеджер</option>@for (user of assignerOptions; track user.id) { <option [ngValue]="user.id">{{ user.fullName }}</option> }</select></label>
            }
            <label>Руководитель (инженер ТП)<select name="manager" [(ngModel)]="settings.manager">@for (engineer of settings.engineers ?? []; track engineer) { <option [value]="engineer">{{ engineer }}</option> }</select></label>
            <label>Поставка<select name="tenantMode" [(ngModel)]="settings.tenantMode"><option value="on-premise">On-premise</option><option value="saas">SaaS с изоляцией тенантов</option></select></label>
            <label>Учёт SLA<select name="slaMode" [(ngModel)]="settings.slaMode"><option value="24/7">Круглосуточно</option><option value="working-hours">По рабочему графику</option></select></label>
            @if (settings.slaMode === 'working-hours') { <label>Рабочий график<input name="workingHours" [(ngModel)]="settings.workingHours" /></label><div class="calendar-settings"><strong>Рабочие дни</strong><div class="day-list">@for (day of weekDays; track day) { <label class="check"><input type="checkbox" [name]="'workday-' + day" [checked]="settings.workingDays.includes(day)" (change)="toggleWorkingDay(day,$event)" /> {{ day }}</label> }</div><strong>Праздничные исключения</strong><div class="inline"><input type="date" name="newHoliday" [(ngModel)]="newHoliday" /><button class="quiet" type="button" [disabled]="!newHoliday" (click)="addHoliday()">Добавить дату</button></div><div class="holiday-list">@for (holiday of settings.holidays; track holiday) { <span>{{ holiday }}<button type="button" [attr.aria-label]="'Убрать праздник ' + holiday" (click)="removeHoliday(holiday)">×</button></span> }</div></div> }
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
            <div class="transition-matrix"><h3>Допустимые переходы</h3>@for (source of settings.statuses; track source.name) { <div class="transition-row"><strong>{{ source.name }}</strong><div>@for (target of settings.statuses; track target.name) { <label class="check"><input type="checkbox" [name]="'transition-' + source.name + '-' + target.name" [checked]="settings.statusTransitions[source.name]?.includes(target.name)" [disabled]="source.name === target.name || source.name === 'Закрыта' || target.name === 'Открыта'" (change)="toggleStatusTransition(source.name,target.name,$event)" /> {{ target.name }}</label> }</div></div> }</div>
          </section>

          <section class="panel"><header><h2>Типы заявок и SLA</h2><p>Нормативы времени реакции и решения задаются для сочетания типа и критичности</p></header>
            <div class="type-toolbar"><button class="quiet" type="button" (click)="openTicketTypeEditor()">+ Добавить тип заявки</button></div>
            <div class="type-list">@for (type of ticketTypes; track type.id) { <article class="type-row">
              <div class="type-top"><div class="type-name"><strong>{{ type.name }}</strong>@if (type.builtIn || type.repair) { <span class="builtin">Встроенный тип</span> }</div><label class="check"><input type="checkbox" [name]="'portal-' + type.id" [(ngModel)]="type.portalAvailable" [disabled]="type.builtIn || type.repair" (ngModelChange)="ticketTypeChangesDirty = true" /> Доступен на портале</label><label class="check"><input type="checkbox" [name]="'type-sla-' + type.id" [(ngModel)]="type.slaEnabled" (ngModelChange)="ticketTypeChangesDirty = true" /> SLA включён</label><div class="type-actions">@if (!type.builtIn && !type.repair) { <button class="icon-button" type="button" [attr.aria-label]="'Редактировать тип ' + type.name" (click)="openTicketTypeEditor(type)">✎</button><button class="remove-type" type="button" (click)="archiveType(type)">Архивировать</button> }</div></div>
              <div class="sla-grid">@for (level of settings.criticalities; track level) { <div class="sla-level"><strong>{{ level }}</strong><label>Реакция<input [name]="'reaction-' + type.id + '-' + level" [ngModel]="type.slaByCriticality?.[level]?.reaction || type.reactionSla" (ngModelChange)="setTypeSla(type,level,'reaction',$event)" /></label><label>Решение<input [name]="'resolution-' + type.id + '-' + level" [ngModel]="type.slaByCriticality?.[level]?.resolution || type.resolutionSla" (ngModelChange)="setTypeSla(type,level,'resolution',$event)" /></label></div> }</div>
            </article> }</div>
            @if (archivedTicketTypes.length) {
              <div class="archived-types"><h3>Архив типов заявок</h3>@for (type of archivedTicketTypes; track type.id) { <div><span>{{ type.name }}</span><button class="quiet" type="button" (click)="restoreType(type)">Восстановить</button><button class="remove-type" type="button" (click)="deleteArchivedType(type)">Удалить</button></div> }</div>
            }
            @if (ticketTypeChangesDirty) { <button class="quiet type-discard" type="button" (click)="discardTicketTypeChanges()">Отменить несохранённые изменения</button> }
            <button class="primary save-button" type="button" [disabled]="!ticketTypeChangesDirty" (click)="saveTicketTypeChanges()">Сохранить изменения типов</button>
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
        @if (ticketTypeEditorOpen && ticketTypeDraft; as draft) {
          <div class="type-modal-backdrop" (click)="closeTicketTypeEditor()">
            <section class="type-modal" role="dialog" aria-modal="true" [attr.aria-label]="editingTicketTypeId ? 'Редактировать тип заявки' : 'Добавить тип заявки'" (click)="$event.stopPropagation()">
              <header><div><p class="eyebrow">Тип заявки</p><h2>{{ editingTicketTypeId ? 'Редактирование типа' : 'Новый тип заявки' }}</h2></div><button class="icon-button" type="button" aria-label="Закрыть" (click)="closeTicketTypeEditor()">×</button></header>
              <div class="type-modal-fields">
                <label>Название<input name="ticketTypeNameDraft" [(ngModel)]="draft.name" (keydown.enter)="$event.preventDefault(); saveTicketTypeDraft()" autofocus /></label>
                <label class="check"><input type="checkbox" name="ticketTypePortalDraft" [(ngModel)]="draft.portalAvailable" /> Доступен на портале</label>
                <label class="check"><input type="checkbox" name="ticketTypeSlaDraft" [(ngModel)]="draft.slaEnabled" /> SLA включён</label>
              </div>
              <footer><button class="quiet" type="button" (click)="closeTicketTypeEditor()">Отмена</button><button class="primary" type="button" [disabled]="!draft.name.trim()" (click)="saveTicketTypeDraft()">Сохранить тип</button></footer>
            </section>
          </div>
        }
      }

      @if (section === 'forms') {
        <section class="panel"><header><h2>Формы по типам заявок</h2><p>Одна форма на тип; встроенные поля нельзя удалить</p></header><label class="form-select">Тип заявки<select name="formId" [(ngModel)]="formId" (ngModelChange)="loadForm($event)"><option value="">Выберите тип заявки</option>@for (type of formTicketTypes; track type.id) { <option [value]="type.id">{{ type.name }}</option> }</select></label>
          @if (formLoading) { <p class="empty">Загрузка схемы формы…</p> }
          @if (activeForm; as form) { <div class="fields">
            <div class="field-list" cdkDropList [cdkDropListData]="form.fields" (cdkDropListDropped)="moveField(form,$event)">
            <article class="field-row" *ngFor="let field of form.fields" cdkDrag>
              <button class="drag-handle" type="button" cdkDragHandle aria-label="Переместить поле" title="Перетащить поле">⠿</button>
              <div class="field-main"><label>Название<input [name]="'field-name-' + field.id" [(ngModel)]="field.name" [disabled]="field.immutable ?? false" /></label><label>Тип поля<select [name]="'field-type-' + field.id" [(ngModel)]="field.type" [disabled]="field.immutable ?? false"><option value="string">Строка</option><option value="textarea">Многострочный текст</option><option value="number">Число</option><option value="date">Дата</option><option value="dictionary">Справочник</option><option value="asset">Выбор актива</option><option value="file">Вложение</option><option value="boolean">Да/нет</option></select></label><label class="check"><input type="checkbox" [name]="'required-' + field.id" [(ngModel)]="field.required" [disabled]="field.immutable ?? false" /> Обязательное</label><label class="check"><input type="checkbox" [name]="'visible-' + field.id" [(ngModel)]="field.visibleToRequester" /> Видно автору</label></div>
              <div class="field-options"><label>Значение по умолчанию<input [name]="'default-' + field.id" [(ngModel)]="field.defaultValue" /></label><label>Проверка ввода<input [name]="'validation-' + field.id" [(ngModel)]="field.validation" placeholder="Например, регулярное выражение" /></label><label>Подсказка<input [name]="'hint-' + field.id" [(ngModel)]="field.hint" /></label>
                @if (field.type === 'dictionary') { <label>Значения списка<input [name]="'options-' + field.id" [ngModel]="field.options?.join(', ') || ''" (ngModelChange)="setOptions(field,$event)" placeholder="Значение 1, Значение 2" /></label> }
                @if (!field.immutable) { <label>Показывать, если поле<select [name]="'condition-field-' + field.id" [ngModel]="field.conditionFieldId || ''" (ngModelChange)="setConditionField(field,$event)"><option value="">Всегда показывать</option>@for (other of form.fields; track other.id) { @if (other.id !== field.id) { <option [value]="other.id">{{ other.name }}</option> } }</select></label>@if (field.conditionFieldId) { <label>Условие<select [name]="'condition-operator-' + field.id" [(ngModel)]="field.conditionOperator"><option value="equals">Равно</option><option value="notEquals">Не равно</option><option value="greaterThan" [disabled]="!conditionSourceIsNumeric(form,field)">Больше</option></select></label><label>Значение условия<input [name]="'condition-value-' + field.id" [(ngModel)]="field.conditionValue" placeholder="Введите значение" /></label> } }
              </div>
              @if (!field.immutable) { <button class="icon-action field-delete" type="button" [attr.aria-label]="'Удалить поле ' + field.name" title="Удалить поле" (click)="removeField(form,field)">×</button> }
            </article>
            </div>
            <div class="add-field"><label>Новое поле<input name="newFieldName" [(ngModel)]="newFieldName" placeholder="Название поля" /></label><label>Тип<select name="newFieldType" [(ngModel)]="newFieldType"><option value="string">Строка</option><option value="textarea">Многострочный текст</option><option value="number">Число</option><option value="date">Дата</option><option value="dictionary">Справочник</option><option value="asset" [disabled]="form.fields.some(field => field.type === 'asset')">Выбор актива</option><option value="file">Вложение</option><option value="boolean">Да/нет</option></select></label><button class="quiet align-end" type="button" [disabled]="!newFieldName.trim() || (newFieldType === 'asset' && form.fields.some(field => field.type === 'asset'))" (click)="addField(form)">＋ Добавить поле</button></div>
            <button class="primary save-button" type="button" [disabled]="savingFormIds.has(form.ticketTypeId)" (click)="saveForm(form)">{{ savingFormIds.has(form.ticketTypeId) ? 'Сохранение…' : 'Сохранить форму' }}</button>
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
        <section class="panel"><header><h2>Отчёты Service Desk</h2><p>Данные загружаются из базы. Период применяется к отчетам по событиям; статусы, нагрузка и этапы активов показывают текущий срез.</p></header><div class="period"><label>С даты<input type="date" name="from" [(ngModel)]="reportFrom" /></label><label>По дату<input type="date" name="to" [(ngModel)]="reportTo" /></label><button class="quiet" type="button" [disabled]="reportLoading" (click)="loadReports()">Обновить</button></div>
          @if (reportError) { <p class="feedback error" role="alert">{{ reportError }}</p> }
          @if (reportLoading && !reports.length) { <p class="empty" role="status">Загрузка отчётов…</p> }
          @else if (!reportLoading && !reports.length) { <p class="empty">Нет данных для отображения</p> }
          @else { <div class="report-list">@for (report of reports; track report.id) { <article><button type="button" class="report-open" (click)="openReport(report)"><span class="report-icon" aria-hidden="true">▤</span><span class="report-copy"><strong>{{ report.title }}</strong><small>{{ report.description }}</small></span><b>{{ report.count ?? report.value }}</b></button><button class="quiet" type="button" [disabled]="downloadingReportId !== null" [attr.aria-label]="'Скачать CSV: ' + report.title" (click)="downloadCsv(report.id)">↓ CSV</button></article> }</div> }
        </section>
        @if (reportDetailOpen) {
          <div class="report-modal-backdrop" (click)="closeReport()">
            <section class="report-modal" role="dialog" aria-modal="true" aria-label="Детализация отчета" (click)="$event.stopPropagation()">
              <header><div><p class="eyebrow">Детализация отчета</p><h2>{{ activeReport?.title }}</h2></div><button class="icon-button" type="button" aria-label="Закрыть" (click)="closeReport()">×</button></header>
              @if (reportDetail) {
                @if (activeReport?.id === 'repairs' && repairOutcomeSlices.length) {
                  <section class="repair-outcomes" aria-label="Распределение результатов ремонта">
                    <div class="repair-pie" role="img" aria-label="Диаграмма результатов ремонта" [style.background]="repairOutcomeGradient"></div>
                    <ul>@for (slice of repairOutcomeSlices; track slice.label) { <li><span [style.background-color]="slice.color"></span>{{ slice.label }} <b>{{ slice.value }}</b></li> }</ul>
                  </section>
                }
                <div class="report-table-wrap"><table><thead><tr>@for (column of reportDetail.columns; track column.key) { <th>{{ column.label }}</th> }</tr></thead><tbody>@for (item of reportDetail.items; track $index) { <tr>@for (column of reportDetail!.columns; track column.key) { <td>{{ item.values[column.key] ?? '—' }}</td> }</tr> } @if (!reportDetail.items.length) { <tr><td [attr.colspan]="reportDetail.columns.length" class="empty">Нет данных за выбранный период</td></tr> }</tbody></table></div>
                <footer><span>Всего: {{ reportDetail.total }}</span><div><button class="quiet" type="button" [disabled]="reportDetail.skip === 0" (click)="changeReportPage(-1)">Назад</button><button class="quiet" type="button" [disabled]="reportDetail.skip + reportDetail.take >= reportDetail.total" (click)="changeReportPage(1)">Далее</button></div></footer>
              } @else if (reportError) { <p class="feedback error" role="alert">{{ reportError }}</p> } @else if (reportDetailLoading) { <p class="empty" role="status">Загрузка данных…</p> } @else { <p class="empty">Нет данных за выбранный период</p> }
            </section>
          </div>
        }
      }

      @if (section === 'users') {
        <section class="panel"><header><h2>Управление пользователями</h2><p>Просмотр, редактирование и управление учётными записями пользователей</p></header>
          @if (quotaError) { <p class="feedback error">{{ quotaError }}</p> }
          @if (userQuota?.usersOverLimit) {
            <aside class="feedback error" role="alert">
              <strong>Превышен лимит пользователей тенанта «{{ userQuota.tenantName }}»</strong>
              <p>Активных пользователей: {{ userQuota.activeUserCount }} из {{ userQuota.maxUsers }}. Нужно освободить {{ userQuota.usersOverLimit }} мест.</p>
              @if (userQuota.graceEndsAt && quotaDaysRemaining > 0) {
                <p>Если не освободить места до {{ userQuota.graceEndsAt | date:'short' }}, система деактивирует до {{ usersScheduledForDeactivation }} случайных пользователей. Администраторы защищены.</p>
              } @else if (userQuota.graceEndsAt) {
                <p>Срок освобождения мест истёк. Деактивация выполнена для доступных пользователей; администраторы защищены. Остаток превышения: {{ userQuota.usersOverLimit }}.</p>
              } @else {
                <p>Начинается 7-дневный срок для освобождения мест.</p>
              }
            </aside>
          }
          <div class="users-toolbar"><button class="quiet" type="button" routerLink="/admin/users/new">+ Добавить пользователя</button></div>
          @if (usersLoading) {
            <p class="empty" style="padding:16px;">Загрузка пользователей…</p>
          } @else if (usersList.length) {
            <button
              class="mobile-filters-toggle"
              type="button"
              aria-controls="users-filters"
              [attr.aria-expanded]="usersFiltersExpanded"
              (click)="usersFiltersExpanded = !usersFiltersExpanded">
              Filters
            </button>
            <div id="users-filters" class="users-filters" [class.filters-expanded]="usersFiltersExpanded" aria-label="Фильтры пользователей">
              <label class="users-search">Поиск
                <input type="search" name="usersSearch" [(ngModel)]="usersSearch" placeholder="ФИО, email, username..." />
              </label>
              <label>Департамент
                <select name="usersDepartment" [(ngModel)]="usersDepartmentFilter">
                  <option value="">Все департаменты</option>
                  @for (department of userDepartments; track department) { <option [value]="department">{{ department }}</option> }
                </select>
              </label>
              <label>Роль
                <select name="usersRole" [(ngModel)]="usersRoleFilter">
                  <option value="">Все роли</option>
                  @for (role of availableRoles; track role.id) { <option [value]="role.id">{{ role.name }}</option> }
                </select>
              </label>
              <label>Статус
                <select name="usersStatus" [(ngModel)]="usersStatusFilter">
                  <option value="">Все статусы</option>
                  <option value="active">Активные</option>
                  <option value="inactive">Неактивные</option>
                </select>
              </label>
              @if (hasUserFilters) { <button class="clear-user-filters" type="button" (click)="clearUserFilters()">Сбросить</button> }
              <span class="users-count" aria-live="polite">Показано {{ filteredUsers.length }} из {{ usersList.length }}</span>
            </div>
          }
          @if (!usersLoading && filteredUsers.length) {
            <table class="users-table">
              <thead>
                <tr><th>ФИО</th><th>Email</th><th>Username</th><th>Департамент</th><th>Руководитель</th><th>Статус</th><th>Роли</th><th>2FA</th><th></th></tr>
              </thead>
              <tbody>
                @for (user of filteredUsers; track user.id) {
                  @if (editingUserId === user.id) {
                    <tr class="edit-row mobile-expanded">
                      <td data-label="ФИО"><input name="fullName" [(ngModel)]="editingUser!.fullName" /></td>
                      <td data-label="Email"><input name="email" type="email" [(ngModel)]="editingUser!.email" /></td>
                      <td data-label="Username"><input name="username" [(ngModel)]="editingUser!.username" /></td>
                      <td data-label="Департамент"><input name="department" [(ngModel)]="editingUser!.department" /></td>
                      <td data-label="Руководитель"><select name="managerId" [(ngModel)]="editingUser!.managerId"><option [ngValue]="null">Не назначен</option>@for (manager of usersList.filter(u => u.id !== user.id && u.isActive); track manager.id) { <option [ngValue]="manager.id">{{ manager.fullName }}</option> }</select></td>
                      <td data-label="Статус"><label class="check"><input type="checkbox" [(ngModel)]="editingUser!.isActive" [disabled]="isSelfTenantOwner(user)" /><span>Активен</span></label></td>
                      <td class="roles-cell" data-label="Роли"><div class="roles-list">@for (role of assignableRoles; track role.id) { <label class="role-check"><input type="radio" [name]="'edit-role-' + user.id" [checked]="(editingUser?.roleIds || []).includes(role.id)" (change)="toggleEditingUserRole(role.id, $event)" [disabled]="role.code === 'admin' && !canCreateAdminRoleForCurrentUser" /><span>{{ role.name }}@if (role.code === 'admin' && !canCreateAdminRoleForCurrentUser) { <small> — Создание админа недоступно. Обратитесь к владельцу тенанта</small> }</span></label> }</div></td>
                      <td data-label="2FA"><span class="mfa-badge" [class.enabled]="user.isTwoFactorEnabled">{{ user.isTwoFactorEnabled ? 'Активна' : 'Не настроена' }}</span></td>
                      <td class="actions" data-label="Действия"><button type="button" aria-label="Сохранить пользователя" (click)="saveUser(user.id)">✓</button><button type="button" aria-label="Отменить редактирование" (click)="cancelEdit()">✕</button></td>
                    </tr>
                  } @else {
                    <tr [class.mobile-expanded]="expandedUserId === user.id">
                      <td data-label="ФИО">
                        <span class="desktop-user-name">{{ user.fullName }}</span>
                        <button
                          class="mobile-user-toggle"
                          type="button"
                          [attr.aria-expanded]="expandedUserId === user.id"
                          [attr.aria-label]="(expandedUserId === user.id ? 'Скрыть данные пользователя ' : 'Показать данные пользователя ') + user.fullName"
                          (click)="toggleUserDetails(user.id)">
                            <span class="mobile-user-name">{{ user.fullName }}</span><span class="mobile-expand-indicator" aria-hidden="true">⌄</span>
                        </button>
                      </td>
                      <td data-label="Email">{{ user.email }}</td>
                      <td data-label="Username">{{ user.username }}</td>
                      <td data-label="Департамент">{{ user.department }}</td>
                      <td data-label="Руководитель">{{ findUserName(user.managerId) || '—' }}</td>
                      <td data-label="Статус"><span class="status" [class.active]="user.isActive" [class.inactive]="!user.isActive">{{ user.isActive ? 'Активен' : 'Неактивен' }}</span></td>
                      <td data-label="Роли"><span class="role-tags">@if (isTenantOwner(user)) { <span class="owner-badge">Владелец</span> }@for (role of getUserRoles(user); track role.id) { @if (role.code !== 'tenant_owner') { <span>{{ role.name }}</span> } }@if (!user.roleIds.length) { — }</span></td>
                      <td data-label="2FA"><span class="mfa-badge" [class.enabled]="user.isTwoFactorEnabled">{{ user.isTwoFactorEnabled ? 'Активна' : 'Не настроена' }}</span></td>
                      <td class="actions" data-label="Действия">
                        <button type="button" [disabled]="!canEditUser(user)" [title]="canEditUser(user) ? 'Редактировать пользователя' : 'Изменять владельца может только глобальный администратор'" [attr.aria-label]="'Редактировать пользователя ' + user.fullName" (click)="editUser(user)">✎</button>
                        @if (canResetUserPassword(user)) {
                          <button type="button" [title]="'Сбросить пароль для ' + user.fullName" [attr.aria-label]="'Сбросить пароль для ' + user.fullName" (click)="resetUserPassword(user)">🔑</button>
                        }
                        @if (user.isTwoFactorEnabled && canResetUserMfa(user)) {
                          <button type="button" [title]="'Сбросить 2FA для ' + user.fullName" [attr.aria-label]="'Сбросить 2FA для ' + user.fullName" (click)="resetUserMfa(user)">↻</button>
                        }
                      </td>
                    </tr>
                  }
                }
              </tbody>
            </table>
          } @else if (!usersLoading) {
            @if (usersList.length && hasUserFilters) {
              <p class="empty" style="padding:16px;">По заданным условиям пользователи не найдены</p>
            } @else {
            <p class="empty" style="padding:16px;">Пользователей не найдено</p>
            }
          }
        </section>
      }
    </section>
  `,
  styles: [
      `
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
      .calendar-settings { grid-column:1/-1; display:grid; gap:8px; padding:12px; border-radius:5px; background:#f7f9fb; }
      .calendar-settings > strong { color:#586475; font-size:9px; }
      .day-list { display:flex; flex-wrap:wrap; gap:10px; }
      .holiday-list { display:flex; flex-wrap:wrap; gap:6px; }
      .holiday-list span { display:flex; align-items:center; gap:5px; padding:4px 7px; border-radius:4px; background:#fff; color:#576273; font-size:9px; }
      .holiday-list button { width:18px; height:18px; border:0; background:transparent; color:#a34a4a; cursor:pointer; }
      .check { display:flex; align-items:center; gap:6px; min-height:30px; color:#5e6978; font-size:9px; }
      .list-editor,.status-list,.type-list,.policy-list,.fields,.report-list { padding:4px 16px 12px; }
      .list-editor { display:grid; grid-template-columns:repeat(3,minmax(0,1fr)); gap:11px; padding-top:13px; }
      .inline { display:flex; gap:7px; align-items:center; } .inline input { flex:1; }
      .icon-action { width:28px; height:28px; border:0; border-radius:4px; background:#fff2f1; color:#a94343; cursor:pointer; }
      .icon-action:disabled { opacity:.4; cursor:not-allowed; }
      .add-row { margin:0 16px 15px; } .quiet { min-height:32px; padding:0 10px; border:1px solid #dfe4ea; border-radius:4px; background:#f8f9fb; color:#536173; font-size:9px; cursor:pointer; white-space:nowrap; }
      .quiet:disabled { opacity:.5; cursor:not-allowed; } .status-row { display:grid; grid-template-columns:minmax(140px,1fr) 1fr 28px; align-items:center; gap:12px; padding:8px 0; border-bottom:1px solid #f0f2f5; }
      .transition-matrix { overflow-x:auto; padding:4px 16px 16px; } .transition-matrix h3 { margin:0 0 9px; color:#576273; font-size:10px; }
      .transition-row { display:grid; grid-template-columns:150px minmax(max-content,1fr); gap:12px; align-items:start; padding:8px 0; border-top:1px solid #f0f2f5; }
      .transition-row > strong { padding-top:6px; color:#475363; font-size:9px; } .transition-row > div { display:flex; gap:10px; }
      .transition-row .check { min-height:25px; white-space:nowrap; }
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
      .report-open { display:flex; align-items:center; gap:12px; flex:1; min-width:0; padding:0; border:0; background:transparent; color:inherit; text-align:left; cursor:pointer; }
      .report-icon { width:30px; height:30px; display:grid; place-items:center; border-radius:5px; background:#edf4ff; color:#3270cb; }
      .report-copy { display:grid; gap:4px; min-width:0; flex:1; } .report-copy strong { font-size:10px; } .report-copy small { color:#84909d; font-size:9px; } .report-open b { color:#4a5665; font-size:10px; white-space:nowrap; }
      .empty { padding:10px 16px; color:#8993a1; font-size:10px; }
      .users-toolbar { padding:12px 16px; border-bottom:1px solid #edf0f4; } .users-toolbar button { min-height:32px; }
      .mobile-filters-toggle { display:none; }
      .users-filters { display:grid; grid-template-columns:minmax(200px,2fr) repeat(3,minmax(140px,1fr)) auto; align-items:end; gap:10px; padding:12px 16px; border-bottom:1px solid #edf0f4; }
      .users-filters label { min-width:0; }
      .users-filters input, .users-filters select { width:100%; min-height:36px; }
      .users-search { grid-column:span 1; }
      .clear-user-filters { min-height:36px; padding:0 10px; border:1px solid #dfe4ea; border-radius:4px; background:#fff; color:#536173; cursor:pointer; }
      .users-count { grid-column:1/-1; color:#8993a1; font-size:9px; }
      .users-table { width:100%; border-collapse:collapse; font-size:9px; }
      .users-table thead { background:#f7f9fb; }
      .users-table th { padding:10px 8px; text-align:left; color:#546173; font-weight:600; border-bottom:1px solid #dfe4ea; }
      .users-table td { padding:10px 8px; border-bottom:1px solid #eef1f4; color:#414c5a; }
      .users-table .edit-row input, .users-table .edit-row select { width:100%; min-height:28px; padding:4px 6px; border:1px solid #dfe4ea; border-radius:3px; }
      .users-table .edit-row td { padding:8px 4px; }
      .users-table .status { display:inline-block; padding:3px 6px; border-radius:3px; font-size:8px; font-weight:600; }
      .users-table .status.active { background:#e6f7f0; color:#176f52; }
      .users-table .status.inactive { background:#ffe9e9; color:#a93232; }
      .users-table .roles-cell { max-width:180px; }
      .roles-list { display:grid; gap:4px; }
      .role-check { display:flex; align-items:center; gap:4px; min-height:24px; }
      .role-check input { margin:0; }
      .role-check span { font-size:8px; color:#546173; }
      .users-table .actions { text-align:center; }
      .users-table button { width:24px; height:24px; margin:0 2px; padding:0; border:0; border-radius:3px; background:#f0f2f5; color:#546173; cursor:pointer; font-size:9px; }
      .users-table button:hover { background:#e2e7ed; }
      .mobile-user-toggle { display:none; }
      @media(max-width:950px) { .policy-row { grid-template-columns:repeat(2,minmax(0,1fr)); } .type-top { grid-template-columns:1fr 1fr; } .sla-grid { grid-template-columns:1fr; } .users-table { font-size:8px; } .users-table th, .users-table td { padding:6px 4px; } }
    `],
})
export class AdminWorkspaceComponent implements OnInit {
    private readonly workspace = inject(WorkspaceService);
    private readonly tenantSettingsApi = inject(HttpWorkspaceApiService);
    private readonly admin = inject(AdminService);
    private readonly auth = inject(AuthService);
    private readonly quotaService = inject(TenantUserQuotaService);
    private readonly route = inject(ActivatedRoute);
    private readonly router = inject(Router);
    private readonly changeDetector = inject(ChangeDetectorRef);

    section: AdminSection = 'settings';
    settings: AdminSettings | null = null;
    manualAssignerUserId: number | null = null;
    assignerOptions: { id: number; fullName: string }[] = [];
    ticketTypes: TicketType[] = [];
    forms: TicketFormDefinition[] = [];
    formTicketTypes: TicketFormType[] = [];
    formId = '';
    formLoading = false;
    private readonly formDrafts = new Map<string, TicketFormDefinition>();
    private readonly savedFormDrafts = new Map<string, TicketFormDefinition>();
    private formRequestVersion = 0;
    archivedTicketTypes: TicketType[] = [];
    ticketTypeEditorOpen = false;
    ticketTypeDraft: TicketType | null = null;
    editingTicketTypeId: string | null = null;
    ticketTypeChangesDirty = false;
    private savedTicketTypes: TicketType[] = [];
    private savedArchivedTicketTypes: TicketType[] = [];
    private localIdSequence = 0;
    policies: EscalationPolicy[] = [];
    escalationHistory: EscalationHistoryEntry[] = [];
    reports: ReportSummary[] = [];
    reportDetail: ReportDetail | null = null;
    activeReport: ReportSummary | null = null;
    reportDetailOpen = false;
    reportLoading = false;
    reportDetailLoading = false;
    reportError = '';
    downloadingReportId: string | null = null;
    reportDetailSkip = 0;
    reportDetailTake = 30;
    private reportsRequestId = 0;
    private reportDetailRequestId = 0;
    usersList: UserSummary[] = [];
    usersLoading = false;
    userQuota: TenantUserQuota | null = null;
    quotaError = '';
    usersSearch = '';
    usersDepartmentFilter = '';
    usersRoleFilter = '';
    usersStatusFilter = '';
    usersFiltersExpanded = false;
    expandedUserId: number | null = null;
    availableRoles: RoleSummary[] = [];
    editingUserId: number | null = null;
    editingUser: Partial<UpdateAdminUserRequest> | null = null;
    savingTenantPolicy = false;
    tenantPolicyLoading = false;
    tenantPolicyLoaded = false;
    tenantPolicyError = '';
    allowAdminsCreateAdmins = false;
    twoFactorMode: 'all' | 'admins' | 'optional' = 'all';
    twoFactorGraceDays = 0;
    error = '';
    notice = '';
    readonly savingFormIds = new Set<string>();
    newCriticality = '';
    newStatus = '';
    newUsername = '';
    newPassword = '';
    newFieldName = '';
    newFieldType: FormFieldDefinition['type'] = 'string';
    reportFrom = '';
    reportTo = '';
    newHoliday = '';
    readonly weekDays = ['Пн', 'Вт', 'Ср', 'Чт', 'Пт', 'Сб', 'Вс'];

    get assignableRoles(): RoleSummary[] { return this.availableRoles.filter(role => ['admin', 'manager', 'agent', 'portal_user'].includes(role.code)); }
    get canManageAdminCreationSetting(): boolean { return this.auth.isTenantOwner(); }
    get canCreateAdminRoleForCurrentUser(): boolean {
        if (this.auth.isTenantOwner() || this.auth.isTenantAdmin()) return true;
        return this.auth.isAdmin() && this.allowAdminsCreateAdmins;
    }
    get pageTitle(): string { return { settings: 'Настройки Service Desk', forms: 'Конструктор форм', escalations: 'Процесс эскалации', reports: 'Отчёты', users: 'Управление пользователями' }[this.section]; }
    get repairOutcomeSlices(): { label: string; value: number; color: string; percentage: number }[] {
        const colors: Record<string, string> = {
            'В эксплуатацию': '#22a06b',
            'На склад': '#3978d4',
            'Списать': '#d95757',
            'Не указан': '#8792a0',
        };
        const aggregates = Object.entries(this.reportDetail?.aggregates ?? {}).filter(([, value]) => value > 0);
        const total = aggregates.reduce((sum, [, value]) => sum + value, 0);
        return aggregates.map(([label, value], index) => ({
            label,
            value,
            color: colors[label] ?? ['#22a06b', '#3978d4', '#d95757', '#8792a0'][index % 4],
            percentage: value * 100 / total,
        }));
    }
    get repairOutcomeGradient(): string {
        let current = 0;
        const stops = this.repairOutcomeSlices.map(slice => {
            const start = current;
            current += slice.percentage;
            return `${slice.color} ${start}% ${current}%`;
        });
        return stops.length ? `conic-gradient(${stops.join(', ')})` : '#e5e9ef';
    }
    get activeForm(): TicketFormDefinition | undefined { return this.forms.find(form => form.ticketTypeId === this.formId); }
    get userDepartments(): string[] {
        return [...new Set(this.usersList.map(user => user.department?.trim()).filter((department): department is string => !!department))]
            .sort((a, b) => a.localeCompare(b, 'ru'));
    }
    get filteredUsers(): UserSummary[] {
        const search = this.usersSearch.trim().toLocaleLowerCase();
        const roleId = this.usersRoleFilter ? Number(this.usersRoleFilter) : null;

        return this.usersList.filter(user => {
            const manager = this.findUserName(user.managerId) ?? '';
            const roleNames = user.roleIds
                .map(id => this.availableRoles.find(role => role.id === id))
                .filter((role): role is RoleSummary => role !== undefined)
                .flatMap(role => [role.name, role.code]);
            const searchFields = [user.fullName, user.email, user.username, user.department ?? '', manager, ...roleNames];

            return (!search || searchFields.some(value => value.toLocaleLowerCase().includes(search)))
                && (!this.usersDepartmentFilter || user.department === this.usersDepartmentFilter)
                && (roleId === null || user.roleIds.includes(roleId))
                && (!this.usersStatusFilter
                    || (this.usersStatusFilter === 'active' ? user.isActive : !user.isActive));
        });
    }
    get hasUserFilters(): boolean {
        return !!(this.usersSearch.trim() || this.usersDepartmentFilter || this.usersRoleFilter || this.usersStatusFilter);
    }
    get quotaDaysRemaining(): number {
        if (!this.userQuota?.graceEndsAt) return 0;
        return Math.max(0, Math.ceil((Date.parse(this.userQuota.graceEndsAt) - Date.now()) / 86_400_000));
    }
    get usersScheduledForDeactivation(): number {
        if (!this.userQuota) return 0;
        return Math.min(this.userQuota.usersOverLimit, this.userQuota.usersEligibleForDeactivation);
    }

    ngOnInit(): void {
        this.section = this.route.snapshot.data['section'] as AdminSection;
        if (this.section === 'reports') {
            const today = new Date();
            const weekAgo = new Date(today);
            weekAgo.setDate(today.getDate() - 6);
            this.reportFrom = this.toDateInput(weekAgo);
            this.reportTo = this.toDateInput(today);
        }
        if (this.section === 'users') {
            const createdUser = this.router.getCurrentNavigation()?.extras.state?.['userCreated'];
            if (typeof createdUser === 'string') {
                this.notice = `Пользователь ${createdUser} успешно создан.`;
            }
        }
        this.workspace.getAdminSettings().subscribe({
            next: settings => this.settings = settings,
            error: err => this.error = err.message,
        });
        if (this.section === 'settings') {
            this.workspace.getAssignmentSetting().subscribe({
                next: assignment => {
                    this.manualAssignerUserId = assignment.manualAssignerUserId ?? null;
                    if (!this.settings) return;
                    this.settings = {
                        ...this.settings,
                        assignmentMode: assignment.mode === 'manual' ? 'manual' : 'automatic',
                    };
                },
                error: err => this.error = err?.error?.detail ?? err.message,
            });
            this.workspace.getUsers().subscribe({
                next: users => {
                    this.assignerOptions = users
                        .filter(user => user.isActive && (user.roleIds.includes(2) || user.roleIds.includes(4)))
                        .map(user => ({ id: user.id, fullName: user.fullName }));
                },
                error: () => this.assignerOptions = [],
            });
        }
        if (this.section === 'settings' || this.section === 'users') {
            this.tenantPolicyLoading = true;
            this.tenantPolicyError = '';
            this.tenantSettingsApi.getTenantAdminCreationPolicy().subscribe({
                next: policy => {
                    this.allowAdminsCreateAdmins = policy.allowAdminsCreateAdmins;
                    this.twoFactorMode = policy.twoFactorMode ?? 'all';
                    this.twoFactorGraceDays = policy.twoFactorGraceDays ?? 0;
                    this.tenantPolicyLoaded = true;
                    this.tenantPolicyLoading = false;
                    this.changeDetector.markForCheck();
                },
                error: err => {
                    this.tenantPolicyError = err.message || 'Не удалось загрузить настройки тенанта.';
                    this.tenantPolicyLoading = false;
                    this.changeDetector.markForCheck();
                },
            });
        }
        if (this.section === 'settings') {
            if (this.workspace.supportsTicketTypeConfiguration) {
                this.workspace.getTicketTypes().subscribe({
                    next: types => {
                        this.ticketTypes = types;
                        this.savedTicketTypes = this.cloneTicketTypes(types);
                        this.savedArchivedTicketTypes = [];
                    },
                    error: err => this.error = err.message,
                });
            } else {
                this.workspace.getFormTicketTypes().subscribe({
                    next: types => {
                        this.ticketTypes = types.map(type => ({
                            id: type.id,
                            name: type.name,
                            portalAvailable: type.isPortalAvailable ?? true,
                            repair: type.code === 'repair',
                            builtIn: type.isBuiltIn ?? type.code === 'repair',
                            slaEnabled: true,
                            reactionSla: '1 ч',
                            resolutionSla: '8 ч',
                        }));
                        this.savedTicketTypes = this.cloneTicketTypes(this.ticketTypes);
                        this.savedArchivedTicketTypes = [];
                        this.changeDetector.markForCheck();
                    },
                    error: err => {
                        this.error = err.message;
                        this.changeDetector.markForCheck();
                    },
                });
            }
        }
        if (this.section === 'forms') this.workspace.getFormTicketTypes().subscribe({
            next: types => {
                this.formTicketTypes = types;
                this.formId = types[0]?.id ?? '';
                if (this.formId) this.loadForm(this.formId);
                this.changeDetector.markForCheck();
            },
            error: err => {
                this.error = err.message;
                this.changeDetector.markForCheck();
            },
        });
        if (this.section === 'escalations') {
            this.workspace.getEscalationPolicies().subscribe({ next: policies => this.policies = policies, error: err => this.error = err.message });
            this.workspace.getEscalationHistory().subscribe({ next: history => this.escalationHistory = history, error: err => this.error = err.message });
        }
        if (this.section === 'reports') this.loadReports();
        if (this.section === 'users') {
            this.usersLoading = true;
            this.error = '';
            this.loadUserQuota();
            this.admin.getUsers().pipe(finalize(() => {
                this.usersLoading = false;
                this.changeDetector.markForCheck();
            })).subscribe({
                next: users => {
                    this.usersList = users;
                    this.changeDetector.markForCheck();
                },
                error: err => {
                    this.error = err.message;
                    this.changeDetector.markForCheck();
                },
            });
            this.admin.getRoles().subscribe({ next: roles => this.availableRoles = roles, error: err => this.error = err.message });
        }
    }

    saveSettings(): void {
        const currentSettings = this.settings;
        if (!currentSettings) return;
        this.workspace.saveAdminSettings({
            ...currentSettings,
            allowAdminsCreateAdmins: this.allowAdminsCreateAdmins,
        }).subscribe({
            next: settings => {
                this.settings = settings;
                this.notice = 'Настройки сохранены.';
            },
            error: err => this.error = err.message,
        });
        this.workspace.saveAssignmentSettingWithAssigner(
            currentSettings.assignmentMode === 'manual' ? 'manual' : 'auto',
            currentSettings.assignmentMode === 'manual' ? this.manualAssignerUserId : null,
        ).subscribe({
            next: assignment => {
                this.manualAssignerUserId = assignment.manualAssignerUserId ?? null;
            },
            error: err => this.error = err?.error?.detail ?? err.message,
        });
        if (this.workspace.supportsTicketTypeConfiguration) {
            this.workspace.saveTicketTypes(this.ticketTypes).subscribe({
                next: types => this.ticketTypes = types,
                error: err => this.error = err.message,
            });
        }
    }

    saveTenantAdminCreationPolicy(): void {
        if (!this.tenantPolicyLoaded || !this.canManageAdminCreationSetting || this.savingTenantPolicy) return;
        this.savingTenantPolicy = true;
        this.tenantPolicyError = '';
        this.tenantSettingsApi.saveTenantAdminCreationPolicy(this.allowAdminsCreateAdmins).subscribe({
            next: policy => {
                this.allowAdminsCreateAdmins = policy.allowAdminsCreateAdmins;
                this.notice = 'Настройка прав администраторов сохранена.';
                this.savingTenantPolicy = false;
                this.changeDetector.markForCheck();
            },
            error: err => {
                this.tenantPolicyError = err.message || 'Не удалось сохранить настройку прав администраторов.';
                this.savingTenantPolicy = false;
                this.changeDetector.markForCheck();
            },
        });
    }

    onTenantPolicyToggle(event: Event): void {
        this.allowAdminsCreateAdmins = (event.target as HTMLInputElement).checked;
    }

    saveTenantSecurityPolicy(): void {
        if (!this.tenantPolicyLoaded || !this.canManageAdminCreationSetting || this.savingTenantPolicy) return;
        this.savingTenantPolicy = true;
        this.tenantPolicyError = '';
        this.tenantSettingsApi.saveTenantSecurityPolicy(
            this.twoFactorMode,
            this.twoFactorMode === 'optional' ? 0 : this.twoFactorGraceDays,
        ).subscribe({
            next: policy => {
                this.twoFactorMode = policy.twoFactorMode ?? 'all';
                this.twoFactorGraceDays = policy.twoFactorGraceDays ?? 0;
                this.notice = 'Политики безопасности сохранены.';
                this.savingTenantPolicy = false;
                this.changeDetector.markForCheck();
            },
            error: err => {
                this.tenantPolicyError = err.message || 'Не удалось сохранить политики безопасности.';
                this.savingTenantPolicy = false;
                this.changeDetector.markForCheck();
            },
        });
    }

    addCriticality(): void { if (!this.settings || this.settings.criticalities.length >= 5 || !this.newCriticality.trim()) return; this.settings.criticalities.push(this.newCriticality.trim()); this.newCriticality = ''; }
    removeCriticality(index: number): void { if (this.settings && this.settings.criticalities.length > 3) this.settings.criticalities.splice(index, 1); }
    addStatus(): void { if (!this.settings || !this.newStatus.trim()) return; const name = this.newStatus.trim(); this.settings.statuses.push({ name, pausesSla: false, immutable: false }); this.settings.statusTransitions[name] = []; this.newStatus = ''; }
    removeStatus(index: number): void {
      if (!this.settings || this.settings.statuses[index].immutable) return;
      const [removed] = this.settings.statuses.splice(index, 1);
      delete this.settings.statusTransitions[removed.name];
      for (const transitions of Object.values(this.settings.statusTransitions)) {
        const transitionIndex = transitions.indexOf(removed.name);
        if (transitionIndex >= 0) transitions.splice(transitionIndex, 1);
      }
    }
    toggleStatusTransition(source: string, target: string, event: Event): void {
      if (!this.settings) return;
      const allowed = this.settings.statusTransitions[source] ?? [];
      const enabled = (event.target as HTMLInputElement).checked;
      this.settings.statusTransitions[source] = enabled ? [...new Set([...allowed, target])] : allowed.filter(status => status !== target);
    }
    openTicketTypeEditor(type?: TicketType): void {
        if (type?.builtIn || type?.repair) return;
        this.editingTicketTypeId = type?.id ?? null;
        this.ticketTypeDraft = type
            ? this.cloneTicketType(type)
            : {
                id: '',
                name: '',
                portalAvailable: true,
                repair: false,
                builtIn: false,
                slaEnabled: true,
                reactionSla: '1 ч',
                resolutionSla: '8 ч',
                slaByCriticality: Object.fromEntries(
                    (this.settings?.criticalities ?? []).map(level => [level, { reaction: '1 ч', resolution: '8 ч' }]),
                ),
            };
        this.ticketTypeEditorOpen = true;
    }

    closeTicketTypeEditor(): void {
        this.ticketTypeEditorOpen = false;
        this.ticketTypeDraft = null;
        this.editingTicketTypeId = null;
    }

    saveTicketTypeDraft(): void {
        const draft = this.ticketTypeDraft;
        const name = draft?.name.trim();
        if (!draft || !name) return;
        if (this.ticketTypes.some(type => type.id !== this.editingTicketTypeId && type.name.trim().toLocaleLowerCase() === name.toLocaleLowerCase())) {
            this.error = 'Тип заявки с таким названием уже существует.';
            return;
        }

        const isNewType = this.editingTicketTypeId === null;
        const type = {
            ...draft,
            id: this.editingTicketTypeId ?? `local-type-${Date.now()}-${++this.localIdSequence}`,
            name,
        };
        if (isNewType) {
            this.ticketTypes = [...this.ticketTypes, type];
            this.formDrafts.set(type.id, {
                ticketTypeId: type.id,
                ticketTypeName: type.name,
                fields: this.defaultFields(type.id),
            });
        } else {
            this.ticketTypes = this.ticketTypes.map(item => item.id === type.id ? type : item);
            const formDraft = this.formDrafts.get(type.id);
            if (formDraft) formDraft.ticketTypeName = type.name;
        }

        this.ticketTypeChangesDirty = true;
        this.syncFormTicketTypes();
        this.closeTicketTypeEditor();
        this.error = '';
    }

    archiveType(type: TicketType): void {
        if (type.builtIn || type.repair || !this.ticketTypes.some(item => item.id === type.id)) return;
        this.ticketTypes = this.ticketTypes.filter(item => item.id !== type.id);
        this.archivedTicketTypes = [...this.archivedTicketTypes, type];
        this.ticketTypeChangesDirty = true;
        this.syncFormTicketTypes();
    }

    restoreType(type: TicketType): void {
        if (this.ticketTypes.some(item => item.name.trim().toLocaleLowerCase() === type.name.trim().toLocaleLowerCase())) {
            this.error = 'Тип заявки с таким названием уже существует.';
            return;
        }
        this.archivedTicketTypes = this.archivedTicketTypes.filter(item => item.id !== type.id);
        this.ticketTypes = [...this.ticketTypes, type];
        this.ticketTypeChangesDirty = true;
        this.syncFormTicketTypes();
        this.error = '';
    }

    deleteArchivedType(type: TicketType): void {
        if (!confirm(`Удалить тип заявки «${type.name}» из локального списка?`)) return;
        this.archivedTicketTypes = this.archivedTicketTypes.filter(item => item.id !== type.id);
        this.ticketTypeChangesDirty = true;
    }

    saveTicketTypeChanges(): void {
        this.savedTicketTypes = this.cloneTicketTypes(this.ticketTypes);
        this.savedArchivedTicketTypes = this.cloneTicketTypes(this.archivedTicketTypes);
        this.ticketTypeChangesDirty = false;
        this.notice = 'Изменения типов сохранены в памяти текущего сеанса; серверный API сохранения не подключён.';
    }

    discardTicketTypeChanges(): void {
        this.ticketTypes = this.cloneTicketTypes(this.savedTicketTypes);
        this.archivedTicketTypes = this.cloneTicketTypes(this.savedArchivedTicketTypes);
        this.ticketTypeChangesDirty = false;
        this.closeTicketTypeEditor();
        this.syncFormTicketTypes();
    }

    setTypeSla(type: TicketType, level: string, metric: 'reaction' | 'resolution', value: string): void {
        type.slaByCriticality ??= {};
        type.slaByCriticality[level] ??= { reaction: type.reactionSla, resolution: type.resolutionSla };
        type.slaByCriticality[level][metric] = value;
        this.ticketTypeChangesDirty = true;
    }
    togglePortalSla(field: string, event: Event): void { if (!this.settings) return; this.settings.portalSlaFields = this.toggleString(this.settings.portalSlaFields, field, (event.target as HTMLInputElement).checked); }
    toggleWorkingDay(day: string, event: Event): void { if (!this.settings) return; this.settings.workingDays = this.toggleString(this.settings.workingDays, day, (event.target as HTMLInputElement).checked); }
    addHoliday(): void { if (!this.settings || !this.newHoliday || this.settings.holidays.includes(this.newHoliday)) return; this.settings.holidays = [...this.settings.holidays, this.newHoliday].sort(); this.newHoliday = ''; }
    removeHoliday(date: string): void { if (this.settings) this.settings.holidays = this.settings.holidays.filter(holiday => holiday !== date); }
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
        if (this.newFieldType === 'asset' && form.fields.some(field => field.type === 'asset')) {
            this.error = 'В одной форме можно связать заявку только с одним активом.';
            return;
        }
        form.fields = [...form.fields, {
            id: `field-${Date.now()}-${++this.localIdSequence}`,
            name,
            type: this.newFieldType,
            required: false,
            visibleToRequester: true,
        }];
        this.newFieldName = '';
        this.changeDetector.markForCheck();
    }
    loadForm(ticketTypeId: string): void {
        this.error = '';
        this.notice = '';
        this.forms = [];
        const requestVersion = ++this.formRequestVersion;
        if (!ticketTypeId) {
            this.formLoading = false;
            return;
        }

        const cachedDraft = this.formDrafts.get(ticketTypeId);
        const cachedForm = cachedDraft ?? this.savedFormDrafts.get(ticketTypeId);
        if (cachedForm) {
            this.forms = [cachedForm];
            this.formLoading = false;
            return;
        }

        this.formLoading = true;
        this.workspace.getFormTemplate(ticketTypeId).subscribe({
            next: form => {
                if (requestVersion !== this.formRequestVersion) return;
                const draft = this.cloneForm(form);
                this.formDrafts.set(ticketTypeId, draft);
                this.forms = [draft];
                this.formLoading = false;
                this.changeDetector.markForCheck();
            },
            error: err => {
                if (requestVersion !== this.formRequestVersion) return;
                this.formLoading = false;
                this.error = err.message;
                this.changeDetector.markForCheck();
            },
        });
    }
    moveField(form: TicketFormDefinition, event: Pick<CdkDragDrop<FormFieldDefinition[]>, 'previousIndex' | 'currentIndex'>): void {
        moveItemInArray(form.fields, event.previousIndex, event.currentIndex);
        form.fields = [...form.fields];
        this.changeDetector.markForCheck();
    }
    removeField(form: TicketFormDefinition, field: FormFieldDefinition): void {
        if (field.immutable) {
            this.error = `Встроенное поле «${field.name}» нельзя удалить.`;
            return;
        }
        form.fields = form.fields.filter(item => item.id !== field.id);
        for (const dependent of form.fields) {
            if (dependent.conditionFieldId === field.id) this.setConditionField(dependent, '');
        }
        this.changeDetector.markForCheck();
    }
    setOptions(field: FormFieldDefinition, value: string): void { field.options = value.split(',').map(item => item.trim()).filter(Boolean); }
    setConditionField(field: FormFieldDefinition, fieldId: string): void {
        if (!fieldId) {
            delete field.conditionFieldId;
            delete field.conditionOperator;
            delete field.conditionValue;
            return;
        }
        field.conditionFieldId = fieldId;
        field.conditionOperator = 'equals';
        field.conditionValue = '';
    }
    conditionSourceIsNumeric(form: TicketFormDefinition, field: FormFieldDefinition): boolean {
        return form.fields.find(item => item.id === field.conditionFieldId)?.type === 'number';
    }
    saveForm(form: TicketFormDefinition): void {
        if (this.savingFormIds.has(form.ticketTypeId)) return;
        for (const field of form.fields) {
            if (!field.conditionFieldId) continue;
            const source = form.fields.find(item => item.id === field.conditionFieldId);
            if (!source || source.id === field.id) {
                this.error = `Для поля «${field.name}» выберите существующее поле условия.`;
                return;
            }
            if (!field.conditionOperator || !field.conditionValue?.trim()) {
                this.error = `Для поля «${field.name}» заполните оператор и значение условия.`;
                return;
            }
            if (field.conditionOperator === 'greaterThan'
                && (source.type !== 'number' || !Number.isFinite(Number(field.conditionValue)))) {
                this.error = `Условие «Больше» для поля «${field.name}» требует числовое поле и значение.`;
                return;
            }
        }
        this.error = '';
        this.notice = '';
        this.savingFormIds.add(form.ticketTypeId);
        this.workspace.saveForm(this.cloneForm(form)).subscribe({
            next: savedForm => {
                const saved = this.cloneForm(savedForm);
                this.savedFormDrafts.set(saved.ticketTypeId, saved);
                this.formDrafts.set(saved.ticketTypeId, saved);
                if (this.formId === saved.ticketTypeId) this.forms = [saved];
                this.savingFormIds.delete(form.ticketTypeId);
                this.notice = 'Схема формы сохранена.';
                this.changeDetector.markForCheck();
            },
            error: err => {
                this.savingFormIds.delete(form.ticketTypeId);
                this.error = err?.error?.detail ?? err?.message ?? 'Не удалось сохранить схему формы.';
                this.changeDetector.markForCheck();
            },
        });
    }
    loadReports(): void {
        const requestId = ++this.reportsRequestId;
        this.reportLoading = true;
        this.reportError = '';
        this.changeDetector.markForCheck();
        this.tenantSettingsApi.getReports(this.reportFrom || undefined, this.reportTo || undefined).subscribe({
            next: reports => {
                if (requestId !== this.reportsRequestId) return;
                this.reports = reports;
                this.reportLoading = false;
                this.changeDetector.markForCheck();
                if (this.reportDetailOpen) this.loadReportDetails();
            },
            error: err => {
                if (requestId !== this.reportsRequestId) return;
                this.reportError = err?.error?.detail ?? err.message ?? 'Не удалось загрузить отчёты.';
                this.reportLoading = false;
                this.changeDetector.markForCheck();
            },
        });
    }
    openReport(report: ReportSummary): void {
        this.activeReport = report;
        this.reportDetailOpen = true;
        this.reportDetailSkip = 0;
        this.loadReportDetails();
        this.changeDetector.markForCheck();
    }
    closeReport(): void {
        this.reportDetailRequestId++;
        this.reportDetailOpen = false;
        this.reportDetailLoading = false;
        this.reportDetail = null;
        this.activeReport = null;
        this.changeDetector.markForCheck();
    }
    changeReportPage(direction: -1 | 1): void {
        this.reportDetailSkip = Math.max(0, this.reportDetailSkip + direction * this.reportDetailTake);
        this.loadReportDetails();
    }
    loadReportDetails(): void {
        if (!this.activeReport) return;
        const requestId = ++this.reportDetailRequestId;
        this.reportDetail = null;
        this.reportDetailLoading = true;
        this.reportError = '';
        this.changeDetector.markForCheck();
        this.tenantSettingsApi.getReportDetails(
            this.activeReport.id,
            this.reportFrom || undefined,
            this.reportTo || undefined,
            this.reportDetailSkip,
            this.reportDetailTake,
        ).subscribe({
            next: detail => {
                if (requestId !== this.reportDetailRequestId) return;
                this.reportDetail = detail;
                this.reportDetailLoading = false;
                this.changeDetector.markForCheck();
            },
            error: err => {
                if (requestId !== this.reportDetailRequestId) return;
                this.reportError = err?.error?.detail ?? err.message ?? 'Не удалось загрузить детализацию отчёта.';
                this.reportDetailLoading = false;
                this.changeDetector.markForCheck();
            },
        });
    }
    private toDateInput(date: Date): string {
        const year = date.getFullYear();
        const month = String(date.getMonth() + 1).padStart(2, '0');
        const day = String(date.getDate()).padStart(2, '0');
        return `${year}-${month}-${day}`;
    }
    downloadCsv(reportType: string): void {
        this.downloadingReportId = reportType;
        this.reportError = '';
        this.tenantSettingsApi.downloadReportCsv(reportType, this.reportFrom || undefined, this.reportTo || undefined).subscribe({
            next: blob => {
                const url = URL.createObjectURL(blob);
                const anchor = document.createElement('a');
                anchor.href = url;
                anchor.download = `itsm-${reportType}.csv`;
                anchor.click();
                URL.revokeObjectURL(url);
                this.downloadingReportId = null;
            },
            error: err => {
                this.reportError = err?.error?.detail ?? err.message ?? 'Не удалось скачать CSV.';
                this.downloadingReportId = null;
            },
        });
    }

    private syncFormTicketTypes(): void {
        this.formTicketTypes = this.ticketTypes.map(type => ({
            id: type.id,
            name: type.name,
            code: type.repair ? 'repair' : null,
            isBuiltIn: type.builtIn ?? type.repair,
            isPortalAvailable: type.portalAvailable,
        }));
    }

    private cloneTicketType(type: TicketType): TicketType {
        return {
            ...type,
            slaByCriticality: type.slaByCriticality
                ? Object.fromEntries(Object.entries(type.slaByCriticality).map(([level, sla]) => [level, { ...sla }]))
                : undefined,
        };
    }

    private cloneTicketTypes(types: TicketType[]): TicketType[] {
        return types.map(type => this.cloneTicketType(type));
    }

    private cloneForm(form: TicketFormDefinition): TicketFormDefinition {
        return {
            ...form,
            fields: form.fields.map(field => ({
                ...field,
                options: field.options ? [...field.options] : undefined,
            })),
        };
    }

    private toggleString(values: string[], value: string, enabled: boolean): string[] { return enabled ? [...new Set([...values, value])] : values.filter(item => item !== value); }
    private defaultFields(id: string): FormFieldDefinition[] { return [
        { id: `${id}-title`, name: 'Тема', type: 'string', required: true, visibleToRequester: true, immutable: true },
        { id: `${id}-description`, name: 'Описание', type: 'textarea', required: true, visibleToRequester: true, immutable: true },
        { id: `${id}-priority`, name: 'Критичность', type: 'dictionary', required: true, visibleToRequester: true, options: this.settings?.criticalities, immutable: true },
    ]; }

    editUser(user: UserSummary): void {
        if (!this.canEditUser(user)) return;
        this.expandedUserId = user.id;
        this.editingUserId = user.id;
        const roleIds = this.isTenantOwner(user)
            ? [...user.roleIds]
            : user.roleIds.filter(roleId => this.assignableRoles.some(role => role.id === roleId)).slice(0, 1);
        this.editingUser = { ...user, roleIds };
    }

    resetUserMfa(user: UserSummary): void {
        if (!this.canEditUser(user) || !confirm(`Сбросить 2FA для пользователя «${user.fullName}»? При следующем входе потребуется настроить её заново.`)) return;
        this.error = '';
        this.notice = '';
        this.admin.resetMfa(user.id).subscribe({
            next: updated => {
                this.usersList = this.usersList.map(item => item.id === updated.id ? updated : item);
                this.notice = `2FA пользователя «${user.fullName}» сброшена.`;
                this.changeDetector.markForCheck();
            },
            error: err => {
                this.error = err.message || 'Не удалось сбросить 2FA.';
                this.changeDetector.markForCheck();
            },
        });
    }

    resetUserPassword(user: UserSummary): void {
        if (!this.canEditUser(user)) return;
        const newPassword = prompt(`Введите новый пароль для «${user.fullName}» (не менее 8 символов):`);
        if (newPassword === null) return;
        if (newPassword.length < 8) {
            this.error = 'Новый пароль должен содержать не менее 8 символов.';
            return;
        }

        this.error = '';
        this.notice = '';
        this.admin.resetPassword(user.id, newPassword).subscribe({
            next: updated => {
                this.usersList = this.usersList.map(item => item.id === updated.id ? updated : item);
                this.notice = `Пароль пользователя «${user.fullName}» сброшен.`;
                this.changeDetector.markForCheck();
            },
            error: err => {
                this.error = err.error?.message || err.message || 'Не удалось сбросить пароль.';
                this.changeDetector.markForCheck();
            },
        });
    }

    toggleUserDetails(userId: number): void {
        this.expandedUserId = this.expandedUserId === userId ? null : userId;
    }

    cancelEdit(): void {
        this.editingUserId = null;
        this.editingUser = null;
        this.expandedUserId = null;
        this.error = '';
    }

    toggleEditingUserRole(roleId: number, event: Event): void {
        if (!this.editingUser) return;
        const selectedRole = this.availableRoles.find(role => role.id === roleId);
        if (selectedRole?.code === 'tenant_owner') return;
        const checked = (event.target as HTMLInputElement).checked;
        this.editingUser.roleIds = checked ? [roleId] : [];
    }

    saveUser(userId: number): void {
        const target = this.usersList.find(user => user.id === userId);
        if (target && !this.canEditUser(target)) {
            this.error = 'Изменять учётную запись владельца может только глобальный администратор.';
            return;
        }
        if (target && this.isSelfTenantOwner(target) && this.editingUser?.isActive === false) {
            this.error = 'Владелец тенанта не может деактивировать собственную учётную запись.';
            return;
        }
        if (!this.editingUser || !this.editingUser.roleIds || this.editingUser.roleIds.length === 0) {
            this.error = 'Назначьте пользователю хотя бы одну роль.';
            return;
        }
        const request: UpdateAdminUserRequest = {
            username: this.editingUser.username ?? '',
            email: this.editingUser.email ?? '',
            fullName: this.editingUser.fullName ?? '',
            department: this.editingUser.department ?? null,
            managerId: this.editingUser.managerId ?? null,
            tenantId: this.editingUser.tenantId ?? null,
            isActive: this.editingUser.isActive ?? true,
            roleIds: this.editingUser.roleIds,
        };
        this.admin.updateUser(userId, request).subscribe({
            next: updated => {
                this.usersList = this.usersList.map(u => u.id === userId ? updated : u);
                this.editingUserId = null;
                this.editingUser = null;
                this.expandedUserId = null;
                this.notice = 'Пользователь обновлён.';
                this.error = '';
                this.loadUserQuota();
            },
            error: err => {
                this.error = err.message || 'Не удалось обновить пользователя.';
            },
        });
    }

    private loadUserQuota(): void {
        this.quotaError = '';
        this.quotaService.getCurrentTenantQuota().subscribe({
            next: quota => {
                this.userQuota = quota;
                this.changeDetector.markForCheck();
            },
            error: () => {
                this.quotaError = 'Не удалось загрузить состояние лимита пользователей.';
                this.changeDetector.markForCheck();
            },
        });
    }

    findUserName(userId: number | null): string | undefined {
        if (!userId) return undefined;
        return this.usersList.find(u => u.id === userId)?.fullName;
    }

    findRoleName(roleId: number): string | undefined {
        return this.availableRoles.find(r => r.id === roleId)?.name;
    }

    getUserRoles(user: UserSummary): RoleSummary[] {
        return user.roleIds
            .map(id => this.availableRoles.find(role => role.id === id))
            .filter((role): role is RoleSummary => role !== undefined);
    }

    isTenantOwner(user: UserSummary): boolean {
        return user.roleCodes?.includes('tenant_owner') === true
            || user.roleIds.some(id => this.availableRoles.find(role => role.id === id)?.code === 'tenant_owner');
    }

    canEditUser(user: UserSummary): boolean {
        return !this.isTenantOwner(user) || this.auth.isTenantAdmin() || this.auth.isTenantOwner();
    }

    canResetUserMfa(user: UserSummary): boolean {
        return !this.isCurrentUser(user) && this.canEditUser(user);
    }

    canResetUserPassword(user: UserSummary): boolean {
        if (this.isCurrentUser(user) || !this.canEditUser(user)) return false;
        const targetIsAdmin = user.roleCodes?.includes('admin')
            || this.getUserRoles(user).some(role => role.code === 'admin');
        return !targetIsAdmin || this.isTenantOwner(user) || this.auth.isTenantOwner();
    }

    private isCurrentUser(user: UserSummary): boolean {
        return user.username.toLocaleLowerCase() === this.auth.getLogin().toLocaleLowerCase();
    }

    isSelfTenantOwner(user: UserSummary): boolean {
        return this.isTenantOwner(user) && this.auth.isTenantOwner()
            && user.username.toLocaleLowerCase() === this.auth.getLogin().toLocaleLowerCase();
    }

    clearUserFilters(): void {
        this.usersSearch = '';
        this.usersDepartmentFilter = '';
        this.usersRoleFilter = '';
        this.usersStatusFilter = '';
    }
}