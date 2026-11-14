import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/auth/auth.service';
import { DictionaryApiService } from '../../core/dictionaries/dictionary.service';
import { DictionaryValueDto } from '../../core/dictionaries/dictionary.models';
import { HttpWorkspaceApiService } from '../../core/workspace/http-workspace-api.service';
import { AssetClassAttributeOption, AssetClassOption, AssetDetails, AssetMovement, AssetRecord } from '../../core/workspace/workspace.models';

type AssetTab = 'card' | 'history' | 'tickets' | 'movements';

@Component({
    selector: 'app-assets-page',
    standalone: true,
    imports: [CommonModule, FormsModule],
    template: `
    <section class="page">
      <header class="heading"><div><p class="eyebrow">Рабочее пространство / Учёт активов</p><h1>Активы</h1></div><button class="primary" type="button" (click)="showCreate = !showCreate">＋ Новый актив</button></header>
      @if (error) { <p class="message error">{{ error }}</p> }
      @if (notice) { <p class="message success">{{ notice }}</p> }

      @if (showCreate) {
        <form class="create panel" (ngSubmit)="createAsset()">
          <h2>Карточка нового актива</h2>
          <label>Наименование *<input name="name" [(ngModel)]="draft.name" required /></label>
          <label>Инвентарный номер<input name="id" [(ngModel)]="draft.id" /></label>
          <label>Серийный номер<input name="serialNumber" [(ngModel)]="draft.serialNumber" /></label>
          <label>Класс *<select name="className" [(ngModel)]="draft.className" (ngModelChange)="onCreateClassChange($event)" required>@for (assetClass of activeAssetClasses; track assetClass.id) { <option [value]="assetClass.name">{{ assetClass.name }}</option> }</select></label>
          @for (attribute of createAttributes; track attribute.code) {
            <label>{{ attribute.name }}@if (attribute.required) { <span>*</span> }
              @switch (attribute.dataType) {
                @case ('text') { <textarea [name]="'new-' + attribute.code" [(ngModel)]="draftAttributes[attribute.code]" [required]="attribute.required"></textarea> }
                @case ('number') { <input type="number" [name]="'new-' + attribute.code" [(ngModel)]="draftAttributes[attribute.code]" [required]="attribute.required" /> }
                @case ('date') { <input type="date" [name]="'new-' + attribute.code" [(ngModel)]="draftAttributes[attribute.code]" [required]="attribute.required" /> }
                @case ('bool') { <input type="checkbox" [name]="'new-' + attribute.code" [(ngModel)]="draftAttributes[attribute.code]" [required]="attribute.required" /> }
                @case ('dictionary') { <select [name]="'new-' + attribute.code" [(ngModel)]="draftAttributes[attribute.code]" [required]="attribute.required"><option value="">Выберите значение</option>@for (option of attribute.options; track option) { <option [value]="option">{{ option }}</option> }</select> }
                @default { <input type="text" [name]="'new-' + attribute.code" [(ngModel)]="draftAttributes[attribute.code]" [required]="attribute.required" /> }
              }
            </label>
          }
          <label>Склад<select name="warehouseCode" [(ngModel)]="draft.warehouseCode"><option [ngValue]="null">Не выбрано</option>@for (value of activeWarehouses; track value.code) { <option [ngValue]="value.code">{{ value.name }} · {{ value.code }}</option> }</select></label>
          <label>Место<select name="locationCode" [(ngModel)]="draft.locationCode"><option [ngValue]="null">Не выбрано</option>@for (value of activeLocations; track value.code) { <option [ngValue]="value.code">{{ value.name }} · {{ value.code }}</option> }</select></label>
          <label>Подразделение<select name="departmentCode" [(ngModel)]="draft.departmentCode"><option [ngValue]="null">Не выбрано</option>@for (value of activeDepartments; track value.code) { <option [ngValue]="value.code">{{ value.name }} · {{ value.code }}</option> }</select></label>
          <p class="form-note">Укажите инвентарный или серийный номер. На склад актив попадёт только при наличии инвентарного номера и кода склада; иначе останется на этапе «Закуплен».</p>
          <div class="form-actions"><button class="primary" type="submit" [disabled]="!draft.name.trim() || (!draft.id.trim() && !draft.serialNumber.trim()) || (!!draft.warehouseCode && !draft.id.trim())">Создать</button><button class="quiet" type="button" (click)="showCreate = false">Отмена</button></div>
        </form>
      }

      <div class="asset-layout">
        <section class="panel register">
          <header class="panel-head"><div><h2>Реестр</h2><small>{{ filteredAssets.length }} активов</small></div><label class="search"><span aria-hidden="true">⌕</span><input aria-label="Поиск активов" [(ngModel)]="query" placeholder="Название или ID" /></label></header>
          <div class="asset-list">
            @for (asset of filteredAssets; track asset.recordId || asset.id) {
              <button type="button" class="asset-row" [class.selected]="selected?.id === asset.id" (click)="select(asset)">
                <span class="asset-mark" aria-hidden="true">{{ asset.type.slice(0, 1) }}</span>
                <span class="asset-name"><strong>{{ asset.name }}</strong><small>{{ asset.id || asset.serialNumber || 'Без идентификатора' }} · {{ asset.className }}</small></span>
                <span class="stage" [class.retired]="asset.lifecycle === 'Списан'">{{ asset.lifecycle }}</span>
              </button>
            }
            @if (!filteredAssets.length) { <p class="empty">Активы не найдены</p> }
          </div>
        </section>

        <section class="panel detail">
          @if (selected) {
            <header class="detail-head"><div><p class="eyebrow">{{ selected.id || selected.serialNumber || 'Закупка' }} · {{ selected.className }}</p><h2>{{ selected.name }}</h2></div><span class="lifecycle">{{ selected.lifecycle }}</span></header>
            <nav class="tabs" aria-label="Разделы карточки актива">
              <button [class.active]="tab === 'card'" (click)="tab = 'card'">Карточка</button>
              <button [class.active]="tab === 'history'" (click)="tab = 'history'">История актива</button>
              <button [class.active]="tab === 'tickets'" (click)="tab = 'tickets'">История заявок <span>{{ selected.relatedTickets.length }}</span></button>
              <button [class.active]="tab === 'movements'" (click)="tab = 'movements'">Движения</button>
            </nav>

            @if (tab === 'card') {
              <form class="card-form" (ngSubmit)="saveAsset()">
                <label>Идентификатор<input name="assetId" [(ngModel)]="edit.id" [disabled]="!!selected.id" [placeholder]="selected.id ? '' : 'Присвоить инвентарный номер'" /></label>
                <label>Наименование<input name="assetName" [(ngModel)]="edit.name" required /></label>
                <label>Этап жизненного цикла<input [value]="selected.lifecycle" disabled /></label>
                <label>Место<select name="locationCode" [(ngModel)]="edit.locationCode"><option [ngValue]="null">Без изменения</option>@for (value of locations; track value.code) { <option [ngValue]="value.code" [disabled]="value.isArchived">{{ value.name }} · {{ value.code }}{{ value.isArchived ? ' · архив' : '' }}</option> }</select></label>
                <label>Подразделение<select name="departmentCode" [(ngModel)]="edit.departmentCode"><option [ngValue]="null">Без изменения</option>@for (value of departments; track value.code) { <option [ngValue]="value.code" [disabled]="value.isArchived">{{ value.name }} · {{ value.code }}{{ value.isArchived ? ' · архив' : '' }}</option> }</select></label>
                @for (attribute of selectedAttributes; track attribute.code) {
                  <label>{{ attribute.name }}@if (attribute.required) { <span>*</span> }
                    @switch (attribute.dataType) {
                      @case ('text') { <textarea [name]="'edit-' + attribute.code" [(ngModel)]="edit.attributes[attribute.code]" [required]="attribute.required"></textarea> }
                      @case ('number') { <input type="number" [name]="'edit-' + attribute.code" [(ngModel)]="edit.attributes[attribute.code]" [required]="attribute.required" /> }
                      @case ('date') { <input type="date" [name]="'edit-' + attribute.code" [(ngModel)]="edit.attributes[attribute.code]" [required]="attribute.required" /> }
                      @case ('bool') { <input type="checkbox" [name]="'edit-' + attribute.code" [(ngModel)]="edit.attributes[attribute.code]" [required]="attribute.required" /> }
                      @case ('dictionary') { <select [name]="'edit-' + attribute.code" [(ngModel)]="edit.attributes[attribute.code]" [required]="attribute.required"><option value="">Выберите значение</option>@for (option of attribute.options; track option) { <option [value]="option">{{ option }}</option> }</select> }
                      @default { <input type="text" [name]="'edit-' + attribute.code" [(ngModel)]="edit.attributes[attribute.code]" [required]="attribute.required" /> }
                    }
                  </label>
                }
                <div class="card-actions"><button class="primary" type="submit">Сохранить карточку</button>
                  @if (isEngineer && selected.lifecycle === 'Закуплен') { <button class="danger" type="button" (click)="deleteAsset()">Удалить закупленный актив</button> }
                </div>
              </form>
            }

            @if (tab === 'history') {
              <div class="timeline">@for (entry of selected.history; track entry.id) {
                <article><span class="timeline-dot"></span><div><strong>{{ entry.event }}</strong><p>{{ entry.details }}</p><small>{{ entry.createdAt }} · {{ entry.actor }}</small></div></article>
              } @if (!selected.history.length) { <p class="empty">История пока пуста</p> }</div>
            }

            @if (tab === 'tickets') {
              <div class="history-filters">
                <label>Тип<select name="ticketTypeFilter" [(ngModel)]="ticketTypeFilter"><option value="">Все типы</option>@for (type of relatedTicketTypes; track type) { <option [value]="type">{{ type }}</option> }</select></label>
                <label>Статус<select name="ticketStatusFilter" [(ngModel)]="ticketStatusFilter"><option value="">Все статусы</option>@for (status of relatedTicketStatuses; track status) { <option [value]="status">{{ status }}</option> }</select></label>
                <label>Критичность<select name="ticketPriorityFilter" [(ngModel)]="ticketPriorityFilter"><option value="">Любая</option>@for (priority of relatedTicketPriorities; track priority) { <option [value]="priority">{{ priority }}</option> }</select></label>
                <label>Исполнитель<select name="ticketAssigneeFilter" [(ngModel)]="ticketAssigneeFilter"><option value="">Все исполнители</option>@for (assignee of relatedTicketAssignees; track assignee) { <option [value]="assignee">{{ assignee }}</option> }</select></label>
                <label>Создана с<input type="date" name="createdFrom" [(ngModel)]="createdFrom" /></label>
                <label>Создана по<input type="date" name="createdTo" [(ngModel)]="createdTo" /></label>
              </div>
              <div class="table-scroll"><table><thead><tr><th>Номер</th><th>Тип</th><th>Тема</th><th>Автор</th><th>Исполнитель</th><th>Статус</th><th>Критичность</th><th>Создана</th><th>Закрыта</th></tr></thead><tbody>
                @for (ticket of filteredRelatedTickets; track ticket.id) { <tr><td>{{ ticket.id }}</td><td>{{ ticket.type }}</td><td>{{ ticket.title }}</td><td>{{ ticket.requester }}</td><td>{{ ticket.assignee }}</td><td>{{ ticket.status }}</td><td>{{ ticket.priority }}</td><td>{{ ticket.createdOn }}</td><td>{{ ticket.closedOn || '—' }}</td></tr> }
                @if (!filteredRelatedTickets.length) { <tr><td colspan="9" class="empty">Заявок по заданным фильтрам нет</td></tr> }
              </tbody></table></div>
            }

            @if (tab === 'movements') {
              @if (isEngineer && ['На складе', 'В эксплуатации'].includes(selected.lifecycle || '')) {
                <form class="movement-form" (ngSubmit)="moveAsset()">
                  <label>Вид движения<select name="kind" [(ngModel)]="movement.kind">
                    @if (selected.lifecycle === 'На складе') { <option>Выдача со склада</option><option>Перемещение между складами</option> }
                    @if (selected.lifecycle === 'В эксплуатации') { <option>Возврат на склад</option> }
                  </select></label>
                  <label>Откуда<input name="from" [(ngModel)]="movement.from" required /></label>
                  <label>Куда / кому<input name="to" [(ngModel)]="movement.to" required /></label>
                  @if (movement.kind === 'Выдача со склада') { <label>ID сотрудника-получателя<input type="number" min="1" name="toUserId" [(ngModel)]="movement.toUserId" required /></label> }
                  @if (movement.kind !== 'Выдача со склада') { <label>Склад-получатель<select name="toWarehouseCode" [(ngModel)]="movement.toWarehouseCode" required><option [ngValue]="null">Выберите склад</option>@for (value of activeWarehouses; track value.code) { <option [ngValue]="value.code">{{ value.name }} · {{ value.code }}</option> }</select></label> }
                  <button class="primary" type="submit" [disabled]="!movement.from.trim() || !movement.to.trim()">Оформить</button>
                </form>
              }
              @if (isEngineer && ['На складе', 'В эксплуатации'].includes(selected.lifecycle || '')) {
                <form class="repair-form" (ngSubmit)="createRepair()"><label>Тикет на ремонт<input name="repairTitle" [(ngModel)]="repairTitle" placeholder="Что требуется проверить или исправить" required /></label><button class="secondary" type="submit">Создать тикет на ремонт</button></form>
              }
              @if (isEngineer && selected.lifecycle === 'В ремонте' && activeRepairTicket) {
              <form class="repair-form close-repair" (ngSubmit)="closeRepair()"><strong>{{ activeRepairTicket.id }} · {{ activeRepairTicket.title }}</strong><label>Результат ремонта<select name="repairResult" [(ngModel)]="repairResult"><option value="В эксплуатацию">Вернуть в эксплуатацию</option><option value="На склад">Перевести на склад</option><option value="Списать">Списать</option></select></label><label>ID сотрудника для возврата в эксплуатацию (если не назначен)<input type="number" min="1" name="repairToUserId" [(ngModel)]="repairToUserId" /></label><label>Код склада для возврата (если не задан)<input name="repairToWarehouseCode" [(ngModel)]="repairToWarehouseCode" /></label><label>Что ремонтировалось и что менялось *<textarea name="workDescription" [(ngModel)]="workDescription" required></textarea></label><button class="primary" type="submit" [disabled]="!workDescription.trim()">Закрыть тикет ремонта</button></form>
              }
              <div class="movement-list">@for (item of selected.movements; track item.id) {
                <article><strong>{{ item.kind }}</strong><span>{{ item.from }} → {{ item.to }}</span><small>{{ item.createdAt }} · {{ item.actor }}</small></article>
              } @if (!selected.movements.length) { <p class="empty">Движений пока нет</p> }</div>
            }
          } @else { <div class="select-empty"><h2>Выберите актив</h2><p>Карточка, история, движения и связанные заявки отображаются здесь.</p></div> }
        </section>
      </div>
    </section>
  `,
  styles: [`
    :host { display:block; color:#202734; }
    .page { max-width:1500px; margin:auto; padding:26px 28px 38px; }
    .heading,.panel-head,.detail-head { display:flex; align-items:center; justify-content:space-between; gap:16px; }
    .heading { margin-bottom:20px; } h1 { margin:0; font-size:24px; } h2 { margin:0; font-size:14px; }
    .eyebrow { margin:0 0 5px; color:#8993a1; font-size:10px; }
    .panel { min-width:0; border:1px solid #e2e7ed; border-radius:8px; background:#fff; }
    button,input,select,textarea { font:inherit; }
    .primary,.secondary,.danger,.quiet { min-height:36px; padding:0 12px; border:0; border-radius:5px; cursor:pointer; font-size:11px; font-weight:650; }
    .primary { background:#2563eb; color:#fff; } .secondary { border:1px solid #dce3eb; background:#fff; color:#465264; }
    .danger { background:#fff0f0; color:#bb4141; } .quiet { background:#f2f4f7; color:#526070; }
    .asset-layout { display:grid; grid-template-columns:minmax(260px,340px) minmax(0,1fr); gap:15px; align-items:start; }
    .register { overflow:hidden; } .panel-head { min-height:64px; padding:12px 14px; border-bottom:1px solid #edf0f4; }
    .panel-head small { color:#929baa; font-size:10px; }
    .search { display:flex; align-items:center; gap:5px; color:#8792a0; font-size:18px; }
    .search input { width:130px; height:31px; padding:0 8px; border:1px solid #e0e5eb; border-radius:5px; font-size:10px; }
    .asset-list { max-height:72vh; overflow:auto; }
    .asset-row { width:100%; display:flex; align-items:center; gap:9px; padding:12px; border:0; border-bottom:1px solid #f0f2f5; background:#fff; text-align:left; cursor:pointer; }
    .asset-row:hover,.asset-row.selected { background:#f4f7fc; }
    .asset-mark { width:30px; height:30px; flex:0 0 30px; display:grid; place-items:center; border-radius:6px; background:#edf4ff; color:#2864bf; font-weight:700; }
    .asset-name { min-width:0; flex:1; } .asset-name strong,.asset-name small { display:block; overflow:hidden; text-overflow:ellipsis; white-space:nowrap; }
    .asset-name strong { color:#394352; font-size:10px; } .asset-name small { margin-top:4px; color:#929baa; font-size:9px; }
    .stage,.lifecycle { padding:4px 6px; border-radius:4px; background:#eef7f2; color:#267b59; font-size:9px; white-space:nowrap; }
    .stage.retired { background:#f0f1f3; color:#707989; } .detail { min-height:400px; padding:17px; }
    .detail-head { align-items:flex-start; margin-bottom:17px; } .detail-head h2 { font-size:18px; }
    .tabs { display:flex; gap:4px; margin-bottom:16px; overflow:auto; border-bottom:1px solid #e9edf2; }
    .tabs button { min-height:36px; padding:0 10px; border:0; border-bottom:2px solid transparent; background:none; color:#788392; font-size:10px; white-space:nowrap; cursor:pointer; }
    .tabs button.active { border-bottom-color:#3475d1; color:#2864bf; font-weight:650; }
    .tabs button span { margin-left:4px; color:#8e98a6; }
    .card-form { display:grid; grid-template-columns:repeat(2,minmax(0,1fr)); gap:13px; }
    label { display:grid; gap:5px; color:#697586; font-size:10px; }
    input,select,textarea { min-width:0; min-height:34px; padding:6px 9px; border:1px solid #dfe4ea; border-radius:5px; background:#fff; color:#394252; font-size:11px; }
    input:disabled { background:#f7f8fa; color:#8792a0; }
    .card-actions { grid-column:1/-1; display:flex; gap:8px; margin-top:3px; }
    .timeline { display:grid; gap:0; } .timeline article { position:relative; display:flex; gap:12px; min-height:72px; }
    .timeline article:not(:last-child)::before { position:absolute; top:12px; bottom:0; left:5px; width:1px; background:#dfe6ef; content:''; }
    .timeline-dot { z-index:1; width:11px; height:11px; flex:0 0 11px; margin-top:2px; border:2px solid #3977d1; border-radius:50%; background:#fff; }
    .timeline strong,.movement-list strong { color:#394352; font-size:11px; } .timeline p { margin:5px 0; color:#667180; font-size:10px; }
    .timeline small,.movement-list small { color:#959eaa; font-size:9px; }
    .table-scroll { overflow:auto; } table { width:100%; min-width:850px; border-collapse:collapse; text-align:left; }
    .history-filters { display:grid; grid-template-columns:repeat(3,minmax(0,1fr)); gap:9px; margin-bottom:12px; }
    th,td { padding:9px 8px; border-bottom:1px solid #eef1f4; font-size:9px; white-space:nowrap; } th { color:#8c96a4; text-transform:uppercase; }
    td { color:#576272; } .movement-form { display:grid; grid-template-columns:1.1fr 1fr 1fr auto; align-items:end; gap:9px; padding:12px; border-radius:6px; background:#f7f9fc; }
    .repair-form { display:grid; grid-template-columns:minmax(0,1fr) auto; align-items:end; gap:9px; margin-top:12px; padding:12px; border:1px solid #e1e7ef; border-radius:6px; }
    .repair-form textarea { min-height:66px; resize:vertical; } .repair-form strong { grid-column:1/-1; font-size:11px; }
    .close-repair { grid-template-columns:1fr 1fr; } .close-repair label:nth-of-type(2) { grid-column:1/-1; }
    .movement-list { margin-top:14px; } .movement-list article { display:grid; grid-template-columns:1fr 1fr auto; gap:12px; padding:11px 4px; border-bottom:1px solid #eef1f4; }
    .movement-list span { color:#667180; font-size:10px; }
    .empty,.select-empty { padding:25px 12px; color:#8a94a2; font-size:11px; } .select-empty { padding:80px 22px; text-align:center; } .select-empty p { color:#8a94a2; font-size:11px; }
    .create { display:grid; grid-template-columns:repeat(3,minmax(0,1fr)); gap:12px; margin-bottom:15px; padding:15px; }
    .create h2,.form-note,.form-actions { grid-column:1/-1; } .form-note { margin:0; color:#778291; font-size:10px; }
    .form-actions { display:flex; gap:8px; }
    .message { padding:10px 12px; border-radius:5px; font-size:11px; } .error { color:#a93232; background:#fff0f0; } .success { color:#176f52; background:#edf8f2; }
    @media(max-width:1020px) { .asset-layout { grid-template-columns:1fr; } .asset-list { display:grid; grid-template-columns:repeat(2,minmax(0,1fr)); max-height:none; } }
    @media(max-width:680px) { .page { padding:20px 13px 28px; } .heading { align-items:flex-start; flex-direction:column; } .asset-list { grid-template-columns:1fr; } .detail { padding:13px; } .card-form,.create,.history-filters { grid-template-columns:1fr; } .create h2,.form-note,.form-actions { grid-column:auto; } .movement-form,.repair-form,.close-repair { grid-template-columns:1fr; } .close-repair label:nth-of-type(2) { grid-column:auto; } .movement-list article { grid-template-columns:1fr; gap:4px; } .card-actions { flex-wrap:wrap; } }
  `],
})
export class AssetsPageComponent implements OnInit {
    private readonly workspace = inject(HttpWorkspaceApiService);
    private readonly auth = inject(AuthService);
    private readonly dictionaryApi = inject(DictionaryApiService);

