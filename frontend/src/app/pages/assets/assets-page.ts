import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/auth/auth.service';
import { WorkspaceService } from '../../core/workspace/workspace.service';
import { AssetDetails, AssetLifecycle, AssetMovement, AssetRecord } from '../../core/workspace/workspace.models';

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
          <label>Идентификатор<input name="id" [(ngModel)]="draft.id" placeholder="Можно добавить позже" /></label>
          <label>Класс *<select name="className" [(ngModel)]="draft.className" required><option value="Ноутбук">Ноутбук</option><option value="Сервер">Сервер</option><option value="Монитор">Монитор</option><option value="Лицензия">Лицензия</option></select></label>
          <label>Склад <input name="warehouse" [(ngModel)]="draft.warehouse" [required]="!!draft.id" /></label>
          <label>Ответственный<input name="owner" [(ngModel)]="draft.owner" /></label>
          <label>Место<input name="location" [(ngModel)]="draft.location" /></label>
          <p class="form-note">Без идентификатора актив создаётся на этапе «Закуплен». Идентификатор уникален; при его указании склад обязателен.</p>
          <div class="form-actions"><button class="primary" type="submit" [disabled]="!draft.name.trim() || (!!draft.id && !draft.warehouse.trim())">Создать</button><button class="quiet" type="button" (click)="showCreate = false">Отмена</button></div>
        </form>
      }

      <div class="asset-layout">
        <section class="panel register">
          <header class="panel-head"><div><h2>Реестр</h2><small>{{ filteredAssets.length }} активов</small></div><label class="search"><span aria-hidden="true">⌕</span><input aria-label="Поиск активов" [(ngModel)]="query" placeholder="Название или ID" /></label></header>
          <div class="asset-list">
            @for (asset of filteredAssets; track asset.recordId || asset.id) {
              <button type="button" class="asset-row" [class.selected]="selected?.id === asset.id" (click)="select(asset)">
                <span class="asset-mark" aria-hidden="true">{{ asset.type.slice(0, 1) }}</span>
                <span class="asset-name"><strong>{{ asset.name }}</strong><small>{{ asset.id || 'Без идентификатора' }} · {{ asset.className }}</small></span>
                <span class="stage" [class.retired]="asset.lifecycle === 'Списан'">{{ asset.lifecycle }}</span>
              </button>
            }
            @if (!filteredAssets.length) { <p class="empty">Активы не найдены</p> }
          </div>
        </section>

        <section class="panel detail">
          @if (selected) {
            <header class="detail-head"><div><p class="eyebrow">{{ selected.id || 'Закупка' }} · {{ selected.className }}</p><h2>{{ selected.name }}</h2></div><span class="lifecycle">{{ selected.lifecycle }}</span></header>
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
                <label>Ответственный<input name="owner" [(ngModel)]="edit.owner" /></label>
                <label>Место<input name="location" [(ngModel)]="edit.location" /></label>
                <label>Подразделение<input name="department" [(ngModel)]="edit.department" /></label>
                @if (!selected.id) { <label>Склад для присвоения ID<input name="warehouse" [(ngModel)]="edit.warehouse" [required]="!!edit.id" /></label> }
                @for (attribute of attributeEntries; track attribute.key) { <label>{{ attribute.key }}<input [value]="attribute.value" disabled /></label> }
                <div class="card-actions"><button class="primary" type="submit" [disabled]="!!edit.id && !edit.warehouse.trim()">Сохранить карточку</button>
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
              <div class="table-scroll"><table><thead><tr><th>Номер</th><th>Тип</th><th>Тема</th><th>Автор</th><th>Исполнитель</th><th>Статус</th><th>Критичность</th><th>Создана</th><th>Закрыта</th></tr></thead><tbody>
                @for (ticket of selected.relatedTickets; track ticket.id) { <tr><td>{{ ticket.id }}</td><td>{{ ticket.type }}</td><td>{{ ticket.title }}</td><td>{{ ticket.requester }}</td><td>{{ ticket.assignee }}</td><td>{{ ticket.status }}</td><td>{{ ticket.priority }}</td><td>{{ ticket.createdOn }}</td><td>{{ ticket.closedOn || '—' }}</td></tr> }
                @if (!selected.relatedTickets.length) { <tr><td colspan="9" class="empty">Связанных заявок пока нет</td></tr> }
              </tbody></table></div>
            }

            @if (tab === 'movements') {
              @if (isEngineer && selected.lifecycle !== 'Списан') {
                <form class="movement-form" (ngSubmit)="moveAsset()">
                  <label>Вид движения<select name="kind" [(ngModel)]="movement.kind"><option>Передача сотруднику</option><option>Перемещение</option><option>Выдача со склада</option><option>Возврат на склад</option></select></label>
                  <label>Откуда<input name="from" [(ngModel)]="movement.from" required /></label>
                  <label>Куда / кому<input name="to" [(ngModel)]="movement.to" required /></label>
                  <button class="primary" type="submit" [disabled]="!movement.from.trim() || !movement.to.trim()">Оформить</button>
                </form>
              }
              @if (isEngineer && ['На складе', 'В эксплуатации'].includes(selected.lifecycle || '')) {
                <form class="repair-form" (ngSubmit)="createRepair()"><label>Тикет на ремонт<input name="repairTitle" [(ngModel)]="repairTitle" placeholder="Что требуется проверить или исправить" required /></label><button class="secondary" type="submit">Создать тикет на ремонт</button></form>
              }
              @if (isEngineer && selected.lifecycle === 'В ремонте' && activeRepairTicket) {
                <form class="repair-form close-repair" (ngSubmit)="closeRepair()"><strong>{{ activeRepairTicket.id }} · {{ activeRepairTicket.title }}</strong><label>Результат ремонта<select name="repairResult" [(ngModel)]="repairResult"><option value="В эксплуатацию">Вернуть в эксплуатацию</option><option value="На склад">Перевести на склад</option><option value="Списать">Списать</option></select></label><label>Что ремонтировалось и что менялось *<textarea name="workDescription" [(ngModel)]="workDescription" required></textarea></label><button class="primary" type="submit" [disabled]="!workDescription.trim()">Закрыть тикет ремонта</button></form>
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
    @media(max-width:680px) { .page { padding:20px 13px 28px; } .heading { align-items:flex-start; flex-direction:column; } .asset-list { grid-template-columns:1fr; } .detail { padding:13px; } .card-form,.create { grid-template-columns:1fr; } .create h2,.form-note,.form-actions { grid-column:auto; } .movement-form,.repair-form,.close-repair { grid-template-columns:1fr; } .close-repair label:nth-of-type(2) { grid-column:auto; } .movement-list article { grid-template-columns:1fr; gap:4px; } .card-actions { flex-wrap:wrap; } }
  `],
})
export class AssetsPageComponent implements OnInit {
    private readonly workspace = inject(WorkspaceService);
    private readonly auth = inject(AuthService);

    assets: AssetRecord[] = [];
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
    draft = { id: '', name: '', type: 'Ноутбук', className: 'Ноутбук', owner: '', warehouse: '', location: '', department: '' };
    edit = { id: '', name: '', owner: '', location: '', department: '', warehouse: '' };
    movement = { kind: 'Передача сотруднику', from: '', to: '' };

    get filteredAssets(): AssetRecord[] {
        const query = this.query.toLocaleLowerCase();
        return this.assets.filter(asset => `${asset.id} ${asset.name} ${asset.owner} ${asset.className} ${asset.lifecycle}`.toLocaleLowerCase().includes(query));
    }
    get attributeEntries(): { key: string; value: string }[] { return Object.entries(this.selected?.attributes ?? {}).map(([key, value]) => ({ key, value })); }
    get activeRepairTicket() { return this.selected?.relatedTickets.find(ticket => ticket.type === 'Тикет на ремонт' && ticket.status !== 'Закрыта'); }

    ngOnInit(): void {
        this.isEngineer = this.auth.canRead();
        this.loadAssets();
    }

    select(asset: AssetRecord): void {
        this.tab = 'card';
      this.workspace.getAssetDetails(asset.recordId || asset.id).subscribe({
            next: details => { this.selected = details; this.edit = { id: details.id, name: details.name, owner: details.owner, location: details.location ?? '', department: details.department ?? '', warehouse: details.warehouse ?? '' }; this.movement = { kind: 'Передача сотруднику', from: details.owner, to: '' }; },
            error: err => this.error = err.message,
        });
    }

    createAsset(): void {
        const request = { ...this.draft, id: this.draft.id.trim() || undefined, attributes: {} };
        this.workspace.createAsset(request).subscribe({
            next: asset => { this.notice = `Создан актив: ${asset.id || asset.name} · ${asset.lifecycle}.`; this.showCreate = false; this.draft = { id: '', name: '', type: 'Ноутбук', className: 'Ноутбук', owner: '', warehouse: '', location: '', department: '' }; this.loadAssets(asset.recordId || asset.id || undefined); },
            error: err => this.error = err.message,
        });
    }

    saveAsset(): void {
        if (!this.selected) return;
        const key = this.selected.recordId || this.selected.id;
        const changes = { ...this.edit, id: this.edit.id.trim() || undefined };
        this.workspace.updateAsset(key, changes).subscribe({ next: asset => { this.selected = asset; this.notice = 'Карточка сохранена, изменение записано в историю.'; this.loadAssets(key); }, error: err => this.error = err.message });
    }

    moveAsset(): void {
        if (!this.selected) return;
        const request: AssetMovement = { id: '', ...this.movement, actor: '', createdAt: '' };
        const { kind, from, to } = request;
        this.workspace.moveAsset(this.selected.recordId || this.selected.id, { kind, from, to }).subscribe({ next: asset => { this.selected = asset; this.notice = 'Движение оформлено, дата и время зарегистрированы.'; this.loadAssets(asset.recordId || asset.id); }, error: err => this.error = err.message });
    }

    createRepair(): void {
        if (!this.selected) return;
        this.workspace.createRepairTicket(this.selected.recordId || this.selected.id, this.repairTitle.trim()).subscribe({ next: ticket => { this.notice = `Создан ${ticket.id}; актив переведен в ремонт.`; this.repairTitle = ''; this.select(this.selected!); this.loadAssets(); }, error: err => this.error = err.message });
    }

    closeRepair(): void {
        if (!this.activeRepairTicket || !this.selected) return;
        this.workspace.closeRepairTicket(this.activeRepairTicket.id, { result: this.repairResult, workDescription: this.workDescription }).subscribe({ next: () => { this.notice = 'Ремонт закрыт, результат и описание добавлены в историю актива.'; this.workDescription = ''; this.select(this.selected!); this.loadAssets(); }, error: err => this.error = err.message });
    }

    deleteAsset(): void {
        if (!this.selected || !confirm('Удалить актив на этапе «Закуплен»?')) return;
        this.workspace.deleteAsset(this.selected.recordId || this.selected.id).subscribe({ next: () => { this.selected = null; this.notice = 'Закупленный актив удален.'; this.loadAssets(); }, error: err => this.error = err.message });
    }

    private loadAssets(selectId?: string): void {
        this.workspace.getAssets().subscribe({ next: assets => { this.assets = assets; const target = selectId ? assets.find(asset => asset.id === selectId || asset.recordId === selectId) : this.selected ? assets.find(asset => asset.id === this.selected?.id || asset.recordId === this.selected?.recordId) : undefined; if (target) this.select(target); }, error: err => this.error = err.message });
    }
}
