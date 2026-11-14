import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { concatMap, from, toArray } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { AssetClassApiService } from '../../core/asset-class/asset-class.service';
import {
    AssetClassDto,
    AssetClassDetailDto,
    AssetClassAttributeDto,
    ASSET_ATTRIBUTE_DATA_TYPES,
} from '../../core/asset-class/asset-class.models';

@Component({
    selector: 'app-asset-classes',
    standalone: true,
    imports: [CommonModule, FormsModule],
    template: `
    <section class="ac-page">
      <header class="page-header">
        <div>
          <h1>Классы активов</h1>
          <p class="subtitle">Настройка классов активов и их атрибутов</p>
        </div>
        @if (isAdmin) {
          <div class="header-actions">
            <button type="button" class="secondary basic-classes-button" (click)="addBasicClasses()" [disabled]="addingBasicClasses">
              {{ addingBasicClasses ? 'Добавление…' : 'Добавить базовые классы активов' }}
            </button>
            <button type="button" (click)="openCreateDialog()">
              + Создать класс активов
            </button>
          </div>
        }
      </header>

      @if (errorMessage && !showCreateDialog) {
        <div class="error">{{ errorMessage }}</div>
      }
      @if (successMessage) {
        <div class="success">{{ successMessage }}</div>
      }

      @if (showCreateDialog) {
        <div class="create-modal-backdrop" role="presentation" (click)="onCreateBackdropClick($event)" (keydown.escape)="closeCreateDialog()">
          <section class="create-modal" role="dialog" aria-modal="true" aria-labelledby="create-class-title">
            <header class="create-modal-header">
              <h2 id="create-class-title">Создать класс активов</h2>
              <button type="button" class="modal-close" aria-label="Закрыть" (click)="closeCreateDialog()">×</button>
            </header>
            <p class="modal-hint">Укажите код для API и отображаемое название класса.</p>
            @if (errorMessage) { <div class="error">{{ errorMessage }}</div> }
            <form class="create-form" (ngSubmit)="createClass()">
              <label>Код
                <input name="classCode" [(ngModel)]="newCode" placeholder="Например, laptop" required />
              </label>
              <label>Название
                <input name="className" [(ngModel)]="newName" placeholder="Например, Ноутбуки" required />
              </label>
              <footer class="create-modal-actions">
                <button type="button" class="secondary" (click)="closeCreateDialog()">Отмена</button>
                <button type="submit" [disabled]="!newCode.trim() || !newName.trim()">Создать класс</button>
              </footer>
            </form>
          </section>
        </div>
      }

      <div class="layout">
        <aside class="list">
          <h3>Классы ({{ classes.length }})</h3>
          <input class="class-search" type="search" aria-label="Поиск по классам активов"
            [(ngModel)]="classSearch" placeholder="Поиск по названию или коду" />
          @for (c of filteredClasses; track c.id) {
            <button type="button" class="item" [class.active]="selected?.id === c.id"
              [attr.aria-pressed]="selected?.id === c.id" (click)="select(c)">
              <div>
                <strong>{{ c.name }}</strong>
                <small>{{ c.code }} · атрибутов: {{ c.attributesCount }} · активов: {{ c.assetsCount }}</small>
              </div>
              @if (!c.isActive) {
                <span class="badge inactive">выкл.</span>
              }
            </button>
          }
          @if (classes.length === 0) {
            <p class="empty">Нет классов активов</p>
          } @else if (filteredClasses.length === 0) {
            <p class="empty">По вашему запросу ничего не найдено.</p>
          }
        </aside>

        <main class="detail">
          @if (selected) {
            <div class="detail-header">
              <div>
                <h2>{{ selected.name }}</h2>
                <div class="class-meta">
                  <span class="code-tag">{{ selected.code }}</span>
                  <span>Активов: {{ selected.assetsCount }}</span>
                  <span>Атрибутов: {{ selected.attributesCount }}</span>
                </div>
              </div>
              <div class="actions">
                @if (isAdmin) {
                  <button class="secondary" (click)="startEditingClass()">Изменить класс</button>
                  @if (selected.isActive) {
                    <button class="secondary" (click)="deactivate()">Деактивировать</button>
                  } @else {
                    <button class="secondary" (click)="activate()">Активировать</button>
                  }
                  @if (selected.assetsCount === 0) {
                    <button class="delete-outline" (click)="deleteClass()">Удалить</button>
                  } @else {
                    <span class="hint">Удаление запрещено: есть активы</span>
                  }
                }
              </div>
            </div>

            @if (isAdmin && editingClassName) {
              <form class="edit-class-form" (ngSubmit)="saveClassName()">
                <input name="editClassName" [(ngModel)]="editClassName" placeholder="Название класса" required />
                <button type="submit" [disabled]="!editClassName.trim()">Сохранить</button>
                <button type="button" class="secondary" (click)="editingClassName = false">Отмена</button>
              </form>
            }

            <h3 class="section-title">Атрибуты класса ({{ attributes.length }})</h3>

            @if (isAdmin) {
              <div class="attribute-form">
                <label>Код
                  <input [(ngModel)]="newAttrCode" placeholder="Например, serial_number" />
                </label>
                <label>Название
                  <input [(ngModel)]="newAttrName" placeholder="Название атрибута" />
                </label>
                <label>Тип
                  <select [(ngModel)]="newAttrDataType">
                    @for (t of dataTypes; track t.value) {
                      <option [value]="t.value">{{ t.label }}</option>
                    }
                  </select>
                </label>
                <label class="chk" title="Пользователь должен заполнить этот атрибут при создании или изменении актива.">
                  <input type="checkbox" [(ngModel)]="newAttrRequired" />
                  <span>Обязательный атрибут</span>
                </label>
                <button (click)="createAttribute()" [disabled]="!newAttrCode.trim() || !newAttrName.trim()">Добавить атрибут</button>
              </div>
            }

            <table>
              <thead>
                <tr>
                  @if (isAdmin) { <th class="reorder-column" aria-label="Изменить порядок"></th> }
                  <th>Код</th>
                  <th>Название</th>
                  <th>Тип</th>
                  <th>Обязательный атрибут</th>
                  <th>По умолчанию</th>
                  @if (isAdmin) { <th>Действия</th> }
                </tr>
              </thead>
              <tbody>
                @for (a of orderedAttributes; track a.id; let i = $index) {
                  <tr>
                    @if (isAdmin) {
                      <td class="reorder-actions">
                        <button type="button" class="reorder-button" title="Переместить выше" aria-label="Переместить выше"
                          [disabled]="i === 0 || isReorderingAttributes" (click)="moveAttribute(a, -1)">↑</button>
                        <button type="button" class="reorder-button" title="Переместить ниже" aria-label="Переместить ниже"
                          [disabled]="i === orderedAttributes.length - 1 || isReorderingAttributes" (click)="moveAttribute(a, 1)">↓</button>
                      </td>
                    }
                    <td>{{ a.code }}</td>
                    <td>{{ a.name }}</td>
                    <td>{{ dataTypeLabel(a.dataType) }}</td>
                    <td>{{ a.isRequired ? 'Да' : 'Нет' }}</td>
                    <td>{{ a.defaultValue ?? '—' }}</td>
                    @if (isAdmin) {
                      <td class="actions">
                        <button class="delete-outline" (click)="deleteAttribute(a)">Удалить</button>
                      </td>
                    }
                  </tr>
                }
                @if (attributes.length === 0) {
                  <tr>
                    <td [attr.colspan]="isAdmin ? 7 : 5" class="empty-cell">
                      <div class="empty-state">
                        <span class="empty-icon" aria-hidden="true">＋</span>
                        <strong>У этого класса пока нет атрибутов</strong>
                        @if (isAdmin) { <span>Заполните поля выше, чтобы добавить первый атрибут.</span> }
                      </div>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          } @else {
            <p class="empty">Выберите класс слева</p>
          }
        </main>
      </div>
    </section>
  `,
    styles: [
        `
      .ac-page { padding: 24px; color: #202734; }
      .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; gap: 16px; flex-wrap: wrap; }
      h1, h2, h3 { margin: 0; }
      h1 { font-size: 24px; }
      .subtitle { margin: 4px 0 0; color: #6b7280; font-size: 13px; }
      .header-actions, .edit-class-form { display: flex; gap: 8px; flex-wrap: wrap; align-items: center; }
      .edit-class-form { margin-bottom: 16px; }
      input, select { min-width: 0; padding: 8px 10px; border: 1px solid #d1d5db; border-radius: 7px; color: #202734; font: inherit; font-size: 13px; }
      input:focus, select:focus { border-color: #6b91d6; outline: 2px solid #e9f0ff; }
      button { padding: 8px 13px; border: 1px solid #315fac; border-radius: 7px; background: #3f6fbd; color: #fff; font: inherit; font-size: 13px; cursor: pointer; }
      button:hover:not(:disabled) { background: #315fac; }
      button:disabled { border-color: #e2e5e9; background: #e8eaed; color: #a0a5ad; cursor: not-allowed; }
      button.secondary { border-color: #d7dce4; background: #fff; color: #4b5563; }
      button.secondary:hover:not(:disabled) { background: #f4f6f8; }
      .basic-classes-button { border-color: #d7dce4; background: #fff; color: #4b5563; }
      .basic-classes-button:hover:not(:disabled) { background: #f4f6f8; }
      button.delete-outline { padding: 7px 11px; border: 1px solid #e7bcbc; background: #fff; color: #a93c3c; }
      button.delete-outline:hover:not(:disabled) { border-color: #c97b7b; background: #fff7f7; color: #8e2929; }
      .layout { display: grid; grid-template-columns: 360px minmax(0, 1fr); gap: 16px; }
      .list, .detail { min-width: 0; padding: 16px; border: 1px solid #e7eaf0; border-radius: 10px; background: #fff; }
      .class-search { width: 100%; margin: 10px 0 12px; }
      .item { width: 100%; display: flex; justify-content: space-between; align-items: center; gap: 10px; margin-bottom: 6px; padding: 10px 12px; border: 1px solid #e0e5ed; border-radius: 8px; background: #fff; color: #202734; text-align: left; cursor: pointer; transition: background .16s ease, border-color .16s ease; }
      .item:hover { border-color: #9ab5e3; background: #f7f9fd; color: #233b5b; }
      .item.active { border-color: #7195d0; background: #f1f5fc; box-shadow: inset 3px 0 #5681c8; }
      .item:focus-visible { outline: 2px solid #5681c8; outline-offset: 2px; }
      .item > div { min-width: 0; }
      .item strong { display: block; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
      .item small { display: block; margin-top: 3px; color: #6b7280; font-size: 12px; }
      .badge { flex: 0 0 auto; padding: 2px 8px; border-radius: 12px; background: #f0f2f5; color: #606b79; font-size: 12px; }
      .badge.inactive { background: #fee2e2; color: #991b1b; }
      .detail-header { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 16px; gap: 16px; flex-wrap: wrap; }
      .detail-header .actions { display: flex; gap: 8px; align-items: center; flex-wrap: wrap; }
      .detail-header small { display: block; margin-top: 7px; }
      .class-meta { display: flex; align-items: center; flex-wrap: wrap; gap: 8px; margin-top: 8px; color: #6b7280; font-size: 12px; }
      .class-meta > span + span { padding-left: 8px; border-left: 1px solid #d7dce4; }
      .code-tag { padding: 3px 8px; border: 1px solid #dce4f0; border-radius: 12px; background: #f4f7fb; color: #536982; font-family: ui-monospace, monospace; }
      .hint { color: #6b7280; font-size: 12px; }
      .section-title { margin: 18px 0 10px; font-size: 16px; }
      .attribute-form { display: grid; grid-template-columns: minmax(110px, 1fr) minmax(140px, 1.3fr) minmax(130px, 1fr) auto auto; align-items: end; gap: 10px; margin-bottom: 16px; padding: 14px; border: 1px solid #e7eaf0; border-radius: 9px; background: #fafbfc; }
      .attribute-form > label:not(.chk) { display: grid; gap: 5px; color: #4b5563; font-size: 12px; }
      .chk { display: flex; align-items: center; gap: 7px; min-height: 36px; color: #4b5563; font-size: 12px; white-space: nowrap; cursor: help; }
      .chk input { width: 16px; height: 16px; margin: 0; accent-color: #3f6fbd; }
      table { width: 100%; border-collapse: collapse; }
      th, td { padding: 9px 10px; border-bottom: 1px solid #edf0f3; text-align: left; font-size: 13px; }
      th { background: #f8f9fb; color: #596372; font-size: 12px; font-weight: 600; }
      .reorder-actions { width: 1%; padding-right: 0; white-space: nowrap; }
      .reorder-button { width: 26px; height: 26px; padding: 0; border-color: transparent; background: transparent; color: #657185; font-size: 16px; }
      .reorder-button:hover:not(:disabled) { border-color: #dfe5ee; background: #f2f5f9; color: #315fac; }
      .reorder-button:disabled { border-color: transparent; background: transparent; color: #c9cdd3; }
      .actions { display: flex; gap: 6px; }
      .error { margin-bottom: 12px; padding: 10px; border-radius: 7px; background: #fef2f2; color: #b91c1c; font-size: 13px; }
      .success { margin-bottom: 12px; padding: 10px; border-radius: 7px; background: #f0fdf4; color: #166534; font-size: 13px; }
      .empty { color: #6b7280; font-size: 13px; }
      .empty-cell { padding: 0; }
      .empty-state { display: grid; justify-items: center; gap: 8px; padding: 32px 16px; color: #6b7280; text-align: center; }
      .empty-state strong { color: #374151; font-size: 14px; }
      .empty-state span:last-child { font-size: 12px; }
      .empty-icon { display: grid; width: 34px; height: 34px; place-items: center; border-radius: 50%; background: #eef4ff; color: #3475d1; font-size: 20px; }
      .create-modal-backdrop { position: fixed; z-index: 1000; inset: 0; display: grid; place-items: center; padding: 20px; background: rgb(17 24 39 / 42%); }
      .create-modal { width: min(100%, 500px); padding: 22px; border: 1px solid #e5e7eb; border-radius: 12px; background: #fff; box-shadow: 0 18px 48px rgb(15 23 42 / 20%); }
      .create-modal-header, .create-modal-actions { display: flex; align-items: center; justify-content: space-between; gap: 12px; }
      .create-modal-header { margin-bottom: 8px; }
      .create-modal-header h2 { font-size: 18px; }
      .modal-hint { margin: 0 0 16px; color: #6b7280; font-size: 13px; }
      .modal-close { width: 32px; height: 32px; padding: 0; border: 0; background: transparent; color: #737d8b; font-size: 24px; }
      .create-form { display: grid; gap: 12px; }
      .create-form label { display: grid; gap: 5px; color: #4b5563; font-size: 12px; }
      .create-modal-actions { justify-content: flex-end; margin-top: 6px; }
      .empty { color: #6b7280; font-size: 13px; }
      @media (max-width: 1000px) { .layout { grid-template-columns: 1fr; } }
      @media (max-width: 760px) { .attribute-form { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
      @media (max-width: 520px) { .ac-page { padding: 16px; } .attribute-form { grid-template-columns: 1fr; } .detail { overflow-x: auto; } table { min-width: 620px; } }
    `,
    ],
})
export class AssetClassesComponent implements OnInit {
    private readonly api = inject(AssetClassApiService);
    private readonly auth = inject(AuthService);
    private readonly cdr = inject(ChangeDetectorRef);

