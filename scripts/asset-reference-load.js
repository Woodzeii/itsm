import http from 'k6/http';
import { check } from 'k6';

const baseUrl = (__ENV.BASE_URL || 'http://localhost:5062').replace(/\/$/, '');
const token = __ENV.JWT_TOKEN || readDotEnvValue('LOAD_TEST_JWT_TOKEN');
const dictionaryId = parseOptionalId(__ENV.DICTIONARY_ID, 'DICTIONARY_ID');
// const assetClassId = parseOptionalId(__ENV.ASSET_CLASS_ID, 'ASSET_CLASS_ID');
const vus = Number(__ENV.VUS || 5);
const duration = __ENV.DURATION || '2m';
const responseLimitMs = Number(__ENV.RESPONSE_LIMIT_MS || 2000);

if (!token) {
    throw new Error('Set LOAD_TEST_JWT_TOKEN in backend/.env or JWT_TOKEN in the environment.');
}

if (!Number.isInteger(vus) || vus < 1) {
    throw new Error('VUS must be a positive integer.');
}

if (!Number.isFinite(responseLimitMs) || responseLimitMs <= 0) {
    throw new Error('RESPONSE_LIMIT_MS must be a positive number.');
}

const endpoints = [
    'dictionaries-list',
    'dictionary-detail',
    'dictionary-values',
    // 'asset-classes-list',
    // 'asset-class-detail',
    // 'asset-class-attributes',
];

const thresholds = {
    http_req_failed: ['rate<0.01'],
};

for (const endpoint of endpoints) {
    thresholds[`http_req_duration{endpoint:${endpoint}}`] = [
        `p(95)<${responseLimitMs}`,
        `max<${responseLimitMs}`,
    ];
    thresholds[`checks{endpoint:${endpoint}}`] = ['rate==1'];
}

export const options = {
    scenarios: {
        read_load: {
            executor: 'constant-vus',
            vus,
            duration,
        },
    },
    thresholds,
};

const requestParams = {
    headers: {
        Authorization: `Bearer ${token}`,
        Accept: 'application/json',
    },
    timeout: '10s',
};

