import http from 'k6/http';
import { check } from 'k6';

const baseUrl = (__ENV.BASE_URL || 'http://localhost:5062').replace(/\/$/, '');
const token = __ENV.JWT_TOKEN;
const dictionaryId = __ENV.DICTIONARY_ID || '1';
const assetClassId = __ENV.ASSET_CLASS_ID || '1';
const vus = Number(__ENV.VUS || 5);
const duration = __ENV.DURATION || '2m';

if (!token) {
    throw new Error('Set JWT_TOKEN to a valid admin or agent bearer token.');
}

if (!Number.isInteger(vus) || vus < 1) {
    throw new Error('VUS must be a positive integer.');
}

const endpoints = [
    'dictionaries-list',
    'dictionary-detail',
    'dictionary-values',
    'asset-classes-list',
    'asset-class-detail',
    'asset-class-attributes',
];

const thresholds = {
    http_req_failed: ['rate<0.01'],
};

for (const endpoint of endpoints) {
    thresholds[`http_req_duration{endpoint:${endpoint}}`] = ['p(95)<2000'];
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

function get(endpoint, path) {
    const response = http.get(`${baseUrl}${path}`, {
        ...requestParams,
        tags: { endpoint },
    });

    check(response, {
        [`${endpoint}: HTTP 200`]: (result) => result.status === 200,
        [`${endpoint}: response <= 2s`]: (result) => result.timings.duration <= 2000,
    }, { endpoint });
}

export default function () {
    get('dictionaries-list', '/api/dictionaries');
    get('dictionary-detail', `/api/dictionaries/${dictionaryId}`);
    get('dictionary-values', `/api/dictionaries/${dictionaryId}/values?includeArchived=false`);
    get('asset-classes-list', '/api/asset-classes');
    get('asset-class-detail', `/api/asset-classes/${assetClassId}`);
    get('asset-class-attributes', `/api/asset-classes/${assetClassId}/attributes`);
}

export function handleSummary(data) {
    const durationMetrics = data.metrics.http_req_duration?.values || {};
    const output = [
        'Asset/reference API load test',
        `Requests: ${data.metrics.http_reqs?.values.count ?? 0}`,
        `Aggregate p95: ${durationMetrics['p(95)']?.toFixed(1) ?? 'n/a'} ms`,
        `Aggregate max: ${durationMetrics.max?.toFixed(1) ?? 'n/a'} ms`,
        `HTTP failure rate: ${((data.metrics.http_req_failed?.values.rate ?? 0) * 100).toFixed(2)}%`,
        'Per-endpoint thresholds and checks are included in load-test-summary.json.',
    ].join('\n');

    return {
        stdout: `${output}\n`,
        'load-test-summary.json': JSON.stringify(data, null, 2),
    };
}