    classes: AssetClassDto[] = [];
    selected: AssetClassDetailDto | null = null;
    attributes: AssetClassAttributeDto[] = [];

    isAdmin = false;
    addingBasicClasses = false;
    editingClassName = false;
    editClassName = '';
    errorMessage = '';
    successMessage = '';
    isReorderingAttributes = false;

    readonly dataTypes = ASSET_ATTRIBUTE_DATA_TYPES;

    // Форма класса
    newCode = '';
    newName = '';
    showCreateDialog = false;
    classSearch = '';

    // Форма атрибута
    newAttrCode = '';
    newAttrName = '';
    newAttrDataType = 'string';
    newAttrRequired = false;

    get filteredClasses(): AssetClassDto[] {
        const search = this.classSearch.trim().toLocaleLowerCase();
        return search
            ? this.classes.filter(assetClass =>
                `${assetClass.name} ${assetClass.code}`.toLocaleLowerCase().includes(search))
            : this.classes;
    }

    get orderedAttributes(): AssetClassAttributeDto[] {
        return [...this.attributes].sort((a, b) => a.sortOrder - b.sortOrder || a.id - b.id);
    }

    ngOnInit(): void {
        this.isAdmin = this.auth.isAdmin();
        this.loadAll();
    }

    private clearMessages(): void {
        this.errorMessage = '';
        this.successMessage = '';
    }