function readDotEnvValue(key) {
    let contents = '';
    for (const path of ['./backend/.env', '../backend/.env']) {
        try {
            contents = open(path);
            break;
        } catch (_) {
            // Try the path relative to the other likely working directory.
        }
    }

    if (!contents) {
        return '';
    }

    const escapedKey = key.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    const match = contents.match(new RegExp(`^\\s*(?:export\\s+)?${escapedKey}\\s*=\\s*(.*?)\\s*$`, 'm'));
    if (!match) {
        return '';
    }

    let value = match[1].trim();
    if ((value.startsWith('"') && value.endsWith('"')) || (value.startsWith("'") && value.endsWith("'"))) {
        value = value.slice(1, -1);
    } else {
        value = value.replace(/\s+#.*$/, '').trim();
    }
    return value;
}

function parseOptionalId(value, name) {
    if (value === undefined || value === '') {
        return null;
    }

    const id = Number(value);
    if (!Number.isInteger(id) || id < 1) {
        throw new Error(`${name} must be a positive integer.`);
    }
    return id;
}

function get(endpoint, path) {
    const response = http.get(`${baseUrl}${path}`, {
        ...requestParams,
        tags: { endpoint },
    });

    check(response, {
        [`${endpoint}: HTTP 200`]: (result) => result.status === 200,
        [`${endpoint}: response < ${responseLimitMs} ms`]: (result) =>
            result.timings.duration < responseLimitMs,
    }, { endpoint });

    return response;
}

function requireSuccessfulJson(endpoint, path) {
    const response = get(endpoint, path);
    if (response.status !== 200) {
        throw new Error(`${path} returned HTTP ${response.status}; check the API, JWT role, and configured IDs.`);
    }
    return response.json();
}

function isObject(value) {
    return value !== null && typeof value === 'object' && !Array.isArray(value);
}

function requireCatalogIds(value, name, requestedId) {
    if (!Array.isArray(value) || value.length === 0) {
        throw new Error(`${name} endpoint returned no records; seed the database before running the test.`);
    }

    const ids = value.map((item) => item?.id);
    if (!ids.every((id) => Number.isInteger(id) && id > 0)) {
        throw new Error(`${name} endpoint returned an item without a valid numeric id.`);
    }

    if (requestedId !== null && !ids.includes(requestedId)) {
        throw new Error(`${name} ID ${requestedId} was not found in the list endpoint response.`);
    }

    return requestedId === null ? ids : [requestedId];
}

function validateDictionary(dictionaryIdToCheck) {
    const detail = requireSuccessfulJson(
        'dictionary-detail',
        `/api/dictionaries/${dictionaryIdToCheck}`,
    );
    const values = requireSuccessfulJson(
        'dictionary-values',
        `/api/dictionaries/${dictionaryIdToCheck}/values?includeArchived=false`,
    );
    const allValues = requireSuccessfulJson(
        'dictionary-values',
        `/api/dictionaries/${dictionaryIdToCheck}/values?includeArchived=true`,
    );

    if (!isObject(detail)
        || detail.id !== dictionaryIdToCheck
        || typeof detail.code !== 'string'
        || typeof detail.name !== 'string') {
        throw new Error(`Dictionary ${dictionaryIdToCheck} detail does not match the list entry shape.`);
    }
    if (!Array.isArray(detail.values)) {
        throw new Error(`Dictionary ${dictionaryIdToCheck} detail is missing its values array.`);
    }
    if (!Array.isArray(values) || !Array.isArray(allValues)) {
        throw new Error(`Dictionary ${dictionaryIdToCheck} values endpoint did not return arrays.`);
    }
    if (values.some((value) => !isObject(value)
        || value.dictionaryId !== dictionaryIdToCheck
        || value.isArchived !== false)) {
        throw new Error(`Dictionary ${dictionaryIdToCheck} returned archived or mismatched active values.`);
    }
    if (allValues.some((value) => !isObject(value) || value.dictionaryId !== dictionaryIdToCheck)) {
        throw new Error(`Dictionary ${dictionaryIdToCheck} returned values linked to another dictionary.`);
    }

    const allValueIds = new Set(allValues.map((value) => value.id));
    if (!values.every((value) => allValueIds.has(value.id))) {
        throw new Error(`Dictionary ${dictionaryIdToCheck} active values are missing from the complete values list.`);
    }
}

/*
function validateAssetClass(assetClassIdToCheck) {
    const detail = requireSuccessfulJson(
        'asset-class-detail',
        `/api/asset-classes/${assetClassIdToCheck}`,
    );
    const attributes = requireSuccessfulJson(
        'asset-class-attributes',
        `/api/asset-classes/${assetClassIdToCheck}/attributes`,
    );

    if (!isObject(detail)
        || detail.id !== assetClassIdToCheck
        || typeof detail.code !== 'string'
        || typeof detail.name !== 'string') {
        throw new Error(`Asset class ${assetClassIdToCheck} detail does not match the list entry shape.`);
    }
    if (!Array.isArray(detail.attributes) || !Array.isArray(attributes)) {
        throw new Error(`Asset class ${assetClassIdToCheck} is missing its attributes array.`);
    }
    if (attributes.some((attribute) => !isObject(attribute)
        || attribute.assetClassId !== assetClassIdToCheck)) {
        throw new Error(`Asset class ${assetClassIdToCheck} returned attributes linked to another class.`);
    }
}
*/

export function setup() {
    const dictionaries = requireSuccessfulJson('dictionaries-list', '/api/dictionaries');
    const dictionaryIds = requireCatalogIds(dictionaries, 'Dictionaries', dictionaryId);

    for (const id of dictionaryIds) {
        validateDictionary(id);
    }

    return { dictionaryIds };
}

export default function (data) {
    const dictionaryIdToLoad = data.dictionaryIds[(__VU + __ITER) % data.dictionaryIds.length];

    get('dictionaries-list', '/api/dictionaries');
    get('dictionary-detail', `/api/dictionaries/${dictionaryIdToLoad}`);
    get('dictionary-values', `/api/dictionaries/${dictionaryIdToLoad}/values?includeArchived=false`);
    // get('asset-classes-list', '/api/asset-classes');
    // get('asset-class-detail', `/api/asset-classes/${assetClassIdToLoad}`);
    // get('asset-class-attributes', `/api/asset-classes/${assetClassIdToLoad}/attributes`);

    // TODO добавить в сидер классы активов для их теста
}

export function handleSummary(data) {
    const durationMetrics = data.metrics.http_req_duration?.values || {};
    const output = [
        'Dictionary API load test',
        `Requests: ${data.metrics.http_reqs?.values.count ?? 0}`,
        `Aggregate p95: ${durationMetrics['p(95)']?.toFixed(1) ?? 'n/a'} ms`,
        `Aggregate max: ${durationMetrics.max?.toFixed(1) ?? 'n/a'} ms`,
        `Response limit: ${responseLimitMs} ms (p95 and max per endpoint)`,
        `HTTP failure rate: ${((data.metrics.http_req_failed?.values.rate ?? 0) * 100).toFixed(2)}%`,
        'Dictionary data consistency was checked during setup.',
        'Per-endpoint latency thresholds and response checks are included in test-results/load-test-summary.json.',
    ].join('\n');

    return {
        stdout: `${output}\n`,
        'test-results/load-test-summary.json': JSON.stringify(data, null, 2),
    };
}
