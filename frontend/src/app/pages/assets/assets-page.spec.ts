import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AuthService } from '../../core/auth/auth.service';
import { HttpWorkspaceApiService } from '../../core/workspace/http-workspace-api.service';
import { environment } from '../../../environments/environment';
import { AssetsPageComponent } from './assets-page';

describe('AssetsPageComponent backend integration', () => {
    let http: HttpTestingController;

    beforeEach(async () => {
        await TestBed.configureTestingModule({
            imports: [AssetsPageComponent],
            providers: [
                HttpWorkspaceApiService,
                provideHttpClient(),
                provideHttpClientTesting(),
                { provide: AuthService, useValue: { canRead: () => true } },
            ],
        }).compileComponents();
        http = TestBed.inject(HttpTestingController);
    });

    afterEach(() => http.verify());

    it('loads assets and their class definitions from the backend', async () => {
        const fixture = TestBed.createComponent(AssetsPageComponent);
        fixture.detectChanges();

        const assetsRequest = http.expectOne(`${environment.apiBaseUrl}/assets`);
        expect(assetsRequest.request.method).toBe('GET');

        const classesRequest = http.expectOne(`${environment.apiBaseUrl}/asset-classes`);
        expect(classesRequest.request.method).toBe('GET');

        for (const code of ['warehouses', 'locations', 'departments']) {
            const valuesRequest = http.expectOne(
                `${environment.apiBaseUrl}/dictionaries/${code}/values?includeArchived=true`,
            );
            expect(valuesRequest.request.method).toBe('GET');
            valuesRequest.flush([]);
        }

        classesRequest.flush([{
            id: 3,
            code: 'laptop',
            name: 'Ноутбуки',
            isActive: true,
        }]);
        const attributesRequest = http.expectOne(`${environment.apiBaseUrl}/asset-classes/3/attributes`);
        attributesRequest.flush([{
            code: 'ram',
            name: 'ОЗУ',
            dataType: 'number',
            isRequired: true,
            defaultValue: '16',
            options: null,
        }]);
        assetsRequest.flush([{
            id: 15,
            inventoryNumber: 'INV-15',
            name: 'Ноутбук Dell',
            assetClassCode: 'laptop',
            assetClassName: 'Ноутбуки',
            lifecycleStage: 'InStock',
            lifecycleStageName: 'На складе',
        }]);

        await fixture.whenStable();
        fixture.detectChanges();
        expect(fixture.componentInstance.assets).toHaveLength(1);
        expect((fixture.nativeElement as HTMLElement).textContent).toContain('Ноутбук Dell');
        expect(fixture.componentInstance.activeAssetClasses[0].attributes[0].code).toBe('ram');
    });
});
