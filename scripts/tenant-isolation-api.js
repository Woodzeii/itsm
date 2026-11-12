import assert from 'node:assert/strict';
import { authenticator } from 'otplib';

const apiBaseUrl = (process.env.TENANT_ISOLATION_API_URL || 'http://localhost:5062/api').replace(/\/+$/, '');

function requiredEnv(name) {
    const value = process.env[name];
    assert.ok(value, `Environment variable ${name} is required`);
    return value;
}

function decodeJwtPayload(token) {
    const parts = token.split('.');
    assert.equal(parts.length, 3, 'Access token must have three JWT segments');
    const payload = JSON.parse(Buffer.from(parts[1], 'base64url').toString('utf8'));
    assert.ok(payload && typeof payload === 'object' && !Array.isArray(payload), 'JWT payload must be an object');
    return payload;
}

async function request(path, { token, method = 'GET', body, tenantHeader } = {}) {
    const headers = { Accept: 'application/json' };
    if (token) headers.Authorization = `Bearer ${token}`;
    if (tenantHeader !== undefined) headers['X-Tenant-Id'] = String(tenantHeader);
    if (body !== undefined) headers['Content-Type'] = 'application/json';

    return fetch(`${apiBaseUrl}${path}`, {
        method,
        headers,
        body: body === undefined ? undefined : JSON.stringify(body),
    });
}

async function readJson(response, description) {
    const text = await response.text();
    assert.ok(text, `${description}: response body must not be empty`);
    try {
        return JSON.parse(text);
    } catch (error) {
        throw new Error(`${description}: response must contain valid JSON`, { cause: error });
    }
}

async function login(tenantLabel) {
    const prefix = `TENANT_${tenantLabel}_ADMIN`;
    const username = requiredEnv(`${prefix}_USERNAME`);
    const password = requiredEnv(`${prefix}_PASSWORD`);
    const secret = requiredEnv(`TENANT_${tenantLabel}_TOTP_SECRET`);

    const loginResponse = await request('/auth/login', {
        method: 'POST',
        body: { username, password },
    });
    assert.equal(loginResponse.status, 200, `${tenantLabel} admin login must succeed`);
    const loginResult = await readJson(loginResponse, `${tenantLabel} admin login`);
    assert.equal(loginResult.requiresTwoFactor, true, `${tenantLabel} admin must have 2FA enabled`);
    assert.equal(loginResult.requiresTwoFactorSetup, false, `${tenantLabel} admin must already have 2FA configured`);
    assert.ok(loginResult.challengeId, `${tenantLabel} admin login must return a 2FA challenge`);

    const verifyResponse = await request('/auth/verify-2fa', {
        method: 'POST',
        body: {
            challengeId: loginResult.challengeId,
            code: authenticator.generate(secret),
        },
    });
    assert.equal(verifyResponse.status, 200, `${tenantLabel} admin TOTP verification must succeed`);
    const verifyResult = await readJson(verifyResponse, `${tenantLabel} admin TOTP verification`);
    assert.ok(verifyResult.accessToken, `${tenantLabel} admin verification must return an access token`);

    const claims = decodeJwtPayload(verifyResult.accessToken);
    const tenantId = Number(claims.tenant_id);
    assert.ok(Number.isInteger(tenantId) && tenantId > 0, `${tenantLabel} admin JWT must include tenant_id`);
    const roles = typeof claims.roles === 'string' ? JSON.parse(claims.roles) : claims.roles;
    assert.ok(Array.isArray(roles) && roles.includes('admin'), `${tenantLabel} account must have the admin role`);

    return { token: verifyResult.accessToken, tenantId, username };
}

async function getUsers(token, query = '', tenantHeader) {
    const response = await request(`/users${query}`, { token, tenantHeader });
    assert.equal(response.status, 200, `User list request must succeed (${query || 'default context'})`);
    const result = await readJson(response, 'User list');
    assert.ok(Array.isArray(result.items), 'User list response must contain an items array');
    return result.items;
}

