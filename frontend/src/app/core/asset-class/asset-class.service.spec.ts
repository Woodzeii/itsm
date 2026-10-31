import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../../environments/environment';
import { AssetClassApiService } from './asset-class.service';

describe('AssetClassApiService', () => {
    let api: AssetClassApiService;
    let http: HttpTestingController;

    beforeEach(() => {
        TestBed.configureTestingModule({
            providers: [AssetClassApiService, provideHttpClient(), provideHttpClientTesting()],
        });
        api = TestBed.inject(AssetClassApiService);
        http = TestBed.inject(HttpTestingController);
    });

    afterEach(() => http.verify());

    it('loads asset classes from the backend', () => {
        let result: unknown;
        api.getAll().subscribe(value => result = value);

        const request = http.expectOne(`${environment.apiBaseUrl}/asset-classes`);
        expect(request.request.method).toBe('GET');
        request.flush([{ id: 1, code: 'laptop', name: 'Ноутбуки', isActive: true, attributesCount: 2, assetsCount: 4 }]);

        expect(result).toEqual([{ id: 1, code: 'laptop', name: 'Ноутбуки', isActive: true, attributesCount: 2, assetsCount: 4 }]);
    });

    it('loads a class with attributes', () => {
        api.getById(1).subscribe();

        const request = http.expectOne(`${environment.apiBaseUrl}/asset-classes/1`);
        expect(request.request.method).toBe('GET');
        request.flush({ id: 1, code: 'laptop', name: 'Ноутбуки', isActive: true, attributes: [] });
    });

    it('creates and updates an asset class', () => {
        api.create({ code: 'printer', name: 'Принтеры' }).subscribe();
        const create = http.expectOne(`${environment.apiBaseUrl}/asset-classes`);
        expect(create.request.method).toBe('POST');
        expect(create.request.body).toEqual({ code: 'printer', name: 'Принтеры' });
        create.flush({ id: 2, code: 'printer', name: 'Принтеры', isActive: true, attributesCount: 0, assetsCount: 0 });

        api.update(2, { name: 'МФУ' }).subscribe();
        const update = http.expectOne(`${environment.apiBaseUrl}/asset-classes/2`);
        expect(update.request.method).toBe('PUT');
        expect(update.request.body).toEqual({ name: 'МФУ' });
        update.flush({ id: 2, code: 'printer', name: 'МФУ', isActive: true, attributesCount: 0, assetsCount: 0 });
    });

    it('activates and deactivates a class through POST endpoints', () => {
        api.deactivate(1).subscribe();
        const deactivate = http.expectOne(`${environment.apiBaseUrl}/asset-classes/1/deactivate`);
        expect(deactivate.request.method).toBe('POST');
        deactivate.flush({});

        api.activate(1).subscribe();
        const activate = http.expectOne(`${environment.apiBaseUrl}/asset-classes/1/activate`);
        expect(activate.request.method).toBe('POST');
        activate.flush({});
    });

    it('creates an attribute using the backend contract', () => {
        api.createAttribute(1, {
            code: 'ram',
            name: 'ОЗУ',
            dataType: 'number',
            isRequired: true,
            defaultValue: '16',
            options: null,
            sortOrder: 2,
        }).subscribe();

        const request = http.expectOne(`${environment.apiBaseUrl}/asset-classes/1/attributes`);
        expect(request.request.method).toBe('POST');
        expect(request.request.body).toEqual({
            code: 'ram',
            name: 'ОЗУ',
            dataType: 'number',
            isRequired: true,
            defaultValue: '16',
            options: null,
            sortOrder: 2,
        });
        request.flush({});
    });

    it('deletes an attribute through the backend endpoint', () => {
        api.deleteAttribute(1, 7).subscribe();

        const request = http.expectOne(`${environment.apiBaseUrl}/asset-classes/1/attributes/7`);
        expect(request.request.method).toBe('DELETE');
        request.flush(null);
    });
});
