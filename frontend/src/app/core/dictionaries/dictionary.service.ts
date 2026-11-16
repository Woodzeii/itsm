import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
    CreateDictionaryRequest,
    CreateDictionaryValueRequest,
    DictionaryDetailDto,
    DictionaryDto,
    DictionaryValueDto,
    UpdateDictionaryRequest,
    UpdateDictionaryValueRequest,
} from './dictionary.models';

const API = `${environment.apiBaseUrl}/dictionaries`;

@Injectable({ providedIn: 'root' })
export class DictionaryApiService {
    private readonly http = inject(HttpClient);

    getAll(includeArchived = false): Observable<DictionaryDto[]> {
        return this.http.get<DictionaryDto[]>(API, {
            params: includeArchived ? { includeArchived: true } : {},
        });
    }

    getByCode(code: string): Observable<DictionaryDetailDto> {
        return this.http.get<DictionaryDetailDto>(`${API}/${encodeURIComponent(code)}`);
    }

    create(req: CreateDictionaryRequest): Observable<DictionaryDto> {
        return this.http.post<DictionaryDto>(API, req);
    }

    update(code: string, req: UpdateDictionaryRequest): Observable<DictionaryDto> {
        return this.http.put<DictionaryDto>(`${API}/${encodeURIComponent(code)}`, req);
    }

    delete(code: string): Observable<void> {
        return this.http.delete<void>(`${API}/${encodeURIComponent(code)}`);
    }

    archiveDictionary(code: string): Observable<DictionaryDto> {
        return this.http.post<DictionaryDto>(`${API}/${encodeURIComponent(code)}/archive`, {});
    }

    restoreDictionary(code: string): Observable<DictionaryDto> {
        return this.http.post<DictionaryDto>(`${API}/${encodeURIComponent(code)}/restore`, {});
    }

    getValues(dictionaryCode: string, includeArchived = false): Observable<DictionaryValueDto[]> {
        return this.http.get<DictionaryValueDto[]>(`${API}/${encodeURIComponent(dictionaryCode)}/values`, {
            params: includeArchived ? { includeArchived: true } : {},
        });
    }

    createValue(dictionaryCode: string, req: CreateDictionaryValueRequest): Observable<DictionaryValueDto> {
        return this.http.post<DictionaryValueDto>(`${API}/${encodeURIComponent(dictionaryCode)}/values`, req);
    }

    updateValue(
        dictionaryCode: string,
        valueCode: string,
        req: UpdateDictionaryValueRequest,
    ): Observable<DictionaryValueDto> {
        return this.http.put<DictionaryValueDto>(
            `${API}/${encodeURIComponent(dictionaryCode)}/values/${encodeURIComponent(valueCode)}`,
            req,
        );
    }

    archiveValue(dictionaryCode: string, valueCode: string): Observable<DictionaryValueDto> {
        return this.http.post<DictionaryValueDto>(
            `${API}/${encodeURIComponent(dictionaryCode)}/values/${encodeURIComponent(valueCode)}/archive`, {},
        );
    }

    restoreValue(dictionaryCode: string, valueCode: string): Observable<DictionaryValueDto> {
        return this.http.post<DictionaryValueDto>(
            `${API}/${encodeURIComponent(dictionaryCode)}/values/${encodeURIComponent(valueCode)}/restore`, {},
        );
    }

    deleteValue(dictionaryCode: string, valueCode: string): Observable<void> {
        return this.http.delete<void>(
            `${API}/${encodeURIComponent(dictionaryCode)}/values/${encodeURIComponent(valueCode)}`,
        );
    }
}