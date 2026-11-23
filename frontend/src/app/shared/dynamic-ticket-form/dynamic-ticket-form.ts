import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { FormControl, FormRecord, ReactiveFormsModule, ValidatorFn, Validators } from '@angular/forms';
import { Subscription } from 'rxjs';
import { FormFieldDefinition, TicketFormDefinition } from '../../core/workspace/workspace.models';

export interface DynamicTicketFormSubmission {
    title: string;
    description: string;
    priority: string;
    fields: Record<string, string | number | boolean | null>;
    files: File[];
    assetId?: string;
}

@Component({
    selector: 'app-dynamic-ticket-form',
    standalone: true,
    imports: [CommonModule, ReactiveFormsModule],
    template: `
      @if (definition) {
        <div class="dynamic-fields" [formGroup]="form">
          @for (field of displayedFields; track field.id) {
            @if (isFieldVisible(field)) {
              <label [class.wide]="field.type === 'textarea'">
                <span>{{ field.name }} @if (field.required) { <b class="required">*</b> }</span>
                @if (field.hint) { <small>{{ field.hint }}</small> }
                @if (field.type === 'textarea') {
                  <textarea [formControl]="controlFor(field)" [attr.aria-label]="field.name"></textarea>
                } @else if (field.type === 'boolean') {
                  <span class="boolean-control"><input type="checkbox" [formControl]="controlFor(field)" /> {{ field.name }}</span>
                } @else if (isSelectField(field)) {
                  <select [formControl]="controlFor(field)" [attr.aria-label]="field.name">
                    <option value="">Выберите значение</option>
                    @for (option of optionsFor(field); track option) { <option [value]="option">{{ option }}</option> }
                  </select>
                } @else if (field.type === 'file') {
                  <input type="file" [attr.aria-label]="field.name" (change)="setFileName(field, $event)" />
                  @if (controlFor(field).value) { <small>{{ controlFor(field).value }}</small> }
                } @else {
                  <input [type]="inputType(field)" [formControl]="controlFor(field)" [attr.aria-label]="field.name" />
                }
                @if (controlFor(field).invalid && (controlFor(field).touched || submitted)) {
                  <small class="field-error">{{ field.required && isEmpty(controlFor(field).value) ? 'Заполните поле.' : 'Проверьте значение поля.' }}</small>
                }
              </label>
            }
          }
        </div>
      }
    `,
    styles: [`
      :host { display: contents; }
      .dynamic-fields { display: contents; }
      label { display: grid; align-content: start; gap: 7px; color: #566171; font-size: 10px; font-weight: 600; }
      label.wide { grid-column: 1 / -1; }
      label > span:first-child { color: #566171; }
      label small { color: #8792a0; font-size: 9px; font-weight: 400; line-height: 1.4; }
      input:not([type=checkbox]):not([type=file]), select, textarea { width: 100%; min-height: 38px; box-sizing: border-box; padding: 8px 10px; border: 1px solid #dce2e9; border-radius: 5px; background: #fff; color: #394352; font-size: 11px; font-weight: 400; }
      textarea { min-height: 100px; resize: vertical; }
      .boolean-control { display: flex; align-items: center; gap: 7px; min-height: 38px; font-weight: 400; }
      .required { color: #c84c4c; }
      .field-error { color: #a93232; }
    `],
})
export class DynamicTicketFormComponent implements OnChanges {
    @Input() definition: TicketFormDefinition | null = null;
    @Input() criticalities: string[] = [];
    @Input() defaultPriority = 'Средний';
    @Input() includeEngineerFields = false;
    @Output() validityChange = new EventEmitter<boolean>();

    readonly form = new FormRecord<FormControl<unknown>>({});
    submitted = false;
    private formChanges?: Subscription;
    private readonly selectedFiles = new Map<string, File>();

    get displayedFields(): FormFieldDefinition[] {
        return (this.definition?.fields ?? []).filter(field => this.includeEngineerFields || field.visibleToRequester);
    }

    get isValid(): boolean { return this.form.valid; }

    ngOnChanges(changes: SimpleChanges): void {
        if (changes['definition'] || changes['includeEngineerFields']) {
            this.initializeControls();
        }
    }

    controlFor(field: FormFieldDefinition): FormControl<unknown> {
        return this.form.controls[field.id]!;
    }

    isSelectField(field: FormFieldDefinition): boolean {
        return field.type === 'dictionary' && this.optionsFor(field).length > 0;
    }

    optionsFor(field: FormFieldDefinition): string[] {
        if (field.options?.length) return field.options;
        return this.isCriticality(field) ? this.criticalities : [];
    }

    inputType(field: FormFieldDefinition): string {
        if (field.type === 'date' || field.type === 'number') return field.type;
        if (field.type === 'asset') return 'number';
        return 'text';
    }

