import { TestBed } from '@angular/core/testing';
import { TicketFormDefinition } from '../../core/workspace/workspace.models';
import { DynamicTicketFormComponent } from './dynamic-ticket-form';

describe('DynamicTicketFormComponent', () => {
    it('reactively hides conditional fields and excludes them from submitted custom values', () => {
        TestBed.configureTestingModule({ imports: [DynamicTicketFormComponent] });
        const fixture = TestBed.createComponent(DynamicTicketFormComponent);
        fixture.componentRef.setInput('definition', {
            ticketTypeId: '4',
            ticketTypeName: 'Инцидент',
            fields: [
                { id: 'subject', name: 'Тема', type: 'string', required: true, visibleToRequester: true, immutable: true },
                { id: 'description', name: 'Описание', type: 'textarea', required: true, visibleToRequester: true, immutable: true },
                { id: 'criticality', name: 'Критичность', type: 'dictionary', required: true, visibleToRequester: true, immutable: true, options: ['Средний'] },
                { id: 'kind', name: 'Тип', type: 'dictionary', required: true, visibleToRequester: true, options: ['Инцидент', 'Запрос'] },
                {
                    id: 'address',
                    name: 'Адрес',
                    type: 'string',
                    required: true,
                    visibleToRequester: true,
                    conditionFieldId: 'kind',
                    conditionOperator: 'equals',
                    conditionValue: 'Инцидент',
                },
            ],
        } satisfies TicketFormDefinition);
        fixture.detectChanges();

        const component = fixture.componentInstance;
        expect(component.isFieldVisible(component.displayedFields[4]!)).toBe(false);
        component.controlFor(component.displayedFields[0]!).setValue('Сбой');
        component.controlFor(component.displayedFields[1]!).setValue('Описание');
        component.controlFor(component.displayedFields[2]!).setValue('Средний');
        component.controlFor(component.displayedFields[3]!).setValue('Инцидент');
        fixture.detectChanges();

        const address = component.displayedFields[4]!;
        expect(component.isFieldVisible(address)).toBe(true);
        expect(component.getSubmission()).toBeNull();
        component.controlFor(address).setValue('Москва');

        expect(component.getSubmission()?.fields).toEqual({ kind: 'Инцидент', address: 'Москва' });

        component.controlFor(component.displayedFields[3]!).setValue('Запрос');
        expect(component.isFieldVisible(address)).toBe(false);
        expect(component.getSubmission()?.fields).toEqual({ kind: 'Запрос' });
    });
});
