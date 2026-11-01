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
        request.flush([{ id: 1, code: 'priority', name: 'Приоритеты', isSystem: true, valuesCount: 3 }]);

        expect(result).toEqual([{ id: 1, code: 'priority', name: 'Приоритеты', isSystem: true, valuesCount: 3 }]);
    });

    it('requests archived values explicitly', () => {
        api.getValues(5, true).subscribe();

        const request = http.expectOne(
            `${environment.apiBaseUrl}/dictionaries/5/values?includeArchived=true`,
        );
        expect(request.request.method).toBe('GET');
        request.flush([]);
    });

    it('creates a dictionary', () => {
        api.create({ code: 'brand', name: 'Бренды' }).subscribe();

        const request = http.expectOne(`${environment.apiBaseUrl}/dictionaries`);
        expect(request.request.method).toBe('POST');
        expect(request.request.body).toEqual({ code: 'brand', name: 'Бренды' });
        request.flush({ id: 2, code: 'brand', name: 'Бренды', isSystem: false, valuesCount: 0 });
    });

    it('updates a dictionary name', () => {
        api.update(2, { name: 'Производители' }).subscribe();

        const request = http.expectOne(`${environment.apiBaseUrl}/dictionaries/2`);
        expect(request.request.method).toBe('PUT');
        expect(request.request.body).toEqual({ name: 'Производители' });
        request.flush({ id: 2, code: 'brand', name: 'Производители', isSystem: false, valuesCount: 0 });
    });

    it('creates a dictionary value', () => {
        api.createValue(5, { code: 'high', name: 'Высокий', sortOrder: 1 }).subscribe();

        const request = http.expectOne(`${environment.apiBaseUrl}/dictionaries/5/values`);
        expect(request.request.method).toBe('POST');
        expect(request.request.body).toEqual({ code: 'high', name: 'Высокий', sortOrder: 1 });
        request.flush({ id: 9, dictionaryId: 5, code: 'high', name: 'Высокий', isArchived: false, sortOrder: 1 });
    });

    it('archives and deletes values through backend endpoints', () => {
        api.archiveValue(5, 9).subscribe();
        const archive = http.expectOne(`${environment.apiBaseUrl}/dictionaries/5/values/9/archive`);
        expect(archive.request.method).toBe('POST');
        expect(archive.request.body).toEqual({});
        archive.flush({});

        api.deleteValue(5, 9).subscribe();
        const remove = http.expectOne(`${environment.apiBaseUrl}/dictionaries/5/values/9`);
        expect(remove.request.method).toBe('DELETE');
        remove.flush(null);
    });
});