    assets: AssetRecord[] = [];
    assetClasses: AssetClassOption[] = [];
    warehouses: DictionaryValueDto[] = [];
    locations: DictionaryValueDto[] = [];
    departments: DictionaryValueDto[] = [];
    selected: AssetDetails | null = null;
    query = '';
    tab: AssetTab = 'card';
    showCreate = false;
    isEngineer = false;
    error = '';
    notice = '';
    repairTitle = '';
    workDescription = '';
    repairResult: 'В эксплуатацию' | 'На склад' | 'Списать' = 'В эксплуатацию';
    repairToUserId: number | null = null;
    repairToWarehouseCode: string | null = null;
    draft = { id: '', serialNumber: '', name: '', type: '', className: '', owner: '', warehouse: '', warehouseCode: null as string | null, locationCode: null as string | null, departmentCode: null as string | null };
    draftAttributes: Record<string, string | number | boolean> = {};
    edit = { id: '', name: '', locationCode: null as string | null, departmentCode: null as string | null, attributes: {} as Record<string, string | number | boolean> };
    movement = { kind: 'Выдача со склада', from: '', to: '', toUserId: null as number | null, toWarehouseCode: null as string | null };
    ticketTypeFilter = '';
    ticketStatusFilter = '';
    ticketPriorityFilter = '';
    ticketAssigneeFilter = '';
    createdFrom = '';
    createdTo = '';

