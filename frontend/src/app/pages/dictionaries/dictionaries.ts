import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/auth/auth.service';
import { DictionaryApiService } from '../../core/dictionaries/dictionary.service';
import { DictionaryDto, DictionaryValueDto } from '../../core/dictionaries/dictionary.models';

@Component({
    selector: 'app-dictionaries',
    standalone: true,
    imports: [CommonModule, FormsModule],
    template: `
    <section class="dict-page">
      <header class="page-header">
        <h1>Справочники</h1>
        @if (isAdmin) {
          <section class="form-panel">
            <h2 title="Именованный список вариантов для выбора в системе (например, Склады или Места). Код используется для идентификации в API.">Создать новый справочник</h2>
            <p class="hint">Создание справочника не добавляет поле в форму актива автоматически — для этого требуется настройка модуля.</p>
            <div class="create-form">
              <label>Код
                <input [(ngModel)]="newCode" placeholder="Например, warehouses" />
              </label>
              <label>Название
                <input [(ngModel)]="newName" placeholder="Название справочника" />
              </label>
              <button (click)="createDictionary()" [disabled]="!newCode.trim() || !newName.trim()">Создать</button>
            </div>
          </section>
        }
      </header>

      @if (errorMessage) {
        <div class="error">{{ errorMessage }}</div>
      }

      <div class="layout">
        <aside class="list">
          <h2>Список справочников</h2>
          @for (d of dictionaries; track d.code) {
            <button type="button" class="item" [class.active]="selected?.code === d.code"
              [attr.aria-pressed]="selected?.code === d.code" (click)="select(d)">
              <div>
                <strong>{{ d.name }}</strong>
                <small>{{ d.code }}{{ d.isArchived ? ' · в архиве' : '' }}</small>
              </div>
              <span class="badge">{{ d.valuesCount }}</span>
              <span class="item-action">{{ selected?.code === d.code ? 'Выбран' : 'Открыть' }}</span>
            </button>
          }
          @if (dictionaries.length === 0) {
            <p class="empty">Нет справочников</p>
          }
        </aside>

        <main class="detail">
          @if (selected) {
            <div class="detail-header">
              <div>
                <h2>{{ selected.name }}</h2>
                <small>{{ selected.code }} · {{ selected.isArchived ? 'в архиве' : 'активный' }} · {{ selected.isSystem ? 'системный' : 'пользовательский' }}</small>
                <p class="hint" title="Конкретный вариант выбора внутри справочника (например, Основной склад). Код является неизменяемым ключом, а название видят пользователи.">
                  Запись справочника: код — неизменяемый ключ, название видно пользователям.
                </p>
              </div>
              @if (isAdmin) {
                @if (selected.isArchived) {
                  <button (click)="restoreDictionary()">Восстановить справочник</button>
                } @else if (!selected.isSystem) {
                  <button class="danger" (click)="archiveDictionary()">Архивировать справочник</button>
                }
                @if (!selected.isSystem && values.length === 0) {
                  <button class="danger" (click)="deleteDictionary()">Удалить насовсем</button>
                }
              }
            </div>

            @if (isAdmin && !selected.isArchived) {
              <section class="form-panel">
                <h3>Добавить запись в справочник {{ selected.name }}</h3>
                <p class="hint" title="Конкретный вариант выбора внутри справочника (например, Основной склад). Код является неизменяемым ключом, а название видят пользователи.">
                  Запись справочника: код неизменяем, название видят пользователи.
                </p>
                <div class="create-value-form">
                  <label>Код
                    <input [(ngModel)]="newValueCode" placeholder="Например, main" />
                  </label>
                  <label>Название
                    <input [(ngModel)]="newValueName" placeholder="Название записи" />
                  </label>
                  <label>Порядок
                    <input type="number" [(ngModel)]="newValueSort" placeholder="Порядок" />
                  </label>
                  <button (click)="createValue()" [disabled]="!newValueCode.trim() || !newValueName.trim()">Добавить</button>
                </div>
                <p class="hint">Эта запись станет доступна для выбора в карточках активов.</p>
              </section>
            }

            <table>
              <thead>
                <tr>
                  <th>Код</th>
                  <th>Название</th>
                  <th>Порядок</th>
                  <th>Статус</th>
                  @if (isAdmin) { <th>Действия</th> }
                </tr>
              </thead>
              <tbody>
                @for (v of values; track v.code) {
                  <tr>
                    <td>{{ v.code }}</td>
                    @if (editingValueCode === v.code) {
                      <td><input aria-label="Название записи" [(ngModel)]="editValueName" /></td>
                      <td><input aria-label="Порядок записи" type="number" [(ngModel)]="editValueSort" /></td>
                    } @else {
                      <td>{{ v.name }}</td>
                      <td>{{ v.sortOrder }}</td>
                    }
                    <td>{{ v.isArchived ? 'В архиве' : 'Активна' }}</td>
                    @if (isAdmin) {
                      <td class="actions">
                        @if (editingValueCode === v.code) {
                          <button (click)="updateValue(v)" [disabled]="!editValueName.trim()">Сохранить</button>
                          <button class="secondary" (click)="cancelEditValue()">Отмена</button>
                        } @else {
                          <button class="secondary" (click)="startEditValue(v)">Изменить</button>
                          @if (!v.isArchived) {
                            <button (click)="archiveValue(v)">Архивировать</button>
                          } @else {
                            <button (click)="restoreValue(v)">Восстановить</button>
                            <button class="danger" (click)="deleteValue(v)">Удалить</button>
                          }
                        }
                      </td>
                    }
                  </tr>
                }
              </tbody>
            </table>
          } @else {
            <p class="empty">Выберите справочник в списке слева.</p>
          }
        </main>
      </div>
    </section>
  `,
    styles: [
        `
      .dict-page { padding: 24px; font-family: Arial, sans-serif; }
      .page-header { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 16px; gap: 16px; }
      h1 { margin: 0; } h2, h3 { margin: 0 0 10px; }
      .form-panel { padding: 14px; border: 1px solid #e5e7eb; border-radius: 10px; background: #f9fafb; }
      .create-form, .create-value-form { display: flex; align-items: end; gap: 8px; }
      label { display: grid; gap: 5px; color: #4b5563; font-size: .85rem; }
      input { min-width: 0; padding: 8px 10px; border: 1px solid #d1d5db; border-radius: 8px; font-size: 0.95rem; }
      .hint { margin: 0 0 10px; color: #6b7280; font-size: .82rem; }
      button { padding: 8px 14px; border: none; border-radius: 8px; background: #3f51b5; color: white; cursor: pointer; }
      button:disabled { opacity: 0.5; cursor: not-allowed; }
      button.danger { background: #dc2626; }
      .layout { display: grid; grid-template-columns: 320px 1fr; gap: 16px; }
      .list, .detail { background: #fff; border-radius: 12px; padding: 16px; box-shadow: 0 2px 8px rgba(15,23,42,0.05); }
      .item { width: 100%; display: flex; justify-content: space-between; align-items: center; gap: 10px; margin-bottom: 6px; padding: 10px; border: 1px solid #d5deeb; border-radius: 8px; background: #fff; color: inherit; text-align: left; cursor: pointer; transition: background .16s ease, border-color .16s ease, box-shadow .16s ease; }
      .item:hover { border-color: #7da8ec; background: #f3f7ff; box-shadow: 0 2px 8px rgba(37,99,235,.12); }
      .item.active { border-color: #3475d1; background: #eef4ff; box-shadow: inset 3px 0 #3475d1; }
      .item:focus-visible { outline: 2px solid #3475d1; outline-offset: 2px; }
      .item-action { flex: 0 0 auto; padding: 4px 8px; border-radius: 12px; background: #eaf1ff; color: #245db0; font-size: .72rem; font-weight: 700; }
      .item small { display: block; color: #6b7280; font-size: 0.8rem; }
      .badge { background: #e5e7eb; padding: 2px 8px; border-radius: 12px; font-size: 0.8rem; }
      .detail-header { display: flex; justify-content: space-between; align-items: start; margin-bottom: 16px; }
      .create-value-form { margin-bottom: 8px; }
      table { width: 100%; border-collapse: collapse; }
      th, td { padding: 8px 10px; text-align: left; border-bottom: 1px solid #e5e7eb; }
      th { background: #f9fafb; font-weight: 600; }
      .actions { display: flex; gap: 6px; }
      button.secondary { background: #64748b; }
      .error { color: #b91c1c; padding: 10px; background: #fef2f2; border-radius: 8px; margin-bottom: 12px; }
      .empty { color: #6b7280; }
    `,
    ],
})
export class DictionariesComponent implements OnInit {
    private readonly api = inject(DictionaryApiService);
    private readonly auth = inject(AuthService);
    private readonly cdr = inject(ChangeDetectorRef);

