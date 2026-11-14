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
          <p class="subtitle">Настройка классов и их атрибутов (АКТ-05, АКТ-07)</p>
        </div>
        @if (isAdmin) {
          <div class="header-actions">
            <button type="button" class="secondary" (click)="addBasicClasses()" [disabled]="addingBasicClasses">
              {{ addingBasicClasses ? 'Добавление…' : 'Добавить базовые классы активов' }}
            </button>
            <button type="button" (click)="showCreateForm = !showCreateForm">
              {{ showCreateForm ? 'Отмена' : 'Создать класс активов' }}
            </button>
          </div>
        }
      </header>

      @if (isAdmin && showCreateForm) {
        <form class="create-form" (ngSubmit)="createClass()">
          <input name="classCode" [(ngModel)]="newCode" placeholder="code (a-z, _)" required />
          <input name="className" [(ngModel)]="newName" placeholder="Название" required />
          <button type="submit" [disabled]="!newCode.trim() || !newName.trim()">Создать класс</button>
        </form>
      }

      @if (errorMessage) {
        <div class="error">{{ errorMessage }}</div>
      }
      @if (successMessage) {
        <div class="success">{{ successMessage }}</div>
      }

      <div class="layout">
        <aside class="list">
          <h3>Классы ({{ classes.length }})</h3>
          @for (c of classes; track c.id) {
            <button type="button" class="item" [class.active]="selected?.id === c.id"
              [attr.aria-pressed]="selected?.id === c.id" (click)="select(c)">
              <div>
                <strong>{{ c.name }}</strong>
                <small>{{ c.code }} · атрибутов: {{ c.attributesCount }} · активов: {{ c.assetsCount }}</small>
              </div>
              @if (!c.isActive) {
                <span class="badge inactive">выкл.</span>
              }
              <span class="item-action">{{ selected?.id === c.id ? 'Выбран' : 'Открыть' }}</span>
            </button>
          }
          @if (classes.length === 0) {
            <p class="empty">Нет классов активов</p>
          }
        </aside>

        <main class="detail">
          @if (selected) {
            <div class="detail-header">
              <div>
                <h2>{{ selected.name }}</h2>
                <small>code: {{ selected.code }} · id: {{ selected.id }} · активов: {{ selected.assetsCount }} · атрибутов: {{ selected.attributesCount }}</small>
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
                    <button class="danger" (click)="deleteClass()">Удалить</button>
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
              <div class="create-attr-form">
                <input [(ngModel)]="newAttrCode" placeholder="code" />
                <input [(ngModel)]="newAttrName" placeholder="Название" />
                <select [(ngModel)]="newAttrDataType">
                  @for (t of dataTypes; track t.value) {
                    <option [value]="t.value">{{ t.label }}</option>
                  }
                </select>
                <label class="chk"><input type="checkbox" [(ngModel)]="newAttrRequired" /> обязательный</label>
                <input type="number" [(ngModel)]="newAttrSort" placeholder="Sort" />
                <button (click)="createAttribute()" [disabled]="!newAttrCode || !newAttrName">Добавить</button>
              </div>
            }

            <table>
              <thead>
                <tr>
                  <th>Id</th>
                  <th>Code</th>
                  <th>Название</th>
                  <th>Тип</th>
                  <th>Обяз.</th>
                  <th>По умолч.</th>
                  <th>Sort</th>
                  @if (isAdmin) { <th></th> }
                </tr>
              </thead>
              <tbody>
                @for (a of attributes; track a.id) {
                  <tr>
                    <td>{{ a.id }}</td>
                    <td>{{ a.code }}</td>
                    <td>{{ a.name }}</td>
                    <td>{{ dataTypeLabel(a.dataType) }}</td>
                    <td>{{ a.isRequired ? 'да' : '—' }}</td>
                    <td>{{ a.defaultValue ?? '—' }}</td>
                    <td>{{ a.sortOrder }}</td>
                    @if (isAdmin) {
                      <td class="actions">
                        <button class="danger" (click)="deleteAttribute(a)">Удалить</button>
                      </td>
                    }
                  </tr>
                }
                @if (attributes.length === 0) {
                  <tr><td colspan="8" class="empty">Нет атрибутов</td></tr>
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
      .ac-page { padding: 24px; font-family: Arial, sans-serif; }
      .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; gap: 16px; flex-wrap: wrap; }
      h1 { margin: 0; }
      .subtitle { margin: 4px 0 0; color: #6b7280; font-size: 0.9rem; }
      .header-actions, .create-form, .edit-class-form, .create-attr-form { display: flex; gap: 8px; flex-wrap: wrap; align-items: center; }
      .create-form, .edit-class-form { margin-bottom: 16px; }
      input, select { padding: 8px 10px; border: 1px solid #d1d5db; border-radius: 8px; font-size: 0.95rem; }
      .chk { display: flex; align-items: center; gap: 4px; font-size: 0.9rem; color: #374151; }
      button { padding: 8px 14px; border: none; border-radius: 8px; background: #3f51b5; color: white; cursor: pointer; }
      button:disabled { opacity: 0.5; cursor: not-allowed; }
      button.danger { background: #dc2626; }
      button.secondary { background: #6b7280; }
      .layout { display: grid; grid-template-columns: 360px 1fr; gap: 16px; }
      .list, .detail { background: #fff; border-radius: 12px; padding: 16px; box-shadow: 0 2px 8px rgba(15,23,42,0.05); }
      .item { width: 100%; display: flex; justify-content: space-between; align-items: center; gap: 10px; margin-bottom: 6px; padding: 10px; border: 1px solid #d5deeb; border-radius: 8px; background: #fff; color: inherit; text-align: left; cursor: pointer; transition: background .16s ease, border-color .16s ease, box-shadow .16s ease; }
      .item:hover { border-color: #7da8ec; background: #f3f7ff; box-shadow: 0 2px 8px rgba(37,99,235,.12); }
      .item.active { border-color: #3475d1; background: #eef4ff; box-shadow: inset 3px 0 #3475d1; }
      .item:focus-visible { outline: 2px solid #3475d1; outline-offset: 2px; }
      .item-action { flex: 0 0 auto; padding: 4px 8px; border-radius: 12px; background: #eaf1ff; color: #245db0; font-size: .72rem; font-weight: 700; }
      .item small { display: block; color: #6b7280; font-size: 0.8rem; margin-top: 2px; }
      .badge { background: #e5e7eb; padding: 2px 8px; border-radius: 12px; font-size: 0.8rem; }
      .badge.inactive { background: #fee2e2; color: #991b1b; }
      .detail-header { display: flex; justify-content: space-between; align-items: start; margin-bottom: 16px; gap: 16px; flex-wrap: wrap; }
      .detail-header .actions { display: flex; gap: 8px; align-items: center; }
      .hint { color: #6b7280; font-size: 0.85rem; font-style: italic; }
      .section-title { margin: 16px 0 10px; font-size: 1rem; }
      table { width: 100%; border-collapse: collapse; }
      th, td { padding: 8px 10px; text-align: left; border-bottom: 1px solid #e5e7eb; }
      th { background: #f9fafb; font-weight: 600; }
      .actions { display: flex; gap: 6px; }
      .error { color: #b91c1c; padding: 10px; background: #fef2f2; border-radius: 8px; margin-bottom: 12px; }
      .success { color: #166534; padding: 10px; background: #f0fdf4; border-radius: 8px; margin-bottom: 12px; }
      .empty { color: #6b7280; }
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

    readonly dataTypes = ASSET_ATTRIBUTE_DATA_TYPES;

    // Форма класса
    newCode = '';
    newName = '';
    showCreateForm = false;

    // Форма атрибута
    newAttrCode = '';
    newAttrName = '';
    newAttrDataType = 'string';
    newAttrRequired = false;
    newAttrSort = 0;

    ngOnInit(): void {
        this.isAdmin = this.auth.isAdmin();
        this.loadAll();
    }

    private clearMessages(): void {
        this.errorMessage = '';
        this.successMessage = '';
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
                this.showCreateForm = false;
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
            sortOrder: this.newAttrSort,
        }).subscribe({
            next: () => {
                this.newAttrCode = '';
                this.newAttrName = '';
                this.newAttrRequired = false;
                this.newAttrSort = 0;
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