    get filteredAssets(): AssetRecord[] {
        const query = this.query.toLocaleLowerCase();
        return this.assets.filter(asset => `${asset.id} ${asset.name} ${asset.owner} ${asset.className} ${asset.lifecycle}`.toLocaleLowerCase().includes(query));
    }
    get activeAssetClasses(): AssetClassOption[] { return this.assetClasses.filter(assetClass => assetClass.active); }
    get activeWarehouses(): DictionaryValueDto[] { return this.warehouses.filter(value => !value.isArchived); }
    get activeLocations(): DictionaryValueDto[] { return this.locations.filter(value => !value.isArchived); }
    get activeDepartments(): DictionaryValueDto[] { return this.departments.filter(value => !value.isArchived); }
    get createAttributes(): AssetClassAttributeOption[] { return this.activeAssetClasses.find(assetClass => assetClass.name === this.draft.className)?.attributes ?? []; }
    get selectedAttributes(): AssetClassAttributeOption[] { return this.assetClasses.find(assetClass => assetClass.name === this.selected?.className)?.attributes ?? []; }
    get relatedTicketTypes(): string[] { return [...new Set((this.selected?.relatedTickets ?? []).map(ticket => ticket.type).filter((value): value is string => !!value))]; }
    get relatedTicketStatuses(): string[] { return [...new Set((this.selected?.relatedTickets ?? []).map(ticket => ticket.status))]; }
    get relatedTicketPriorities(): string[] { return [...new Set((this.selected?.relatedTickets ?? []).map(ticket => ticket.priority))]; }
    get relatedTicketAssignees(): string[] { return [...new Set((this.selected?.relatedTickets ?? []).map(ticket => ticket.assignee).filter((value): value is string => !!value))]; }
    get filteredRelatedTickets() {
      return (this.selected?.relatedTickets ?? []).filter(ticket => {
        const created = ticket.createdOn ?? '';
        return (!this.ticketTypeFilter || ticket.type === this.ticketTypeFilter)
          && (!this.ticketStatusFilter || ticket.status === this.ticketStatusFilter)
          && (!this.ticketPriorityFilter || ticket.priority === this.ticketPriorityFilter)
          && (!this.ticketAssigneeFilter || ticket.assignee === this.ticketAssigneeFilter)
          && (!this.createdFrom || created >= this.createdFrom)
          && (!this.createdTo || created <= this.createdTo);
      });
    }
    get activeRepairTicket() { return this.selected?.relatedTickets.find(ticket => ticket.type === 'Тикет на ремонт' && ticket.status !== 'Закрыта'); }

