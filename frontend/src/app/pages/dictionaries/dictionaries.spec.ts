import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { DictionaryApiService } from '../../core/dictionaries/dictionary.service';
import { DictionaryDto, DictionaryValueDto } from '../../core/dictionaries/dictionary.models';
import { DictionariesComponent } from './dictionaries';

describe('DictionariesComponent live API', () => {
    const dictionary: DictionaryDto = {
        id: 1,
        code: 'priority',
        name: 'Приоритеты',
        isSystem: false,
        valuesCount: 1,
    };
    const activeValue: DictionaryValueDto = {
        id: 10,
        dictionaryId: 1,
        code: 'high',
        name: 'Высокий',
        isArchived: false,
        sortOrder: 1,
    };
    const archivedValue: DictionaryValueDto = {
        ...activeValue,
        id: 11,
        code: 'old',
        name: 'Старый',
        isArchived: true,
    };

    let api: {
        getAll: ReturnType<typeof vi.fn>;
        getValues: ReturnType<typeof vi.fn>;
        create: ReturnType<typeof vi.fn>;
        delete: ReturnType<typeof vi.fn>;
        createValue: ReturnType<typeof vi.fn>;
        archiveValue: ReturnType<typeof vi.fn>;
        deleteValue: ReturnType<typeof vi.fn>;
    };

    beforeEach(async () => {
        api = {
            getAll: vi.fn().mockReturnValue(of([dictionary])),
            getValues: vi.fn().mockReturnValue(of([activeValue, archivedValue])),
            create: vi.fn().mockReturnValue(of(dictionary)),
            delete: vi.fn().mockReturnValue(of(void 0)),
            createValue: vi.fn().mockReturnValue(of(activeValue)),
            archiveValue: vi.fn().mockReturnValue(of({ ...activeValue, isArchived: true })),
            deleteValue: vi.fn().mockReturnValue(of(void 0)),
        };

        await TestBed.configureTestingModule({
            imports: [DictionariesComponent],
            providers: [
                { provide: DictionaryApiService, useValue: api },
                { provide: AuthService, useValue: { isAdmin: () => true } },
            ],
        }).compileComponents();
    });

    it('loads the list and archived values from the HTTP API', async () => {
        const fixture = TestBed.createComponent(DictionariesComponent);
        fixture.detectChanges();
        await fixture.whenStable();
        fixture.detectChanges();

        expect(api.getAll).toHaveBeenCalled();
        expect((fixture.nativeElement as HTMLElement).textContent).toContain('Приоритеты');

        fixture.componentInstance.select(dictionary);
        fixture.detectChanges();

        expect(api.getValues).toHaveBeenCalledWith(1, true);
        expect((fixture.nativeElement as HTMLElement).textContent).toContain('Старый');
    });

    it('creates dictionaries and reloads the list', () => {
        const fixture = TestBed.createComponent(DictionariesComponent);
        fixture.detectChanges();
        const component = fixture.componentInstance;
        component.newCode = 'brand';
        component.newName = 'Бренды';

        component.createDictionary();

        expect(api.create).toHaveBeenCalledWith({ code: 'brand', name: 'Бренды' });
        expect(api.getAll).toHaveBeenCalledTimes(2);
    });

    it('archives a value and refreshes the selected dictionary', () => {
        const fixture = TestBed.createComponent(DictionariesComponent);
        fixture.detectChanges();
        const component = fixture.componentInstance;
        component.selected = dictionary;

        component.archiveValue(activeValue);

        expect(api.archiveValue).toHaveBeenCalledWith(1, 10);
        expect(api.getValues).toHaveBeenCalledWith(1, true);
    });
});