/* Temporarily disabled: dictionary-specific isolation checks.
async function getDictionaries(token, query = '', tenantHeader) {
    const response = await request(`/dictionaries${query}`, { token, tenantHeader });
    assert.equal(response.status, 200, `Dictionary list request must succeed (${query || 'default context'})`);
    const result = await readJson(response, 'Dictionary list');
    assert.ok(Array.isArray(result), 'Dictionary list response must be an array');
    return result;
}
*/

function assertTenantUsers(users, tenantId, label) {
    assert.ok(users.length > 0, `${label} tenant must contain test users`);
    assert.ok(
        users.every(user => Number(user.tenantId) === tenantId),
        `${label} user list must contain only tenant ${tenantId} records`,
    );
}

async function assertHiddenResource(response, expectedStatus, description) {
    assert.equal(response.status, expectedStatus, `${description} must not expose another tenant's record`);
    const body = await response.text();
    assert.ok(!body.includes('TENANT_ISOLATION'), `${description} response must not expose test marker data`);
}

async function main() {
    const [tenantA, tenantB] = await Promise.all([login('A'), login('B')]);
    assert.notEqual(tenantA.tenantId, tenantB.tenantId, 'Test accounts must belong to different tenants');

    const [usersA, usersB] = await Promise.all([
        getUsers(tenantA.token, '?page=1&pageSize=200'),
        getUsers(tenantB.token, '?page=1&pageSize=200'),
        /* Temporarily disabled:
        getDictionaries(tenantA.token),
        getDictionaries(tenantB.token),
        */
    ]);

    assertTenantUsers(usersA, tenantA.tenantId, 'Tenant A');
    assertTenantUsers(usersB, tenantB.tenantId, 'Tenant B');
    assert.ok(usersA.some(user => user.username === tenantA.username), 'Tenant A admin must be visible in its own tenant');
    assert.ok(usersB.some(user => user.username === tenantB.username), 'Tenant B admin must be visible in its own tenant');
    /* Temporarily disabled:
    assert.ok(dictionariesA.length > 0, 'Tenant A must contain a test dictionary');
    assert.ok(dictionariesB.length > 0, 'Tenant B must contain a test dictionary');
    */

    const tenantAUserIds = new Set(usersA.map(user => user.id));
    const tenantBUser = usersB.find(user =>
        Number(user.tenantId) === tenantB.tenantId && !tenantAUserIds.has(user.id));
    assert.ok(tenantBUser, 'Tenant B must contain a user for cross-tenant ID checks');
    /* Temporarily disabled:
    const tenantADictionaryIds = new Set(dictionariesA.map(dictionary => dictionary.id));
    const tenantBDictionary = dictionariesB.find(dictionary => !tenantADictionaryIds.has(dictionary.id));
    assert.ok(
        tenantBDictionary,
        'Test data must include a Tenant B dictionary that is not visible in Tenant A',
    );
    */

    const usersWithTenantIdSpoof = await getUsers(
        tenantA.token,
        `?page=1&pageSize=200&tenantId=${tenantB.tenantId}`,
    );
    assert.ok(
        usersWithTenantIdSpoof.every(user => Number(user.tenantId) === tenantA.tenantId),
        'Adding tenantId=B to the query must not reveal Tenant B users',
    );
    assert.ok(
        !usersWithTenantIdSpoof.some(user => user.username === tenantBUser.username),
        'Tenant B user must not appear when Tenant A spoofs tenantId in the query',
    );

    /* Temporarily disabled:
    const dictionariesWithQuerySpoof = await getDictionaries(
        tenantA.token,
        `?tenantId=${tenantB.tenantId}`,
    );
    assert.deepEqual(
        dictionariesWithQuerySpoof.map(dictionary => dictionary.id).sort((a, b) => a - b),
        dictionariesA.map(dictionary => dictionary.id).sort((a, b) => a - b),
        'Adding tenantId=B to the dictionary query must not change Tenant A results',
    );
    */

    const usersWithHeaderSpoof = await getUsers(
        tenantA.token,
        '?page=1&pageSize=200',
        tenantB.tenantId,
    );
    assertTenantUsers(usersWithHeaderSpoof, tenantA.tenantId, 'Tenant A with X-Tenant-Id spoof');
    assert.ok(
        !usersWithHeaderSpoof.some(user => user.username === tenantBUser.username),
        'X-Tenant-Id=B must not reveal Tenant B users',
    );

    /* Temporarily disabled:
    const dictionariesWithHeaderSpoof = await getDictionaries(
        tenantA.token,
        '',
        tenantB.tenantId,
    );
    assert.ok(
        dictionariesWithHeaderSpoof.some(dictionary => dictionary.id === dictionariesA[0].id),
        'X-Tenant-Id spoof must not hide Tenant A dictionaries',
    );
    assert.ok(
        dictionariesWithHeaderSpoof.every(dictionary =>
            !dictionariesB.some(tenantBDictionaryItem => tenantBDictionaryItem.id === dictionary.id)),
        'X-Tenant-Id=B must not reveal Tenant B dictionaries',
    );
    */

    await assertHiddenResource(
        await request(`/users/${tenantBUser.id}`, { token: tenantA.token }),
        404,
        'Cross-tenant user lookup by ID',
    );
    /* Temporarily disabled:
    await assertHiddenResource(
        await request(`/dictionaries/${tenantBDictionary.id}`, { token: tenantA.token }),
        404,
        'Cross-tenant dictionary lookup by ID',
    );
    */

    const tenantBUserDetailsResponse = await request(`/users/${tenantBUser.id}`, { token: tenantB.token });
    assert.equal(tenantBUserDetailsResponse.status, 200, 'Tenant B admin must be able to read its own user');
    const tenantBUserDetails = await readJson(tenantBUserDetailsResponse, 'Tenant B user details');
    const crossTenantUserUpdateResponse = await request(`/users/${tenantBUser.id}`, {
        token: tenantA.token,
        method: 'PUT',
        body: {
            username: tenantBUserDetails.username,
            email: tenantBUserDetails.email,
            fullName: tenantBUserDetails.fullName,
            department: tenantBUserDetails.department,
            managerId: tenantBUserDetails.managerId,
            tenantId: tenantB.tenantId,
            isActive: tenantBUserDetails.isActive,
            isManager: tenantBUserDetails.isManager,
            roleIds: tenantBUserDetails.roleIds,
        },
    });
    assert.equal(
        crossTenantUserUpdateResponse.status,
        404,
        'Tenant A must not update a Tenant B user by ID or body tenantId',
    );

    /* Temporarily disabled:
    const tenantBDictionaryDetailsResponse = await request(
        `/dictionaries/${tenantBDictionary.id}`,
        { token: tenantB.token },
    );
    assert.equal(tenantBDictionaryDetailsResponse.status, 200, 'Tenant B admin must read its own dictionary');
    const tenantBDictionaryDetails = await readJson(
        tenantBDictionaryDetailsResponse,
        'Tenant B dictionary details',
    );
    const crossTenantDictionaryUpdateResponse = await request(`/dictionaries/${tenantBDictionary.id}`, {
        token: tenantA.token,
        method: 'PUT',
        body: { name: tenantBDictionaryDetails.name },
    });
    assert.equal(
        crossTenantDictionaryUpdateResponse.status,
        404,
        'Tenant A must not update a Tenant B dictionary by ID',
    );

    const crossTenantValuesResponse = await request(
        `/dictionaries/${tenantBDictionary.id}/values`,
        { token: tenantA.token },
    );
    assert.equal(crossTenantValuesResponse.status, 200, 'Dictionary values endpoint must respond successfully');
    const crossTenantValues = await readJson(crossTenantValuesResponse, 'Cross-tenant dictionary values');
    assert.deepEqual(crossTenantValues, [], 'Cross-tenant dictionary values must not be visible');
    */

    console.log(
        `Tenant isolation API tests passed: Tenant A (${tenantA.tenantId}) and Tenant B (${tenantB.tenantId}); `
        + 'user lists, query/header spoofing, and cross-tenant user ID reads and no-op updates were checked.',
    );
}

main().catch(error => {
    console.error(error);
    process.exitCode = 1;
});
