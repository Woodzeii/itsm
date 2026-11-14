import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../../environments/environment';
import { DictionaryApiService } from './dictionary.service';

describe('DictionaryApiService', () => {
    let api: DictionaryApiService;
    let http: HttpTestingController;

    beforeEach(() => {
        TestBed.configureTestingModule({
            providers: [DictionaryApiService, provideHttpClient(), provideHttpClientTesting()],
        });
        api = TestBed.inject(DictionaryApiService);
        http = TestBed.inject(HttpTestingController);
    });

    afterEach(() => http.verify());

    it('loads dictionaries from the backend', () => {
        let result: unknown;
        api.getAll().subscribe(value => result = value);

        const request = http.expectOne(`${environment.apiBaseUrl}/dictionaries`);
        expect(request.request.method).toBe('GET');
        request.flush([{ code: 'priority', name: 'Приоритеты', isSystem: true, isArchived: false, valuesCount: 3 }]);

        expect(result).toEqual([{ code: 'priority', name: 'Приоритеты', isSystem: true, isArchived: false, valuesCount: 3 }]);
    });

    it('requests archived values explicitly', () => {
        api.getValues('warehouses', true).subscribe();

        const request = http.expectOne(
            `${environment.apiBaseUrl}/dictionaries/warehouses/values?includeArchived=true`,
        );
        expect(request.request.method).toBe('GET');
        request.flush([]);
    });

    it('creates a dictionary', () => {
        api.create({ code: 'brand', name: 'Бренды' }).subscribe();

        const request = http.expectOne(`${environment.apiBaseUrl}/dictionaries`);
        expect(request.request.method).toBe('POST');
        expect(request.request.body).toEqual({ code: 'brand', name: 'Бренды' });
        request.flush({ code: 'brand', name: 'Бренды', isSystem: false, isArchived: false, valuesCount: 0 });
    });

    it('updates a dictionary name', () => {
        api.update('brand', { name: 'Производители' }).subscribe();

        const request = http.expectOne(`${environment.apiBaseUrl}/dictionaries/brand`);
        expect(request.request.method).toBe('PUT');
        expect(request.request.body).toEqual({ name: 'Производители' });
        request.flush({ code: 'brand', name: 'Производители', isSystem: false, isArchived: false, valuesCount: 0 });
    });

    it('creates a dictionary value', () => {
        api.createValue('priority', { code: 'high', name: 'Высокий', sortOrder: 1 }).subscribe();

        const request = http.expectOne(`${environment.apiBaseUrl}/dictionaries/priority/values`);
        expect(request.request.method).toBe('POST');
        expect(request.request.body).toEqual({ code: 'high', name: 'Высокий', sortOrder: 1 });
        request.flush({ dictionaryCode: 'priority', code: 'high', name: 'Высокий', isArchived: false, sortOrder: 1 });
    });

    it('updates a dictionary value without changing its code', () => {
        api.updateValue('priority', 'high', { name: 'Высокий приоритет', sortOrder: 2 }).subscribe();

        const request = http.expectOne(`${environment.apiBaseUrl}/dictionaries/priority/values/high`);
        expect(request.request.method).toBe('PUT');
        expect(request.request.body).toEqual({ name: 'Высокий приоритет', sortOrder: 2 });
        request.flush({ dictionaryCode: 'priority', code: 'high', name: 'Высокий приоритет', isArchived: false, sortOrder: 2 });
    });

    it('archives and deletes values through backend endpoints', () => {
        api.archiveValue('priority', 'high').subscribe();
        const archive = http.expectOne(`${environment.apiBaseUrl}/dictionaries/priority/values/high/archive`);
        expect(archive.request.method).toBe('POST');
        expect(archive.request.body).toEqual({});
        archive.flush({});

        api.deleteValue('priority', 'high').subscribe();
        const remove = http.expectOne(`${environment.apiBaseUrl}/dictionaries/priority/values/high`);
        expect(remove.request.method).toBe('DELETE');
        remove.flush(null);
    });

    it('restores a dictionary value by code', () => {
        api.restoreValue('priority', 'high').subscribe();

        const request = http.expectOne(`${environment.apiBaseUrl}/dictionaries/priority/values/high/restore`);
        expect(request.request.method).toBe('POST');
        request.flush({});
    });
});