    dictionaries: DictionaryDto[] = [];
    selected: DictionaryDto | null = null;
    values: DictionaryValueDto[] = [];

    isAdmin = false;
    errorMessage = '';

    // Формы
    newCode = '';
    newName = '';
    newValueCode = '';
    newValueName = '';
    newValueSort = 0;
    editingValueCode: string | null = null;
    editValueName = '';
    editValueSort = 0;

    ngOnInit(): void {
        this.isAdmin = this.auth.isAdmin();
        this.loadAll();
    }

    private loadAll(): void {
        this.api.getAll(true).subscribe({
            next: list => {
                this.dictionaries = list;
                this.cdr.detectChanges();
            },
            error: err => {
                this.errorMessage = `Не удалось загрузить: ${err.status}`;
                this.cdr.detectChanges();
            },
        });
    }

    select(d: DictionaryDto): void {
        this.selected = d;
        this.api.getValues(d.code, true).subscribe({
            next: list => {
                this.values = list;
                this.cdr.detectChanges();
            },
            error: err => {
                this.errorMessage = `Ошибка: ${err.status}`;
                this.cdr.detectChanges();
            },
        });
    }

    createDictionary(): void {
        this.errorMessage = '';
        this.api.create({ code: this.newCode.trim(), name: this.newName.trim() }).subscribe({
            next: () => {
                this.newCode = '';
                this.newName = '';
                this.loadAll();
            },
            error: err => {
                this.errorMessage = `Ошибка: ${err.status} ${err.error?.message ?? ''}`;
                this.cdr.detectChanges();
            },
        });
    }

