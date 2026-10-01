import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
    CreateDictionaryRequest,
    CreateDictionaryValueRequest,
    DictionaryDetailDto,
    DictionaryDto,
    DictionaryValueDto,
    UpdateDictionaryRequest,
} from './dictionary.models';

const API = 'http://localhost:5062/api/dictionaries';

@Injectable({ providedIn: 'root' })
export class DictionaryApiService {
    private readonly http = inject(HttpClient);

    getAll(): Observable<DictionaryDto[]> {
        return this.http.get<DictionaryDto[]>(API);
    }

    getById(id: number): Observable<DictionaryDetailDto> {
        return this.http.get<DictionaryDetailDto>(`${API}/${id}`);
    }

    create(req: CreateDictionaryRequest): Observable<DictionaryDto> {
        return this.http.post<DictionaryDto>(API, req);
    }

    update(id: number, req: UpdateDictionaryRequest): Observable<DictionaryDto> {
        return this.http.put<DictionaryDto>(`${API}/${id}`, req);
    }

    delete(id: number): Observable<void> {
        return this.http.delete<void>(`${API}/${id}`);
    }

    getValues(dictionaryId: number): Observable<DictionaryValueDto[]> {
        return this.http.get<DictionaryValueDto[]>(`${API}/${dictionaryId}/values`);
    }

    createValue(dictionaryId: number, req: CreateDictionaryValueRequest): Observable<DictionaryValueDto> {
        return this.http.post<DictionaryValueDto>(`${API}/${dictionaryId}/values`, req);
    }

    archiveValue(dictionaryId: number, valueId: number): Observable<DictionaryValueDto> {
        return this.http.post<DictionaryValueDto>(`${API}/${dictionaryId}/values/${valueId}/archive`, {});
    }

    deleteValue(dictionaryId: number, valueId: number): Observable<void> {
        return this.http.delete<void>(`${API}/${dictionaryId}/values/${valueId}`);
    }
}