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
          <div class="create-form">
            <input [(ngModel)]="newCode" placeholder="code (a-z, _)" />
            <input [(ngModel)]="newName" placeholder="Название" />
            <button (click)="createDictionary()" [disabled]="!newCode || !newName">Создать</button>
          </div>
        }
      </header>

      @if (errorMessage) {
        <div class="error">{{ errorMessage }}</div>
      }

      <div class="layout">
        <aside class="list">
          <h3>Список</h3>
          @for (d of dictionaries; track d.id) {
            <button type="button" class="item" [class.active]="selected?.id === d.id"
              [attr.aria-pressed]="selected?.id === d.id" (click)="select(d)">
              <div>
                <strong>{{ d.name }}</strong>
                <small>{{ d.code }}</small>
              </div>
              <span class="badge">{{ d.valuesCount }}</span>
              <span class="item-action">{{ selected?.id === d.id ? 'Выбран' : 'Открыть' }}</span>
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
                <small>{{ selected.code }} · id={{ selected.id }} · {{ selected.isSystem ? 'системный' : 'пользовательский' }}</small>
              </div>
              @if (isAdmin && !selected.isSystem) {
                <button class="danger" (click)="deleteDictionary()">Удалить</button>
              }
            </div>

            @if (isAdmin) {
              <div class="create-value-form">
                <input [(ngModel)]="newValueCode" placeholder="code" />
                <input [(ngModel)]="newValueName" placeholder="Название" />
                <input type="number" [(ngModel)]="newValueSort" placeholder="Sort" />
                <button (click)="createValue()" [disabled]="!newValueCode || !newValueName">Добавить</button>
              </div>
            }

            <table>
              <thead>
                <tr>
                  <th>Id</th>
                  <th>Code</th>
                  <th>Name</th>
                  <th>Sort</th>
                  <th>Archived</th>
                  @if (isAdmin) { <th></th> }
                </tr>
              </thead>
              <tbody>
                @for (v of values; track v.id) {
                  <tr>
                    <td>{{ v.id }}</td>
                    <td>{{ v.code }}</td>
                    <td>{{ v.name }}</td>
                    <td>{{ v.sortOrder }}</td>
                    <td>{{ v.isArchived ? 'да' : 'нет' }}</td>
                    @if (isAdmin) {
                      <td class="actions">
                        @if (!v.isArchived) {
                          <button (click)="archiveValue(v)">Архивировать</button>
                        }
                        @if (v.isArchived) {
                          <button class="danger" (click)="deleteValue(v)">Удалить</button>
                        }
                      </td>
                    }
                  </tr>
                }
              </tbody>
            </table>
          } @else {
            <p class="empty">Выберите справочник слева</p>
          }
        </main>
      </div>
    </section>
  `,
    styles: [
        `
      .dict-page { padding: 24px; font-family: Arial, sans-serif; }
      .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; gap: 16px; }
      h1 { margin: 0; }
      .create-form { display: flex; gap: 8px; }
      input { padding: 8px 10px; border: 1px solid #d1d5db; border-radius: 8px; font-size: 0.95rem; }
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
      .create-value-form { display: flex; gap: 8px; margin-bottom: 16px; }
      table { width: 100%; border-collapse: collapse; }
      th, td { padding: 8px 10px; text-align: left; border-bottom: 1px solid #e5e7eb; }
      th { background: #f9fafb; font-weight: 600; }
      .actions { display: flex; gap: 6px; }
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

    ngOnInit(): void {
        this.isAdmin = this.auth.isAdmin();
        this.loadAll();
    }

    private loadAll(): void {
        this.api.getAll().subscribe({
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
        this.api.getValues(d.id, true).subscribe({
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
        this.api.delete(this.selected.id).subscribe({
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
        this.api.createValue(this.selected.id, {
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

    archiveValue(v: DictionaryValueDto): void {
        if (!this.selected) return;
        this.api.archiveValue(this.selected.id, v.id).subscribe({
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
        this.api.deleteValue(this.selected.id, v.id).subscribe({
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
}