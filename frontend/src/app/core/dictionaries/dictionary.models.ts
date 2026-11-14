export interface DictionaryDto {
    code: string;
    name: string;
    isSystem: boolean;
    isArchived: boolean;
    valuesCount: number;
}

export interface DictionaryValueDto {
    dictionaryCode: string;
    code: string;
    name: string;
    isArchived: boolean;
    sortOrder: number;
}

export interface DictionaryDetailDto {
    code: string;
    name: string;
    isSystem: boolean;
    isArchived: boolean;
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

export interface UpdateDictionaryValueRequest {
    name: string;
    sortOrder: number;
}