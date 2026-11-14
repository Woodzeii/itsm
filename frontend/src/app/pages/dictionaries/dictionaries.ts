import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
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
          <button type="button" class="primary-button" (click)="openCreateDialog()">+ Создать справочник</button>
        }
      </header>

      @if (errorMessage && !showCreateDialog) {
        <div class="error">{{ errorMessage }}</div>
      }

      @if (showCreateDialog) {
        <div class="modal-backdrop" role="presentation" (click)="onCreateBackdropClick($event)" (keydown.escape)="closeCreateDialog()">
          <section class="create-dialog" role="dialog" aria-modal="true" aria-labelledby="create-dialog-title">
            <header class="dialog-header">
              <h2 id="create-dialog-title">Создать новый справочник</h2>
              <button type="button" class="icon-close" aria-label="Закрыть" (click)="closeCreateDialog()">×</button>
            </header>
            <p class="hint">Именованный список вариантов для выбора в системе (например, Склады или Места). Код используется для идентификации в API.</p>
            <p class="hint">Создание справочника не добавляет поле в форму актива автоматически — для этого требуется настройка модуля.</p>
            @if (errorMessage) { <div class="error">{{ errorMessage }}</div> }
            <div class="create-form">
              <label>Код
                <input autofocus [(ngModel)]="newCode" placeholder="Например, warehouses" />
              </label>
              <label>Название
                <input [(ngModel)]="newName" placeholder="Название справочника" />
              </label>
            </div>
            <footer class="dialog-actions">
              <button type="button" class="secondary" (click)="closeCreateDialog()">Отмена</button>
              <button type="button" (click)="createDictionary()" [disabled]="!newCode.trim() || !newName.trim()">Создать</button>
            </footer>
          </section>
        </div>
      }

      <div class="layout">
        <aside class="list">
          <div class="list-heading">
            <h2>Список справочников</h2>
            <input class="dictionary-search" type="search" aria-label="Поиск по справочникам"
              [(ngModel)]="dictionarySearch" placeholder="Поиск по названию или коду" />
          </div>
          @for (d of filteredDictionaries; track d.code) {
            <button type="button" class="item" [class.active]="selected?.code === d.code"
              [attr.aria-pressed]="selected?.code === d.code" (click)="select(d)">
              <div>
                <strong>{{ d.name }}</strong>
                <small>{{ d.code }}{{ d.isArchived ? ' · в архиве' : '' }}</small>
              </div>
              <span class="badge">{{ d.valuesCount }}</span>
            </button>
          }
          @if (dictionaries.length === 0) {
            <p class="empty">Нет справочников</p>
          } @else if (filteredDictionaries.length === 0) {
            <p class="empty">По вашему запросу ничего не найдено.</p>
          }
        </aside>

        <main class="detail">
          @if (selected) {
            <div class="detail-header">
              <div>
                <h2>{{ selected.name }}</h2>
                <small>{{ selected.code }} · {{ selected.isArchived ? 'в архиве' : 'активный' }} · {{ selected.isSystem ? 'системный' : 'пользовательский' }}</small>
              </div>
              @if (isAdmin) {
                @if (selected.isArchived) {
                  <button (click)="restoreDictionary()">Восстановить справочник</button>
                } @else if (!selected.isSystem) {
                  <button class="archive-outline" (click)="archiveDictionary()">Архивировать справочник</button>
                }
                @if (!selected.isSystem && values.length === 0) {
                  <button class="danger" (click)="deleteDictionary()">Удалить насовсем</button>
                }
              }
            </div>

            @if (isAdmin && !selected.isArchived) {
              <section class="form-panel">
                <h3>Добавить запись в справочник {{ selected.name }}</h3>
                <p class="hint">Код — неизменяемый ключ, название видят пользователи.</p>
                <div class="create-value-form">
                  <label title="Код является неизменяемым ключом записи.">Код
                    <input [(ngModel)]="newValueCode" placeholder="Например, main" />
                  </label>
                  <label>Название
                    <input [(ngModel)]="newValueName" placeholder="Название записи" />
                  </label>
                  <button (click)="createValue()" [disabled]="!newValueCode.trim() || !newValueName.trim()">Добавить</button>
                </div>
                <p class="hint">Эта запись станет доступна для выбора в карточках активов.</p>
              </section>
            }

            <table>
              <thead>
                <tr>
                  @if (isAdmin) { <th class="reorder-column" aria-label="Изменить порядок"></th> }
                  <th>Код</th>
                  <th>Название</th>
                  <th>Статус</th>
                  @if (isAdmin) { <th>Действия</th> }
                </tr>
              </thead>
              <tbody>
                @for (v of orderedValues; track v.code; let i = $index) {
                  <tr>
                    @if (isAdmin) {
                      <td class="reorder-actions">
                        <button type="button" class="reorder-button" title="Переместить выше" aria-label="Переместить выше"
                          [disabled]="i === 0 || isReordering" (click)="moveValue(v, -1)">↑</button>
                        <button type="button" class="reorder-button" title="Переместить ниже" aria-label="Переместить ниже"
                          [disabled]="i === orderedValues.length - 1 || isReordering" (click)="moveValue(v, 1)">↓</button>
                      </td>
                    }
                    <td>{{ v.code }}</td>
                    @if (editingValueCode === v.code) {
                      <td><input aria-label="Название записи" [(ngModel)]="editValueName" /></td>
                    } @else {
                      <td>{{ v.name }}</td>
                    }
                    <td>
                      <span class="status-pill" [class.archived]="v.isArchived">
                        <span class="status-dot" aria-hidden="true"></span>
                        {{ v.isArchived ? 'В архиве' : 'Активна' }}
                      </span>
                    </td>
                    @if (isAdmin) {
                      <td class="actions">
                        @if (editingValueCode === v.code) {
                          <button class="text-action" (click)="updateValue(v)" [disabled]="!editValueName.trim()">Сохранить</button>
                          <button class="text-action muted-action" (click)="cancelEditValue()">Отмена</button>
                        } @else {
                          <button class="text-action" title="Изменить запись" (click)="startEditValue(v)">Изменить</button>
                          @if (!v.isArchived) {
                            <button class="text-action archive-action" title="Архивировать запись" (click)="archiveValue(v)">Архивировать</button>
                          } @else {
                            <button class="text-action" (click)="restoreValue(v)">Восстановить</button>
                            <button class="text-action danger-action" (click)="deleteValue(v)">Удалить</button>
                          }
                        }
                      </td>
                    }
                  </tr>
                } @empty {
                  <tr><td class="empty table-empty" [attr.colspan]="isAdmin ? 5 : 3">В этом справочнике пока нет записей.</td></tr>
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
      .dict-page { padding: 24px; font-family: Arial, sans-serif; color: #202734; }
      .page-header, .list-heading, .detail-header, .dialog-header, .dialog-actions { display: flex; justify-content: space-between; align-items: center; gap: 12px; }
      .page-header { margin-bottom: 16px; }
      h1, h2, h3 { margin: 0; }
      h1 { font-size: 24px; }
      h2 { font-size: 17px; }
      h3 { margin-bottom: 8px; font-size: 15px; }
      .layout { display: grid; grid-template-columns: 320px minmax(0, 1fr); gap: 16px; }
      .list, .detail { min-width: 0; padding: 16px; border: 1px solid #e7eaf0; border-radius: 10px; background: #fff; }
      .list-heading { align-items: flex-start; flex-direction: column; margin-bottom: 12px; }
      .dictionary-search { width: 100%; }
      input { min-width: 0; padding: 8px 10px; border: 1px solid #d1d5db; border-radius: 7px; color: #202734; font: inherit; font-size: 13px; }
      input:focus { border-color: #6b91d6; outline: 2px solid #e9f0ff; }
      label { display: grid; flex: 1; gap: 5px; color: #4b5563; font-size: 12px; }
      button { padding: 8px 13px; border: 1px solid #315fac; border-radius: 7px; background: #3f6fbd; color: #fff; font: inherit; font-size: 13px; cursor: pointer; }
      button:hover:not(:disabled) { background: #315fac; }
      button:disabled { border-color: #e2e5e9; background: #e8eaed; color: #a0a5ad; cursor: not-allowed; }
      .primary-button { white-space: nowrap; }
      .secondary { border-color: #d7dce4; background: #fff; color: #4b5563; }
      .secondary:hover:not(:disabled) { background: #f4f6f8; }
      .danger { border-color: #f0caca; background: #fff; color: #b42323; }
      .danger:hover:not(:disabled) { background: #fff5f5; }
      .archive-outline { border: 1px solid #e7bcbc; background: #fff; color: #a93c3c; }
      .archive-outline:hover:not(:disabled) { border-color: #c97b7b; background: #fff7f7; color: #8e2929; }
      .item { width: 100%; display: flex; justify-content: space-between; align-items: center; gap: 10px; margin-bottom: 6px; padding: 10px; border: 1px solid #e0e5ed; border-radius: 8px; background: #fff; color: inherit; text-align: left; cursor: pointer; transition: background .16s ease, border-color .16s ease; }
      .item:hover { border-color: #9ab5e3; background: #f7f9fd; }
      .item:hover, .item:hover strong { color: #233b5b; }
      .item.active { border-color: #7195d0; background: #f1f5fc; box-shadow: inset 3px 0 #5681c8; }
      .item:focus-visible { outline: 2px solid #5681c8; outline-offset: 2px; }
      .item small { display: block; margin-top: 3px; color: #6b7280; font-size: 12px; }
      .badge { padding: 2px 8px; border-radius: 12px; background: #f0f2f5; color: #606b79; font-size: 12px; }
      .detail-header { align-items: flex-start; margin-bottom: 16px; }
      .detail-header small { display: block; margin-top: 5px; color: #707987; }
      .form-panel { margin-bottom: 16px; padding: 14px; border: 1px solid #e7eaf0; border-radius: 9px; background: #fafbfc; }
      .create-form, .create-value-form { display: flex; align-items: end; gap: 8px; }
      .hint { margin: 7px 0 10px; color: #6b7280; font-size: 12px; line-height: 1.45; }
      table { width: 100%; border-collapse: collapse; }
      th, td { padding: 9px 10px; border-bottom: 1px solid #edf0f3; text-align: left; font-size: 13px; }
      th { background: #f8f9fb; color: #596372; font-size: 12px; font-weight: 600; }
      .actions, .reorder-actions { white-space: nowrap; }
      .reorder-actions { width: 1%; padding-right: 0; }
      .reorder-button { width: 26px; height: 26px; padding: 0; border-color: transparent; background: transparent; color: #657185; font-size: 16px; }
      .reorder-button:hover:not(:disabled) { border-color: #dfe5ee; background: #f2f5f9; color: #315fac; }
      .reorder-button:disabled { border-color: transparent; background: transparent; color: #c9cdd3; }
      .actions { display: flex; align-items: center; gap: 10px; }
      .text-action { padding: 4px 2px; border: 0; background: transparent; color: #536982; font-size: 12px; }
      .text-action:hover:not(:disabled) { background: transparent; color: #244e86; text-decoration: underline; }
      .archive-action { color: #687485; }
      .danger-action { color: #a93c3c; }
      .status-pill { display: inline-flex; align-items: center; gap: 6px; padding: 4px 8px; border-radius: 12px; background: #edf7f0; color: #287044; font-size: 11px; white-space: nowrap; }
      .status-pill.archived { background: #f1f2f4; color: #68717e; }
      .status-dot { width: 6px; height: 6px; border-radius: 50%; background: #35a05b; }
      .status-pill.archived .status-dot { background: #9aa1aa; }
      .error { margin-bottom: 12px; padding: 10px; border-radius: 7px; background: #fef2f2; color: #b91c1c; font-size: 13px; }
      .empty { color: #6b7280; font-size: 13px; }
      .table-empty { padding: 24px; text-align: center; }
      .modal-backdrop { position: fixed; z-index: 1000; inset: 0; display: grid; place-items: center; padding: 20px; background: rgb(17 24 39 / 42%); }
      .create-dialog { width: min(100%, 520px); padding: 22px; border: 1px solid #e5e7eb; border-radius: 12px; background: #fff; box-shadow: 0 18px 48px rgb(15 23 42 / 20%); }
      .dialog-header { margin-bottom: 12px; }
      .icon-close { width: 32px; height: 32px; padding: 0; border: 0; background: transparent; color: #737d8b; font-size: 24px; }
      .icon-close:hover:not(:disabled) { background: #f2f4f7; color: #313a47; }
      .dialog-actions { justify-content: flex-end; margin-top: 18px; }
      @media (max-width: 900px) { .layout { grid-template-columns: 1fr; } }
      @media (max-width: 620px) {
        .dict-page { padding: 16px; }
        .page-header { align-items: flex-start; flex-direction: column; }
        .create-form, .create-value-form { align-items: stretch; flex-direction: column; }
        .create-form button, .create-value-form button { align-self: flex-start; }
        .detail { overflow-x: auto; }
        table { min-width: 580px; }
      }
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
    showCreateDialog = false;
    dictionarySearch = '';
    isReordering = false;

    // Формы
    newCode = '';
    newName = '';
    newValueCode = '';
    newValueName = '';
    editingValueCode: string | null = null;
    editValueName = '';

    get filteredDictionaries(): DictionaryDto[] {
        const search = this.dictionarySearch.trim().toLocaleLowerCase();
        return search
            ? this.dictionaries.filter(dictionary =>
                `${dictionary.name} ${dictionary.code}`.toLocaleLowerCase().includes(search))
            : this.dictionaries;
    }

    get orderedValues(): DictionaryValueDto[] {
        return [...this.values].sort((a, b) => a.sortOrder - b.sortOrder || a.code.localeCompare(b.code));
    }

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

    openCreateDialog(): void {
        this.errorMessage = '';
        this.showCreateDialog = true;
    }

    closeCreateDialog(): void {
        this.showCreateDialog = false;
        this.errorMessage = '';
        this.newCode = '';
        this.newName = '';
    }

    onCreateBackdropClick(event: MouseEvent): void {
        if (event.target === event.currentTarget) this.closeCreateDialog();
    }

    createDictionary(): void {
        this.errorMessage = '';
        this.api.create({ code: this.newCode.trim(), name: this.newName.trim() }).subscribe({
            next: () => {
                this.closeCreateDialog();
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
            sortOrder: this.orderedValues.length
                ? Math.max(...this.orderedValues.map(value => value.sortOrder)) + 1
                : 0,
        }).subscribe({
            next: () => {
                this.newValueCode = '';
                this.newValueName = '';
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
    }

    cancelEditValue(): void {
        this.editingValueCode = null;
        this.editValueName = '';
    }

    updateValue(value: DictionaryValueDto): void {
        if (!this.selected) return;
        this.errorMessage = '';
        this.api.updateValue(this.selected.code, value.code, {
            name: this.editValueName.trim(),
            sortOrder: value.sortOrder,
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

    moveValue(value: DictionaryValueDto, direction: -1 | 1): void {
        if (!this.selected || this.isReordering) return;
        const dictionaryCode = this.selected.code;
        const ordered = this.orderedValues;
        const currentIndex = ordered.findIndex(item => item.code === value.code);
        const nextIndex = currentIndex + direction;
        if (currentIndex < 0 || nextIndex < 0 || nextIndex >= ordered.length) return;

        [ordered[currentIndex], ordered[nextIndex]] = [ordered[nextIndex], ordered[currentIndex]];
        this.isReordering = true;
        forkJoin(ordered.map((item, index) => this.api.updateValue(dictionaryCode, item.code, {
            name: item.name,
            sortOrder: index,
        }))).subscribe({
            next: updated => {
                if (this.selected?.code === dictionaryCode) this.values = updated;
                this.isReordering = false;
                this.cdr.detectChanges();
            },
            error: err => {
                this.isReordering = false;
                this.errorMessage = `Не удалось изменить порядок записей: ${err.status} ${err.error?.message ?? ''}`;
                if (this.selected?.code === dictionaryCode) this.select(this.selected);
                else this.loadValues(dictionaryCode);
                this.cdr.detectChanges();
            },
        });
    }

    private loadValues(dictionaryCode: string): void {
        this.api.getValues(dictionaryCode, true).subscribe({
            next: values => {
                if (this.selected?.code === dictionaryCode) this.values = values;
                this.cdr.detectChanges();
            },
            error: err => {
                this.errorMessage = `Не удалось обновить список записей: ${err.status}`;
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