    ngOnInit(): void {
        this.isEngineer = this.auth.canRead();
        this.workspace.getAssetClasses().subscribe({ next: classes => { this.assetClasses = classes; if (!this.draft.className && classes.length) this.onCreateClassChange(classes[0].name); }, error: err => this.error = `Не удалось загрузить классы активов: ${err.message}` });
        this.loadDictionaryValues('warehouses', values => this.warehouses = values);
        this.loadDictionaryValues('locations', values => this.locations = values);
        this.loadDictionaryValues('departments', values => this.departments = values);
        this.loadAssets();
    }

    select(asset: AssetRecord): void {
        this.tab = 'card';
      this.workspace.getAssetDetails(asset.recordId || asset.id).subscribe({
            next: details => { this.selected = details; this.edit = { id: details.id, name: details.name, locationCode: details.locationCode ?? null, departmentCode: details.departmentCode ?? null, attributes: this.parseAttributes(details.attributes ?? {}, details.className) }; this.movement = { kind: details.lifecycle === 'На складе' ? 'Выдача со склада' : 'Возврат на склад', from: details.owner, to: '', toUserId: null, toWarehouseCode: null }; },
            error: err => this.error = err.message,
        });
    }

    createAsset(): void {
      const missing = this.createAttributes.find(attribute => attribute.required && !String(this.draftAttributes[attribute.code] ?? '').trim());
      if (missing) { this.error = `Заполните обязательный атрибут «${missing.name}».`; return; }
      const request = { ...this.draft, id: this.draft.id.trim() || undefined, attributes: this.stringifyAttributes(this.draftAttributes) };
        this.workspace.createAsset(request).subscribe({
        next: asset => { this.notice = `Создан актив: ${asset.id || asset.name} · ${asset.lifecycle}.`; this.showCreate = false; this.draft = { id: '', serialNumber: '', name: '', type: '', className: '', owner: '', warehouse: '', warehouseCode: null, locationCode: null, departmentCode: null }; this.draftAttributes = {}; if (this.activeAssetClasses.length) this.onCreateClassChange(this.activeAssetClasses[0].name); this.loadAssets(asset.recordId || asset.id || undefined); },
            error: err => this.error = err.message,
        });
    }

