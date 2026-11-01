import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { AssetClassApiService } from '../../core/asset-class/asset-class.service';
import {
    AssetClassAttributeDto,
    AssetClassDetailDto,
    AssetClassDto,
} from '../../core/asset-class/asset-class.models';
import { AuthService } from '../../core/auth/auth.service';
import { AssetClassesComponent } from './asset-classes';

describe('AssetClassesComponent live API', () => {
    const assetClass: AssetClassDto = {
        id: 1,
        code: 'laptop',
        name: 'Ноутбуки',
        isActive: true,
        attributesCount: 1,
        assetsCount: 0,
    };
    const attribute: AssetClassAttributeDto = {
        id: 7,
        assetClassId: 1,
        code: 'ram',
        name: 'ОЗУ',
        dataType: 'number',
        isRequired: true,
        defaultValue: '16',
        options: null,
        sortOrder: 1,
    };
    const detail: AssetClassDetailDto = {
        ...assetClass,
        createdAt: '2026-10-31T10:00:00Z',
        attributes: [attribute],
    };

    let api: {
        getAll: ReturnType<typeof vi.fn>;
        getById: ReturnType<typeof vi.fn>;
        create: ReturnType<typeof vi.fn>;
        deactivate: ReturnType<typeof vi.fn>;
        activate: ReturnType<typeof vi.fn>;
        delete: ReturnType<typeof vi.fn>;
        createAttribute: ReturnType<typeof vi.fn>;
        deleteAttribute: ReturnType<typeof vi.fn>;
    };

    beforeEach(async () => {
        api = {
            getAll: vi.fn().mockReturnValue(of([assetClass])),
            getById: vi.fn().mockReturnValue(of(detail)),
            create: vi.fn().mockReturnValue(of(assetClass)),
            deactivate: vi.fn().mockReturnValue(of({ ...assetClass, isActive: false })),
            activate: vi.fn().mockReturnValue(of(assetClass)),
            delete: vi.fn().mockReturnValue(of(void 0)),
            createAttribute: vi.fn().mockReturnValue(of(attribute)),
            deleteAttribute: vi.fn().mockReturnValue(of(void 0)),
        };

        await TestBed.configureTestingModule({
            imports: [AssetClassesComponent],
            providers: [
                { provide: AssetClassApiService, useValue: api },
                { provide: AuthService, useValue: { isAdmin: () => true } },
            ],
        }).compileComponents();
    });

    it('loads classes and attributes from the HTTP API', async () => {
        const fixture = TestBed.createComponent(AssetClassesComponent);
        fixture.detectChanges();
        await fixture.whenStable();
        fixture.detectChanges();

        expect(api.getAll).toHaveBeenCalled();
        expect((fixture.nativeElement as HTMLElement).textContent).toContain('Ноутбуки');

        fixture.componentInstance.select(assetClass);
        fixture.detectChanges();

        expect(api.getById).toHaveBeenCalledWith(1);
        expect((fixture.nativeElement as HTMLElement).textContent).toContain('ОЗУ');
    });

    it('creates a class through the live API', () => {
        const fixture = TestBed.createComponent(AssetClassesComponent);
        fixture.detectChanges();
        const component = fixture.componentInstance;
        component.newCode = 'printer';
        component.newName = 'Принтеры';

        component.createClass();

        expect(api.create).toHaveBeenCalledWith({ code: 'printer', name: 'Принтеры' });
        expect(api.getAll).toHaveBeenCalledTimes(2);
    });

    it('creates an attribute for the selected class', () => {
        const fixture = TestBed.createComponent(AssetClassesComponent);
        fixture.detectChanges();
        const component = fixture.componentInstance;
        component.selected = detail;
        component.newAttrCode = 'cpu';
        component.newAttrName = 'Процессор';
        component.newAttrDataType = 'string';
        component.newAttrRequired = true;
        component.newAttrSort = 2;

        component.createAttribute();

        expect(api.createAttribute).toHaveBeenCalledWith(1, {
            code: 'cpu',
            name: 'Процессор',
            dataType: 'string',
            isRequired: true,
            defaultValue: null,
            options: null,
            sortOrder: 2,
        });
    });

    it('deactivates and activates the selected class', () => {
        const fixture = TestBed.createComponent(AssetClassesComponent);
        fixture.detectChanges();
        const component = fixture.componentInstance;
        component.selected = detail;

        component.deactivate();
        expect(api.deactivate).toHaveBeenCalledWith(1);

        component.selected = detail;
        component.activate();
        expect(api.activate).toHaveBeenCalledWith(1);
    });
});
