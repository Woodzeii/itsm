export interface DictionaryDto {
    id: number;
    code: string;
    name: string;
    isSystem: boolean;
    valuesCount: number;
}

export interface DictionaryValueDto {
    id: number;
    dictionaryId: number;
    code: string;
    name: string;
    isArchived: boolean;
    sortOrder: number;
}

export interface DictionaryDetailDto {
    id: number;
    code: string;
    name: string;
    isSystem: boolean;
    values: DictionaryValueDto[];
}

export interface CreateDictionaryRequest {
    code: string;
    name: string;
}

export interface UpdateDictionaryRequest {
    name: string;
}

export interface CreateDictionaryValueRequest {
    code: string;
    name: string;
    sortOrder: number;
}