    isFieldVisible(field: FormFieldDefinition): boolean {
        if (!field.conditionFieldId) return true;
        const source = this.form.controls[field.conditionFieldId]?.value;
        if (source === null || source === undefined) return false;
        const sourceValue = String(source);
        switch (field.conditionOperator ?? 'equals') {
            case 'equals':
                return sourceValue === (field.conditionValue ?? '');
            case 'notEquals':
                return sourceValue !== (field.conditionValue ?? '');
            case 'greaterThan': {
                if (sourceValue === '') return false;
                const left = Number(sourceValue);
                const right = Number(field.conditionValue);
                return Number.isFinite(left) && Number.isFinite(right) && left > right;
            }
        }
    }

    setFileName(field: FormFieldDefinition, event: Event): void {
        const input = event.target as HTMLInputElement;
        const file = input.files?.[0];
        if (file) this.selectedFiles.set(field.id, file);
        else this.selectedFiles.delete(field.id);
        this.controlFor(field).setValue(file?.name ?? '');
    }

    validate(): boolean {
        this.submitted = true;
        this.updateValidators();
        this.form.markAllAsTouched();
        this.validityChange.emit(this.form.valid);
        return this.form.valid;
    }

    getSubmission(): DynamicTicketFormSubmission | null {
        if (!this.validate() || !this.definition) return null;

        const result: DynamicTicketFormSubmission = {
            title: '',
            description: '',
            priority: this.defaultPriority,
            fields: {},
            files: [],
        };
        for (const field of this.displayedFields) {
            if (!this.isFieldVisible(field)) continue;
            const value = this.controlFor(field).value;
            const builtIn = this.builtInKind(field);
            if (builtIn === 'title') result.title = String(value ?? '');
            else if (builtIn === 'description') result.description = String(value ?? '');
            else if (builtIn === 'priority') result.priority = String(value ?? '');
            else {
                result.fields[field.id] = this.toJsonValue(value);
                if (field.type === 'file') {
                    const file = this.selectedFiles.get(field.id);
                    if (file) result.files.push(file);
                }
                if (field.type === 'asset' && value !== null && value !== '') {
                    result.assetId = String(value);
                }
            }
        }
        return result;
    }

    private initializeControls(): void {
        this.formChanges?.unsubscribe();
        this.selectedFiles.clear();
        for (const id of Object.keys(this.form.controls)) this.form.removeControl(id);
        for (const field of this.displayedFields) {
            this.form.addControl(field.id, new FormControl<unknown>(this.initialValue(field)));
        }
        this.updateValidators();
        this.formChanges = this.form.valueChanges.subscribe(() => {
            this.updateValidators();
            this.validityChange.emit(this.form.valid);
        });
        this.submitted = false;
        this.validityChange.emit(this.form.valid);
    }

    private updateValidators(): void {
        for (const field of this.displayedFields) {
            const control = this.form.controls[field.id];
            if (!control) continue;
            const validators: ValidatorFn[] = [];
            if (this.isFieldVisible(field)) {
                if (field.required) validators.push(Validators.required);
            }
            control.setValidators(validators);
            control.updateValueAndValidity({ onlySelf: true, emitEvent: false });
        }
        this.form.updateValueAndValidity({ emitEvent: false });
    }

    private initialValue(field: FormFieldDefinition): unknown {
        const builtIn = this.builtInKind(field);
        if (builtIn === 'priority') return field.defaultValue || this.defaultPriority;
        if (field.type === 'boolean') return field.defaultValue?.toLocaleLowerCase() === 'true';
        if (field.type === 'number') {
            const value = Number(field.defaultValue);
            return field.defaultValue && Number.isFinite(value) ? value : null;
        }
        return field.defaultValue ?? '';
    }

    private builtInKind(field: FormFieldDefinition): 'title' | 'description' | 'priority' | null {
        const name = field.name.trim().toLocaleLowerCase();
        const id = field.id.trim().toLocaleLowerCase();
        if (name === 'тема' || id === 'subject' || id === 'title') return 'title';
        if (name === 'описание' || id === 'description') return 'description';
        if (name === 'критичность' || id === 'criticality' || id === 'priority') return 'priority';
        return null;
    }

    private isCriticality(field: FormFieldDefinition): boolean {
        return this.builtInKind(field) === 'priority';
    }

    isEmpty(value: unknown): boolean {
        return value === null || value === undefined || value === '' || (typeof value === 'string' && !value.trim());
    }

    private toJsonValue(value: unknown): string | number | boolean | null {
        if (typeof value === 'string' || typeof value === 'boolean') return value;
        if (typeof value === 'number' && Number.isFinite(value)) return value;
        return null;
    }
}