    openCreateDialog(): void {
        this.clearMessages();
        this.showCreateDialog = true;
    }

    closeCreateDialog(): void {
        this.showCreateDialog = false;
        this.newCode = '';
        this.newName = '';
        this.errorMessage = '';
    }

    onCreateBackdropClick(event: MouseEvent): void {
        if (event.target === event.currentTarget) this.closeCreateDialog();
    }

    private loadAll(): void {
        this.api.getAll().subscribe({
            next: list => {
                this.classes = list;
                this.cdr.detectChanges();
            },
            error: err => {
                this.errorMessage = `Не удалось загрузить классы: ${err.status}`;
                this.cdr.detectChanges();
            },
        });
    }

    select(c: AssetClassDto): void {
        this.clearMessages();
        this.api.getById(c.id).subscribe({
            next: detail => {
                this.selected = detail;
                this.attributes = detail.attributes;
                this.cdr.detectChanges();
            },
            error: err => {
                this.errorMessage = `Ошибка загрузки класса: ${err.status}`;
                this.cdr.detectChanges();
            },
        });
    }

    createClass(): void {
        this.clearMessages();
        this.api.create({ code: this.newCode.trim(), name: this.newName.trim() }).subscribe({
            next: created => {
                this.newCode = '';
                this.newName = '';
                this.showCreateDialog = false;
                this.successMessage = `Класс «${created.name}» создан`;
                this.loadAll();
            },
            error: err => {
                this.errorMessage = `Ошибка: ${err.status} ${err.error?.message ?? ''}`;
                this.cdr.detectChanges();
            },
        });
    }

