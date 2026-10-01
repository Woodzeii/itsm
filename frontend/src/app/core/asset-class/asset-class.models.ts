// ==================== AssetClass ====================

export interface AssetClassDto {
    id: number;
    code: string;
    name: string;
    isActive: boolean;
    attributesCount: number;
    assetsCount: number;
}

export interface AssetClassDetailDto {
    id: number;
    code: string;
    name: string;
    isActive: boolean;
    createdAt: string;
    attributesCount: number;
    assetsCount: number;
    attributes: AssetClassAttributeDto[];
}

export interface CreateAssetClassRequest {
    code: string;
    name: string;
}

export interface UpdateAssetClassRequest {
    name: string;
}

// ==================== AssetClassAttribute ====================

export interface AssetClassAttributeDto {
    id: number;
    assetClassId: number;
    code: string;
    name: string;
    dataType: string;
    isRequired: boolean;
    defaultValue: string | null;
    options: string | null;
    sortOrder: number;
}

export interface CreateAssetClassAttributeRequest {
    code: string;
    name: string;
    dataType: string;
    isRequired: boolean;
    defaultValue: string | null;
    options: string | null;
    sortOrder: number;
}

export interface UpdateAssetClassAttributeRequest {
    name: string;
    dataType: string;
    isRequired: boolean;
    defaultValue: string | null;
    options: string | null;
    sortOrder: number;
}

// Допустимые типы данных (синхронизировано с backend AssetClassAttributeDataTypes.All)
export const ASSET_ATTRIBUTE_DATA_TYPES: { value: string; label: string }[] = [
    { value: 'string',     label: 'Строка' },
    { value: 'text',       label: 'Многострочный текст' },
    { value: 'number',     label: 'Число' },
    { value: 'date',       label: 'Дата' },
    { value: 'bool',       label: 'Флажок (да/нет)' },
    { value: 'dictionary', label: 'Выбор из справочника' },
    { value: 'asset',      label: 'Выбор актива' },
];