    saveAsset(): void {
        if (!this.selected) return;
        const key = this.selected.recordId || this.selected.id;
        const missing = this.selectedAttributes.find(attribute => attribute.required && !String(this.edit.attributes[attribute.code] ?? '').trim());
        if (missing) { this.error = `Заполните обязательный атрибут «${missing.name}».`; return; }
        const changes = { ...this.edit, id: this.edit.id.trim() || undefined, attributes: this.stringifyAttributes(this.edit.attributes) };
        this.workspace.updateAsset(key, changes).subscribe({ next: asset => { this.selected = asset; this.notice = 'Карточка сохранена, изменение записано в историю.'; this.loadAssets(key); }, error: err => this.error = err.message });
    }

    moveAsset(): void {
        if (!this.selected) return;
        const request: AssetMovement = { id: '', ...this.movement, actor: '', createdAt: '' };
        const { kind, from, to, toUserId, toWarehouseCode } = request;
        this.workspace.moveAsset(this.selected.recordId || this.selected.id, {
            kind,
            from,
            to,
            toUserId: kind === 'Выдача со склада' ? toUserId ?? undefined : undefined,
            toWarehouseCode: kind !== 'Выдача со склада' ? toWarehouseCode ?? undefined : undefined,
        }).subscribe({ next: asset => { this.selected = asset; this.notice = 'Движение оформлено, дата и время зарегистрированы.'; this.loadAssets(asset.recordId || asset.id); }, error: err => this.error = err.message });
    }