    addBasicClasses(): void {
        this.clearMessages();
        const existingCodes = new Set(this.classes.map(assetClass => assetClass.code));
        const missingClasses = BASIC_ASSET_CLASSES.filter(assetClass => !existingCodes.has(assetClass.code));
        if (!missingClasses.length) {
            this.successMessage = 'Базовые классы уже добавлены';
            return;
        }

        this.addingBasicClasses = true;
        from(missingClasses).pipe(
            concatMap(assetClass => this.api.create(assetClass)),
            toArray(),
        ).subscribe({
            next: created => {
                this.addingBasicClasses = false;
                this.successMessage = `Добавлено базовых классов: ${created.length}`;
                this.loadAll();
            },
            error: err => {
                this.addingBasicClasses = false;
                this.errorMessage = `Не удалось добавить базовые классы: ${err.status} ${err.error?.message ?? ''}`;
                this.loadAll();
                this.cdr.detectChanges();
            },
        });
    }

    startEditingClass(): void {
        if (!this.selected) return;
        this.clearMessages();
        this.editClassName = this.selected.name;
        this.editingClassName = true;
    }

    saveClassName(): void {
        if (!this.selected || !this.editClassName.trim()) return;
        this.clearMessages();
        this.api.update(this.selected.id, { name: this.editClassName.trim() }).subscribe({
            next: updated => {
                this.editingClassName = false;
                this.editClassName = '';
                this.loadAll();
                this.select({ ...this.selected!, name: updated.name });
                this.successMessage = `Класс переименован в «${updated.name}»`;
                this.cdr.detectChanges();
            },
            error: err => {
                this.errorMessage = `Не удалось изменить класс: ${err.status} ${err.error?.message ?? ''}`;
                this.cdr.detectChanges();
            },
        });
    }

