import assert from 'node:assert/strict';
import { performance } from 'node:perf_hooks';
import { authenticator } from 'otplib';
import { chromium } from 'playwright';

const pageLoadLimitMs = 2000;
const baseUrl = process.env.E2E_BASE_URL || 'http://localhost:4200';
const username = requiredEnv('E2E_ADMIN_USERNAME');
const password = requiredEnv('E2E_ADMIN_PASSWORD');
const totpSecret = requiredEnv('E2E_TOTP_SECRET');
const forbiddenStatuses = new Set([400, 401, 403]);
const httpFailures = [];

function requiredEnv(name) {
    const value = process.env[name];
    assert.ok(value, `Environment variable ${name} is required`);
    return value;
}

function assertWithinLimit(label, startedAt) {
    const elapsedMs = performance.now() - startedAt;
    assert.ok(
        elapsedMs <= pageLoadLimitMs,
        `${label} took ${elapsedMs.toFixed(0)}ms; limit is ${pageLoadLimitMs}ms`,
    );
    return elapsedMs;
}

function parseJwt(token) {
    const parts = token.split('.');
    assert.equal(parts.length, 3, 'Access token must have three JWT segments');

    const payload = JSON.parse(Buffer.from(parts[1], 'base64url').toString('utf8'));
    assert.equal(typeof payload, 'object', 'JWT payload must be a JSON object');
    assert.ok(payload && !Array.isArray(payload), 'JWT payload must be a JSON object');
    return payload;
}

function readRoles(payload) {
    const roles = typeof payload.roles === 'string'
        ? JSON.parse(payload.roles)
        : payload.roles;
    assert.ok(Array.isArray(roles), 'JWT roles claim must parse as an array');
    return roles;
}

function assertNoForbiddenResponses() {
    assert.deepEqual(httpFailures, [], 'No HTTP 400, 401, or 403 responses are allowed');
}

async function main() {
    const browser = await chromium.launch({ headless: true });

    try {
        const page = await browser.newPage();
        page.setDefaultTimeout(10_000);
        page.on('response', response => {
            if (forbiddenStatuses.has(response.status())) {
                httpFailures.push(`${response.status()} ${response.url()}`);
            }
        });

        const loginStartedAt = performance.now();
        await page.goto(new URL('/login', baseUrl).toString(), { waitUntil: 'domcontentloaded' });
        await page.getByRole('heading', { name: 'Авторизация' }).waitFor({ state: 'visible' });
        await page.waitForLoadState('networkidle', { timeout: pageLoadLimitMs });
        const loginPageMs = assertWithinLimit('Login page load', loginStartedAt);
        assertNoForbiddenResponses();

        const loginResponsePromise = page.waitForResponse(response =>
            response.request().method() === 'POST' && response.url().endsWith('/api/auth/login'),
        );
        await page.locator('input[name="username"]').fill(username);
        await page.locator('input[name="password"]').fill(password);
        const loginActionStartedAt = performance.now();
        await page.getByRole('button', { name: 'Войти' }).click();
        const loginResponse = await loginResponsePromise;
        assertNoForbiddenResponses();
        assert.equal(loginResponse.status(), 200, 'Login request must succeed');
        const loginResult = await loginResponse.json();
        assert.equal(loginResult.requiresTwoFactor, true, 'Login must require TOTP');
        assert.equal(loginResult.requiresTwoFactorSetup, false, 'Test admin must already have 2FA configured');

        await page.locator('input[name="code"]').waitFor({ state: 'visible' });
        await page.waitForLoadState('networkidle', { timeout: pageLoadLimitMs });
        const twoFactorFormMs = assertWithinLimit('2FA form load', loginActionStartedAt);
        assertNoForbiddenResponses();

        const code = authenticator.generate(totpSecret);
        const verifyResponsePromise = page.waitForResponse(response =>
            response.request().method() === 'POST' && response.url().endsWith('/api/auth/verify-2fa'),
        );
        await page.locator('input[name="code"]').fill(code);
        const roleSelectionStartedAt = performance.now();
        await page.getByRole('button', { name: 'Подтвердить' }).click();
        const verifyResponse = await verifyResponsePromise;
        assertNoForbiddenResponses();
        assert.equal(verifyResponse.status(), 200, 'TOTP verification request must succeed');
        await page.getByRole('heading', { name: 'Выберите рабочее пространство' }).waitFor({ state: 'visible' });
        await page.waitForURL(url => url.pathname === '/select-role');
        await page.waitForLoadState('networkidle', { timeout: pageLoadLimitMs });
        const roleSelectionMs = assertWithinLimit('Role selection screen load', roleSelectionStartedAt);
        assertNoForbiddenResponses();

        const initialToken = await page.evaluate(() => localStorage.getItem('itsm_access_token'));
        assert.ok(initialToken, 'JWT must be saved after successful 2FA');
        const initialPayload = parseJwt(initialToken);
        assert.equal(initialPayload.Login, username, 'JWT must identify the test admin');
        assert.ok(Number.isInteger(initialPayload.exp), 'JWT must include an expiration claim');
        const availableRoles = readRoles(initialPayload);
        assert.ok(availableRoles.includes('admin'), 'JWT roles must include admin');
        assert.ok(availableRoles.length > 1, 'Test admin must have more than one role');

        const selectRoleResponsePromise = page.waitForResponse(response =>
            response.request().method() === 'POST' && response.url().endsWith('/api/auth/session/role'),
        );
        const adminPageStartedAt = performance.now();
        await page.getByRole('button', { name: /Админка/ }).click();
        const selectRoleResponse = await selectRoleResponsePromise;
        assertNoForbiddenResponses();
        assert.equal(selectRoleResponse.status(), 200, 'Selecting the admin role must succeed');
        await page.waitForURL(url => url.pathname === '/admin/settings');
        await page.getByRole('heading', { name: 'Настройки Service Desk' }).waitFor({ state: 'visible' });
        await page.waitForLoadState('networkidle', { timeout: pageLoadLimitMs });
        const adminPageMs = assertWithinLimit('Admin settings page load', adminPageStartedAt);

        const selectedToken = await page.evaluate(() => localStorage.getItem('itsm_access_token'));
        assert.ok(selectedToken, 'Role selection must leave an access token');
        const selectedPayload = parseJwt(selectedToken);
        assert.equal(selectedPayload.activeRole, 'admin', 'Selected JWT must carry the admin role');
        assert.ok(readRoles(selectedPayload).includes('admin'), 'Selected JWT must preserve the admin role');

        assertNoForbiddenResponses();
        console.log(
            `Auth smoke E2E passed: login ${loginPageMs.toFixed(0)}ms, `
            + `2FA ${twoFactorFormMs.toFixed(0)}ms, role selection ${roleSelectionMs.toFixed(0)}ms, `
            + `admin page ${adminPageMs.toFixed(0)}ms.`,
        );
    } finally {
        await browser.close();
    }
}

main().catch(error => {
    console.error(error);
    process.exitCode = 1;
});