    deleteDictionary(): void {
        if (!this.selected || !confirm(`Удалить «${this.selected.name}»?`)) return;
        this.api.delete(this.selected.code).subscribe({
            next: () => {
                this.selected = null;
                this.values = [];
                this.loadAll();
            },
            error: err => {
                this.errorMessage = `Ошибка: ${err.status} ${err.error?.message ?? ''}`;
                this.cdr.detectChanges();
            },
        });
    }

    createValue(): void {
        if (!this.selected) return;
        this.errorMessage = '';
        this.api.createValue(this.selected.code, {
            code: this.newValueCode.trim(),
            name: this.newValueName.trim(),
            sortOrder: this.newValueSort,
        }).subscribe({
            next: () => {
                this.newValueCode = '';
                this.newValueName = '';
                this.newValueSort = 0;
                this.loadAll();
                this.select(this.selected!);
            },
            error: err => {
                this.errorMessage = `Ошибка: ${err.status} ${err.error?.message ?? ''}`;
                this.cdr.detectChanges();
            },
        });
    }

    startEditValue(value: DictionaryValueDto): void {
        this.editingValueCode = value.code;
        this.editValueName = value.name;
        this.editValueSort = value.sortOrder;
    }

    cancelEditValue(): void {
        this.editingValueCode = null;
        this.editValueName = '';
        this.editValueSort = 0;
    }

    updateValue(value: DictionaryValueDto): void {
        if (!this.selected) return;
        this.errorMessage = '';
        this.api.updateValue(this.selected.code, value.code, {
            name: this.editValueName.trim(),
            sortOrder: this.editValueSort,
        }).subscribe({
            next: updated => {
                this.values = this.values.map(item => item.code === updated.code ? updated : item);
                this.cancelEditValue();
                this.cdr.detectChanges();
            },
            error: err => {
                this.errorMessage = `Не удалось сохранить запись: ${err.status} ${err.error?.message ?? ''}`;
                this.cdr.detectChanges();
            },
        });
    }

    archiveValue(v: DictionaryValueDto): void {
        if (!this.selected) return;
        this.api.archiveValue(this.selected.code, v.code).subscribe({
            next: () => {
                this.loadAll();
                this.select(this.selected!);
            },
            error: err => {
                this.errorMessage = `Ошибка: ${err.status}`;
                this.cdr.detectChanges();
            },
        });
    }

    deleteValue(v: DictionaryValueDto): void {
        if (!this.selected || !confirm(`Удалить «${v.name}»?`)) return;
        this.api.deleteValue(this.selected.code, v.code).subscribe({
            next: () => {
                this.loadAll();
                this.select(this.selected!);
            },
            error: err => {
                this.errorMessage = `Ошибка: ${err.status} ${err.error?.message ?? ''}`;
                this.cdr.detectChanges();
            },
        });
    }

    restoreValue(v: DictionaryValueDto): void {
        if (!this.selected) return;
        this.api.restoreValue(this.selected.code, v.code).subscribe({
            next: () => {
                this.loadAll();
                this.select(this.selected!);
            },
            error: err => {
                this.errorMessage = `Ошибка: ${err.status} ${err.error?.message ?? ''}`;
                this.cdr.detectChanges();
            },
        });
    }

    archiveDictionary(): void {
        if (!this.selected || !confirm(`Архивировать «${this.selected.name}»?`)) return;
        this.api.archiveDictionary(this.selected.code).subscribe({
            next: dictionary => {
                this.selected = dictionary;
                this.loadAll();
            },
            error: err => {
                this.errorMessage = `Ошибка: ${err.status} ${err.error?.message ?? ''}`;
                this.cdr.detectChanges();
            },
        });
    }

    restoreDictionary(): void {
        if (!this.selected) return;
        this.api.restoreDictionary(this.selected.code).subscribe({
            next: dictionary => {
                this.selected = dictionary;
                this.loadAll();
            },
            error: err => {
                this.errorMessage = `Ошибка: ${err.status} ${err.error?.message ?? ''}`;
                this.cdr.detectChanges();
            },
        });
    }
}