    deleteClass(): void {
        if (!this.selected || !confirm(`Удалить класс «${this.selected.name}»?`)) return;
        this.clearMessages();
        const id = this.selected.id;
        this.api.delete(id).subscribe({
            next: () => {
                this.selected = null;
                this.attributes = [];
                this.successMessage = 'Класс удалён';
                this.loadAll();
                this.cdr.detectChanges();
            },
            error: err => {
                this.errorMessage = `Ошибка: ${err.status} ${err.error?.message ?? ''}`;
                this.cdr.detectChanges();
            },
        });
    }

    deactivate(): void {
        if (!this.selected) return;
        this.clearMessages();
        this.api.deactivate(this.selected.id).subscribe({
            next: () => {
                this.successMessage = 'Класс деактивирован';
                this.reloadSelected();
                this.loadAll();
            },
            error: err => {
                this.errorMessage = `Ошибка: ${err.status}`;
                this.cdr.detectChanges();
            },
        });
    }

    activate(): void {
        if (!this.selected) return;
        this.clearMessages();
        this.api.activate(this.selected.id).subscribe({
            next: () => {
                this.successMessage = 'Класс активирован';
                this.reloadSelected();
                this.loadAll();
            },
            error: err => {
                this.errorMessage = `Ошибка: ${err.status}`;
                this.cdr.detectChanges();
            },
        });
    }

