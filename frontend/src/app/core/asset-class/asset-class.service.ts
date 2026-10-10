import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
    AssetClassDto,
    AssetClassDetailDto,
    AssetClassAttributeDto,
    CreateAssetClassRequest,
    UpdateAssetClassRequest,
    CreateAssetClassAttributeRequest,
    UpdateAssetClassAttributeRequest,
} from './asset-class.models';

const API = `${environment.apiUrl}/api/asset-classes`;

@Injectable({ providedIn: 'root' })
export class AssetClassApiService {
    private readonly http = inject(HttpClient);

    getAll(): Observable<AssetClassDto[]> {
        return this.http.get<AssetClassDto[]>(API);
    }

    getById(id: number): Observable<AssetClassDetailDto> {
        return this.http.get<AssetClassDetailDto>(`${API}/${id}`);
    }

    create(req: CreateAssetClassRequest): Observable<AssetClassDto> {
        return this.http.post<AssetClassDto>(API, req);
    }

    update(id: number, req: UpdateAssetClassRequest): Observable<AssetClassDto> {
        return this.http.put<AssetClassDto>(`${API}/${id}`, req);
    }

    delete(id: number): Observable<void> {
        return this.http.delete<void>(`${API}/${id}`);
    }

    deactivate(id: number): Observable<AssetClassDto> {
        return this.http.post<AssetClassDto>(`${API}/${id}/deactivate`, {});
    }

    activate(id: number): Observable<AssetClassDto> {
        return this.http.post<AssetClassDto>(`${API}/${id}/activate`, {});
    }

    getAttributes(classId: number): Observable<AssetClassAttributeDto[]> {
        return this.http.get<AssetClassAttributeDto[]>(`${API}/${classId}/attributes`);
    }

    getAttribute(classId: number, attributeId: number): Observable<AssetClassAttributeDto> {
        return this.http.get<AssetClassAttributeDto>(`${API}/${classId}/attributes/${attributeId}`);
    }

    createAttribute(classId: number, req: CreateAssetClassAttributeRequest): Observable<AssetClassAttributeDto> {
        return this.http.post<AssetClassAttributeDto>(`${API}/${classId}/attributes`, req);
    }

    updateAttribute(classId: number, attributeId: number, req: UpdateAssetClassAttributeRequest): Observable<AssetClassAttributeDto> {
        return this.http.put<AssetClassAttributeDto>(`${API}/${classId}/attributes/${attributeId}`, req);
    }

    deleteAttribute(classId: number, attributeId: number): Observable<void> {
        return this.http.delete<void>(`${API}/${classId}/attributes/${attributeId}`);
    }
}