    createRepair(): void {
        if (!this.selected) return;
        this.workspace.createRepairTicket(this.selected.recordId || this.selected.id, this.repairTitle.trim()).subscribe({ next: ticket => { this.notice = `Создан ${ticket.id}; актив переведен в ремонт.`; this.repairTitle = ''; this.select(this.selected!); this.loadAssets(); }, error: err => this.error = err.message });
    }

    closeRepair(): void {
        if (!this.activeRepairTicket || !this.selected) return;
        this.workspace.closeRepairTicket(this.activeRepairTicket.id, {
            result: this.repairResult,
            workDescription: this.workDescription,
            repairToUserId: this.repairToUserId,
            repairToWarehouseCode: this.repairToWarehouseCode,
        }).subscribe({ next: () => { this.notice = 'Ремонт закрыт, результат и описание добавлены в историю актива.'; this.workDescription = ''; this.repairToUserId = null; this.repairToWarehouseCode = null; this.select(this.selected!); this.loadAssets(); }, error: err => this.error = err.message });
    }

    deleteAsset(): void {
        if (!this.selected || !confirm('Удалить актив на этапе «Закуплен»?')) return;
        this.workspace.deleteAsset(this.selected.recordId || this.selected.id).subscribe({ next: () => { this.selected = null; this.notice = 'Закупленный актив удален.'; this.loadAssets(); }, error: err => this.error = err.message });
    }