    createAttribute(): void {
        if (!this.selected) return;
        this.clearMessages();
        this.api.createAttribute(this.selected.id, {
            code: this.newAttrCode.trim(),
            name: this.newAttrName.trim(),
            dataType: this.newAttrDataType,
            isRequired: this.newAttrRequired,
            defaultValue: null,
            options: null,
            sortOrder: this.orderedAttributes.length
                ? Math.max(...this.orderedAttributes.map(attribute => attribute.sortOrder)) + 1
                : 0,
        }).subscribe({
            next: () => {
                this.newAttrCode = '';
                this.newAttrName = '';
                this.newAttrRequired = false;
                this.successMessage = 'Атрибут добавлен';
                this.reloadSelected();
                this.loadAll();
            },
            error: err => {
                this.errorMessage = `Ошибка: ${err.status} ${err.error?.message ?? ''}`;
                this.cdr.detectChanges();
            },
        });
    }

    moveAttribute(attribute: AssetClassAttributeDto, direction: -1 | 1): void {
        if (!this.selected || this.isReorderingAttributes) return;
        const classId = this.selected.id;
        const ordered = this.orderedAttributes;
        const currentIndex = ordered.findIndex(item => item.id === attribute.id);
        const nextIndex = currentIndex + direction;
        if (currentIndex < 0 || nextIndex < 0 || nextIndex >= ordered.length) return;

        [ordered[currentIndex], ordered[nextIndex]] = [ordered[nextIndex], ordered[currentIndex]];
        this.clearMessages();
        this.isReorderingAttributes = true;
        from(ordered).pipe(
            concatMap((item, index) => this.api.updateAttribute(classId, item.id, {
                name: item.name,
                dataType: item.dataType,
                isRequired: item.isRequired,
                defaultValue: item.defaultValue,
                options: item.options,
                sortOrder: index,
            })),
            toArray(),
        ).subscribe({
            next: updated => {
                if (this.selected?.id === classId) this.attributes = updated;
                this.isReorderingAttributes = false;
                this.cdr.detectChanges();
            },
            error: err => {
                this.isReorderingAttributes = false;
                this.errorMessage = `Не удалось изменить порядок атрибутов: ${err.status} ${err.error?.message ?? ''}`;
                if (this.selected?.id === classId) this.reloadSelected();
                this.cdr.detectChanges();
            },
        });
    }

    deleteAttribute(a: AssetClassAttributeDto): void {
        if (!this.selected || !confirm(`Удалить атрибут «${a.name}»?`)) return;
        this.clearMessages();
        this.api.deleteAttribute(this.selected.id, a.id).subscribe({
            next: () => {
                this.successMessage = 'Атрибут удалён';
                this.reloadSelected();
                this.loadAll();
            },
            error: err => {
                this.errorMessage = `Ошибка: ${err.status} ${err.error?.message ?? ''}`;
                this.cdr.detectChanges();
            },
        });
    }

    private reloadSelected(): void {
        if (!this.selected) return;
        this.api.getById(this.selected.id).subscribe({
            next: detail => {
                this.selected = detail;
                this.attributes = detail.attributes;
                this.cdr.detectChanges();
            },
        });
    }

    dataTypeLabel(value: string): string {
        return this.dataTypes.find(t => t.value === value)?.label ?? value;
    }
}

const BASIC_ASSET_CLASSES = [
    { code: 'server', name: 'Серверы' },
    { code: 'laptop', name: 'Ноутбуки' },
    { code: 'monoblock', name: 'Моноблоки' },
    { code: 'monitor', name: 'Мониторы' },
    { code: 'desktop', name: 'Рабочие станции' },
    { code: 'printer', name: 'Принтеры и МФУ' },
    { code: 'network_equipment', name: 'Сетевое оборудование' },
    { code: 'ups', name: 'Источники бесперебойного питания' },
    { code: 'projector', name: 'Проекторы' },
];