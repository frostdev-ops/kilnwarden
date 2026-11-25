import { test, expect, type Page } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
const dir = process.env.KWA_E2E_DIR!;
const password = 'OnlyDisposableTests-42!';
const name = 'panel-owner';
test.describe.configure({ mode: 'serial' });
const pageErrors = new WeakMap<Page, string[]>();
test.beforeEach(({ page }) => {
  const errors: string[] = []; pageErrors.set(page, errors);
  page.on('pageerror', e => errors.push(e.message));
});
test.afterEach(({ page }) => { expect(pageErrors.get(page)).toEqual([]); });
async function signIn(page: Page) {
  await page.goto('/signin');
  await page.getByLabel('Name', { exact: true }).fill(name);
  await page.getByLabel('Password', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Kilnwarden', exact: true })).toBeVisible();
}
function kapCalls(): { op: string; source: string; level: string }[] {
  return readFileSync(resolve(dir, 'kap-calls.jsonl'), 'utf8').trim().split('\n').filter(Boolean).map(s => JSON.parse(s));
}
async function confirmPassword(page: Page, value = password) {
  const prompt = page.getByRole('dialog', { name: 'Confirm your identity', exact: true });
  await expect(prompt).toBeVisible();
  await prompt.getByLabel('Your password', { exact: true }).fill(value);
  await prompt.getByRole('button', { name: 'Confirm identity', exact: true }).click();
  await expect(prompt.getByText('Identity confirmed', { exact: true })).toBeVisible();
  await prompt.getByRole('button', { name: 'Return to the action', exact: true }).click();
  await expect(prompt).not.toBeVisible();
}
test('setup consumes fragment and creates owner', async ({ page }) => {
  const log = readFileSync(resolve(dir, 'devhost.log'), 'utf8');
  const token = log.match(/\/setup#token=([^\s]+)/)?.[1];
  expect(token).toBeTruthy();
  await page.goto(`/setup#token=${token}`);
  await expect(page.getByRole('heading', { name: 'Create the owner account' })).toBeVisible();
  expect(new URL(page.url()).hash).toBe('');
  await page.getByLabel('Owner name', { exact: true }).fill(name);
  await page.getByLabel('Password', { exact: true }).fill(password);
  await page.getByLabel('Password again', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Create the owner account' }).click();
  await expect(page.getByRole('heading', { name: 'Kilnwarden', exact: true })).toBeVisible();
});
test('step 2 pages open from the navigation, and the overview shows the dashboard widgets', async ({ page }) => {
  await signIn(page);
  // the metrics module's dashboard widgets (the owner may see metrics)
  await expect(page.getByText(/^(online now|server not connected)$/)).toBeVisible();
  const nav = page.getByRole('navigation', { name: 'Modules' });
  for (const [link, heading] of [['Mods', 'Mods'], ['Schedules', 'Schedules'], ['Saved commands', 'Saved commands'], ['Metrics', 'Metrics'],
    ['Webhooks', 'Webhooks'], ['Public page', 'Public page']] as const) {
    await nav.getByRole('link', { name: link, exact: true }).click();
    await expect(page.getByRole('heading', { level: 1, name: heading, exact: true })).toBeVisible();
    await expect(page.getByText('Coming soon', { exact: true })).toHaveCount(0);
  }
  // the public page is off by default: its anonymous route answers 404
  expect((await page.request.get('/api/public/status')).status()).toBe(404);
});
test('KAP recent-auth refuses without dispatch, prompts, and needs an explicit retry', async ({ page }) => {
  const frames: Record<string, unknown>[] = [];
  const sent: Record<string, unknown>[] = [];
  page.on('websocket', ws => {
    ws.on('framereceived', f => { try { frames.push(JSON.parse(String(f.payload))); } catch {} });
    ws.on('framesent', f => { try { sent.push(JSON.parse(String(f.payload))); } catch {} });
  });
  await signIn(page);
  await page.getByRole('navigation', { name: 'Modules' }).getByRole('link', { name: 'Players', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Players', exact: true })).toBeVisible();
  await page.goto('/commands');
  await page.getByRole('button', { name: '/weather' }).click();
  await page.getByLabel('kind', { exact: true }).selectOption('rain');
  await expect(page.getByTestId('command-line')).toHaveText('/weather rain');
  const dispatched = kapCalls().filter(c => c.op === 'command_run').length;
  await page.getByRole('button', { name: 'Run', exact: true }).click();
  const prompt = page.getByRole('dialog', { name: 'Confirm your identity', exact: true });
  await expect(prompt).toBeVisible();
  expect(frames.some(f => f.t === 'res' && f.ok === false && f.code === 'reauth_required' && typeof f.error === 'string')).toBe(true);
  expect(kapCalls().filter(c => c.op === 'command_run')).toHaveLength(dispatched);
  await prompt.getByLabel('Your password', { exact: true }).fill('wrong-fixture-proof');
  const invalidProof = page.waitForResponse('**/api/auth/reauth');
  await prompt.getByRole('button', { name: 'Confirm identity', exact: true }).click();
  expect((await invalidProof).status()).toBe(401);
  await expect(prompt.getByRole('alert')).toBeVisible();
  await confirmPassword(page);
  const me = await jsonCall(page, '/api/auth/me', 'GET');
  expect(me.data.session.reauthenticatedUntil).toMatch(/^\d{4}-\d{2}-\d{2}T/);
  expect(kapCalls().filter(c => c.op === 'command_run')).toHaveLength(dispatched);
  await expect(page.getByTestId('command-output')).not.toContainText('(mock) ran');
  // A new, explicit click is the only dispatch after the proof.
  await page.getByRole('button', { name: 'Run', exact: true }).click();
  await expect(page.getByTestId('command-output')).toContainText('(mock) ran /weather rain');
  expect(kapCalls().filter(c => c.op === 'command_run')).toHaveLength(dispatched + 1);
  for (const f of sent.filter(f => ['commands_describe', 'command_complete', 'command_run'].includes(String(f.op)))) {
    expect(f).not.toHaveProperty('source'); expect(f).not.toHaveProperty('level');
    expect(f.args).not.toHaveProperty('source'); expect(f.args).not.toHaveProperty('level');
  }
  const calls = kapCalls();
  for (const op of ['commands_describe', 'command_complete', 'command_run']) {
    expect(calls.some(c => c.op === op && c.source === `web:${name}` && c.level === 'console')).toBeTruthy();
  }
});

test('HTTP recent-auth leaves a Users write unapplied until explicit retry, then audits it', async ({ page }) => {
  await signIn(page);
  await page.goto('/users');
  await page.getByRole('button', { name: 'New account', exact: true }).click();
  const dialog = page.getByRole('dialog', { name: 'New account' });
  await dialog.getByLabel('Name', { exact: true }).fill('reader');
  await dialog.getByLabel('Temporary password').fill('TemporaryAccount-42!');
  const refusal = page.waitForResponse(r => r.url().endsWith('/api/users') && r.request().method() === 'POST');
  await dialog.getByRole('button', { name: 'Create', exact: true }).click();
  const denied = await refusal;
  expect(denied.status()).toBe(403);
  expect((await denied.json()).error.code).toBe('reauth_required');
  expect((await jsonCall(page, '/api/users', 'GET')).data.some((a: { name: string }) => a.name === 'reader')).toBe(false);
  await confirmPassword(page);
  expect((await jsonCall(page, '/api/users', 'GET')).data.some((a: { name: string }) => a.name === 'reader')).toBe(false);
  await expect(dialog).toBeVisible();
  await dialog.getByRole('button', { name: 'Create', exact: true }).click();
  await expect(dialog).not.toBeVisible();
  await expect(page.getByRole('row').filter({ hasText: 'reader' })).toBeVisible();
  await page.getByRole('tab', { name: 'Roles', exact: true }).click();
  await page.getByRole('button', { name: 'New role', exact: true }).click();
  const role = page.getByRole('dialog', { name: 'New role' });
  await role.getByLabel('Role id').fill('helper');
  await role.getByLabel('Name', { exact: true }).fill('Test helpers');
  await role.getByLabel('Commands up to', { exact: true }).selectOption('everyone');
  await role.getByRole('button', { name: 'Save', exact: true }).click();
  await expect(role).not.toBeVisible();
  await expect(page.getByRole('row').filter({ hasText: 'Test helpers' })).toBeVisible();
  await page.goto('/audit');
  await expect(page.getByRole('cell', { name: 'users.create', exact: true })).toBeVisible();
  await page.getByLabel('What', { exact: true }).selectOption('users.');
  await expect(page.getByRole('cell', { name: 'users.role_create', exact: true })).toBeVisible();
});

test('real session revoke returns to sign-in and supports a fresh connection', async ({ page }) => {
  await signIn(page);
  await page.goto('/account');
  await expect(page.getByRole('heading', { name: 'Sessions', exact: true })).toBeVisible();
  const row = page.getByRole('row').filter({ hasText: 'this one' });
  await row.getByRole('button', { name: 'Sign out', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Sign in', exact: true })).toBeVisible();
  await signIn(page);
  await page.goto('/commands');
  await expect(page.getByRole('button', { name: '/weather' })).toBeVisible();
});

test('a final 4403 shows Not allowed without crashing the parity page', async ({ page }) => {
  await page.routeWebSocket('**/api/server/kap', ws => { ws.close({ code: 4403, reason: 'fixture policy refusal' }); });
  await signIn(page);
  await page.goto('/commands');
  await expect(page.getByRole('heading', { name: 'Not allowed' })).toBeVisible();
  await expect(page.getByText('fixture policy refusal', { exact: true })).toBeVisible();
});

test('launcher session needs an account credential for HTTP and KAP step-up, never the link secret', async ({ page }) => {
  const { execFileSync } = await import('node:child_process');
  const secret = execFileSync(process.env.KWA_PYTHON ?? 'python3', ['-I', resolve('e2e/kap-mock.py'), 'mint', '--dir', dir], { encoding: 'utf8' }).trim();
  const leaked: string[] = [];
  page.on('request', r => { if (r.url().includes(secret) || (r.headers()['referer'] ?? '').includes(secret)) leaked.push(r.url()); });
  await page.goto(`/launcher#secret=${secret}`);
  await expect(page.getByRole('heading', { name: 'Kilnwarden', exact: true })).toBeVisible();
  expect(new URL(page.url()).hash).toBe('');
  const before = (await jsonCall(page, '/api/auth/me', 'GET')).data;
  expect(before.name).toBe(name);
  expect(before.session.via).toBe('launcher');
  expect(before.session.reauthenticatedUntil).toBeNull();
  expect((await jsonCall(page, '/api/auth/launcher', 'POST', { secret })).status).toBe(401); // single-use
  const action = { id: 'launcher-proof-role', name: 'Launcher proof fixture' };
  const denied = await jsonCall(page, '/api/users/roles', 'POST', action);
  expect(denied.status).toBe(403);
  expect(denied.data.error.code).toBe('reauth_required');
  expect((await jsonCall(page, '/api/auth/reauth', 'POST', { secret })).status).toBe(400);
  await page.goto('/commands');
  await page.getByRole('button', { name: '/weather' }).click();
  await page.getByLabel('kind', { exact: true }).selectOption('storm');
  const dispatched = kapCalls().filter(c => c.op === 'command_run').length;
  await page.getByRole('button', { name: 'Run', exact: true }).click();
  const prompt = page.getByRole('dialog', { name: 'Confirm your identity', exact: true });
  await expect(prompt).toBeVisible();
  await prompt.getByLabel('Your password', { exact: true }).fill(secret);
  const invalidProof = page.waitForResponse('**/api/auth/reauth');
  await prompt.getByRole('button', { name: 'Confirm identity', exact: true }).click();
  expect((await invalidProof).status()).toBe(401);
  expect((await jsonCall(page, '/api/auth/me', 'GET')).data.session.reauthenticatedUntil).toBeNull();
  await confirmPassword(page); // The primary owner's actual local password works for this launcher session.
  const after = (await jsonCall(page, '/api/auth/me', 'GET')).data;
  expect(after.session.handle).toBe(before.session.handle);
  expect(after.session.reauthenticatedUntil).toMatch(/^\d{4}-\d{2}-\d{2}T/);
  expect(kapCalls().filter(c => c.op === 'command_run')).toHaveLength(dispatched);
  expect((await jsonCall(page, '/api/users/roles', 'GET')).data.some((r: { id: string }) => r.id === action.id)).toBe(false);
  await page.getByRole('button', { name: 'Run', exact: true }).click();
  await expect(page.getByTestId('command-output')).toContainText('(mock) ran /weather storm');
  expect(kapCalls().filter(c => c.op === 'command_run')).toHaveLength(dispatched + 1);
  expect((await jsonCall(page, '/api/users/roles', 'POST', action)).status).toBe(201);
  expect(leaked).toEqual([]);
});

test('phone sign-in and command forms fit without horizontal overflow', async ({ page }, info) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/signin');
  await expect(page.getByRole('heading', { name: 'Sign in', exact: true })).toBeVisible();
  await page.screenshot({ path: info.outputPath('phone-signin.png'), fullPage: true });
  await signIn(page);
  await page.goto('/commands');
  await page.getByRole('button', { name: '/weather' }).click();
  await expect(page.getByLabel('kind', { exact: true })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(390);
  await page.screenshot({ path: info.outputPath('phone-commands.png'), fullPage: true });
  await page.getByRole('button', { name: 'Menu', exact: true }).click();
  await expect(page.getByRole('navigation', { name: 'Modules' }).getByRole('link', { name: 'Players', exact: true })).toBeVisible();
});

let readerSecret = '';
let recoveryCodes: string[] = [];
const readerPassword = 'ChangedForTests-73!';
async function totp(secret: string, ahead = 0) {
  const { createHmac } = await import('node:crypto');
  let bits = '';
  for (const c of secret.replace(/\s/g, '')) bits += 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567'.indexOf(c).toString(2).padStart(5, '0');
  const key = Buffer.from((bits.match(/.{8}/g) ?? []).map(b => parseInt(b, 2)));
  const counter = Buffer.alloc(8); counter.writeBigUInt64BE(BigInt(Math.floor(Date.now() / 30_000) + ahead));
  const mac = createHmac('sha1', key).update(counter).digest();
  return ((mac.readUInt32BE(mac[19] & 15) & 0x7fffffff) % 1_000_000).toString().padStart(6, '0');
}
async function jsonCall(page: Page, path: string, method: string, body?: unknown) {
  return page.evaluate(async ({ path, method, body }) => {
    const r = await fetch(path, { method, headers: body === undefined ? {} : { 'Content-Type': 'application/json' }, body: body === undefined ? undefined : JSON.stringify(body) });
    return { status: r.status, data: r.status === 204 ? null : await r.json() };
  }, { path, method, body });
}
test('role-required password and TOTP setup preserves the once-only recovery codes', async ({ page }) => {
  await signIn(page);
  const users = await jsonCall(page, '/api/users', 'GET');
  const reader = users.data.find((a: { name: string }) => a.name === 'reader');
  const proof = await jsonCall(page, '/api/auth/reauth', 'POST', { password });
  expect(proof.status).toBe(200);
  expect((await jsonCall(page, '/api/users/roles/helper', 'PUT', { require2fa: true })).status).toBe(200);
  expect((await jsonCall(page, `/api/users/${reader.id}`, 'PATCH', { roles: ['viewer', 'helper'] })).status).toBe(200);
  await page.goto('/audit');
  await expect(page.getByRole('cell', { name: name, exact: true }).first()).toBeVisible();
  await page.getByRole('button', { name: new RegExp(name) }).click();
  await page.getByRole('menuitem', { name: 'Sign out', exact: true }).click();
  await page.getByLabel('Name', { exact: true }).fill('reader');
  await page.getByLabel('Password', { exact: true }).fill('TemporaryAccount-42!');
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await expect(page.getByText('Choose your own password', { exact: true })).toBeVisible();
  await page.getByLabel('Current password', { exact: true }).fill('TemporaryAccount-42!');
  await page.getByLabel('New password', { exact: true }).fill(readerPassword);
  await page.getByLabel('New password again', { exact: true }).fill(readerPassword);
  await page.getByRole('button', { name: 'Change password', exact: true }).click();
  await page.getByRole('button', { name: 'Set up two-factor', exact: true }).click();
  await page.getByLabel('Your password', { exact: true }).fill(readerPassword);
  const setupResponse = page.waitForResponse('**/api/auth/2fa/setup');
  await page.getByRole('button', { name: 'Continue', exact: true }).click();
  readerSecret = (await (await setupResponse).json()).secret;
  await expect(page.getByRole('img', { name: 'QR code for your authenticator app' })).toBeVisible();
  await page.getByLabel('The 6-digit code the app shows now').fill(await totp(readerSecret));
  await page.getByRole('button', { name: 'Turn on two-factor', exact: true }).click();
  await expect(page.getByText('Save these recovery codes now', { exact: true })).toBeVisible();
  recoveryCodes = await page.getByRole('list', { name: 'Recovery codes' }).locator('code').allTextContents();
  expect(recoveryCodes).toHaveLength(10);
  await page.getByRole('button', { name: 'I saved them', exact: true }).click();
  await expect(page.getByRole('navigation', { name: 'Modules' })).toBeVisible();
  await page.evaluate(() => { history.pushState(null, '', '/audit'); window.dispatchEvent(new PopStateEvent('popstate')); });
  await expect(page.getByRole('heading', { name: 'Audit log', exact: true })).toBeVisible();
  await expect(page.getByRole('cell', { name: name, exact: true })).toHaveCount(0);
});

test('local TOTP sign-in and single-use recovery work through the UI', async ({ page }) => {
  await page.goto('/signin');
  await page.getByLabel('Name', { exact: true }).fill('reader');
  await page.getByLabel('Password', { exact: true }).fill(readerPassword);
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Two-factor sign-in' })).toBeVisible();
  await page.getByLabel('Code from your authenticator app').fill(await totp(readerSecret, 1));
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await expect(page.getByRole('navigation', { name: 'Modules' })).toBeVisible();
  await page.getByRole('button', { name: /reader/ }).click();
  await page.getByRole('menuitem', { name: 'Sign out', exact: true }).click();
  await page.getByLabel('Name', { exact: true }).fill('reader');
  await page.getByLabel('Password', { exact: true }).fill(readerPassword);
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await page.getByRole('button', { name: 'Use a recovery code instead' }).click();
  await page.getByLabel('Recovery code', { exact: true }).fill(recoveryCodes[0]);
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await expect(page.getByRole('navigation', { name: 'Modules' })).toBeVisible();
  await page.goto('/account');
  await expect(page.getByText('9 recovery codes left.', { exact: false })).toBeVisible();
});

test('a final 4401 with expired server status returns to sign-in', async ({ page }) => {
  let expired = false;
  await page.route('**/api/auth/status', async route => {
    if (!expired) return route.continue();
    await route.fulfill({ json: { ownerExists: true, signedIn: false, launcherLogin: true, providers: ['local'] } });
  });
  await page.routeWebSocket('**/api/server/kap', ws => { expired = true; ws.close({ code: 4401, reason: 'fixture expired session' }); });
  await page.goto('/signin');
  await page.getByLabel('Name', { exact: true }).fill(name);
  await page.getByLabel('Password', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await expect(page.getByText('Your sign-in ended. Sign in again.', { exact: true })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Sign in', exact: true })).toBeVisible();
});

test('enrolled TOTP can satisfy a KAP step-up prompt, but recovery and replayed codes cannot', async ({ page }) => {
  await signIn(page);
  await page.goto('/account');
  await page.getByRole('button', { name: 'Set up two-factor', exact: true }).click();
  await page.getByLabel('Your password', { exact: true }).fill(password);
  const setupResponse = page.waitForResponse('**/api/auth/2fa/setup');
  await page.getByRole('button', { name: 'Continue', exact: true }).click();
  const secret = (await (await setupResponse).json()).secret;
  await page.getByLabel('The 6-digit code the app shows now').fill(await totp(secret));
  await page.getByRole('button', { name: 'Turn on two-factor', exact: true }).click();
  await expect(page.getByText('Save these recovery codes now', { exact: true })).toBeVisible();
  const recovery = await page.getByRole('list', { name: 'Recovery codes' }).locator('code').first().innerText();
  await page.getByRole('button', { name: 'I saved them', exact: true }).click();
  await page.goto('/commands');
  expect((await jsonCall(page, '/api/auth/me', 'GET')).data.session.reauthenticatedUntil).toBeNull();
  await page.getByRole('button', { name: '/weather' }).click();
  await page.getByLabel('kind', { exact: true }).selectOption('clear');
  const dispatched = kapCalls().filter(c => c.op === 'command_run').length;
  await page.getByRole('button', { name: 'Run', exact: true }).click();
  const prompt = page.getByRole('dialog', { name: 'Confirm your identity', exact: true });
  await expect(prompt).toBeVisible();
  expect((await jsonCall(page, '/api/auth/reauth', 'POST', { code: recovery })).status).toBe(401);
  expect((await jsonCall(page, '/api/auth/me', 'GET')).data.recoveryCodesLeft).toBe(10);
  await prompt.getByLabel('Verify with').selectOption('code');
  const code = await totp(secret, 1); // A fresh step in the server's accepted TOTP window, not the enrollment step.
  await prompt.getByLabel('Authenticator code', { exact: true }).fill(code);
  await prompt.getByRole('button', { name: 'Confirm identity', exact: true }).click();
  await expect(prompt.getByText('Identity confirmed', { exact: true })).toBeVisible();
  await prompt.getByRole('button', { name: 'Return to the action', exact: true }).click();
  expect(kapCalls().filter(c => c.op === 'command_run')).toHaveLength(dispatched);
  expect((await jsonCall(page, '/api/auth/reauth', 'POST', { code })).status).toBe(401);
  await page.getByRole('button', { name: 'Run', exact: true }).click();
  await expect(page.getByTestId('command-output')).toContainText('(mock) ran /weather clear');
});