    onCreateClassChange(className: string): void {
      this.draft.className = className;
      this.draft.type = className;
      const selectedClass = this.activeAssetClasses.find(assetClass => assetClass.name === className);
      this.draftAttributes = this.parseAttributes(
        Object.fromEntries((selectedClass?.attributes ?? [])
          .filter(attribute => attribute.defaultValue !== undefined)
          .map(attribute => [attribute.code, attribute.defaultValue!])),
        className,
      );
    }

    private parseAttributes(values: Record<string, string>, className: string | undefined): Record<string, string | number | boolean> {
      const definitions = this.assetClasses.find(assetClass => assetClass.name === className)?.attributes ?? [];
      const parsed: Record<string, string | number | boolean> = {};
      for (const [code, value] of Object.entries(values)) {
        const dataType = definitions.find(attribute => attribute.code === code)?.dataType;
        if (dataType === 'bool') {
          parsed[code] = value.toLocaleLowerCase() === 'true';
        } else if (dataType === 'number' && value.trim()) {
          const number = Number(value);
          parsed[code] = Number.isFinite(number) ? number : value;
        } else {
          parsed[code] = value;
        }
      }
      return parsed;
    }

    private stringifyAttributes(attributes: Record<string, string | number | boolean>): Record<string, string> {
      return Object.fromEntries(Object.entries(attributes).map(([code, value]) => [code, String(value)]));
    }

    private loadAssets(selectId?: string): void {
        this.workspace.getAssets().subscribe({ next: assets => { this.assets = assets; const target = selectId ? assets.find(asset => asset.id === selectId || asset.recordId === selectId) : this.selected ? assets.find(asset => asset.id === this.selected?.id || asset.recordId === this.selected?.recordId) : undefined; if (target) this.select(target); }, error: err => this.error = err.message });
    }

    private loadDictionaryValues(code: string, onLoaded: (values: DictionaryValueDto[]) => void): void {
        this.dictionaryApi.getValues(code, true).subscribe({
            next: onLoaded,
            error: err => this.error = `Не удалось загрузить справочник «${code}»: ${err.message}`,
        });
    }
}
