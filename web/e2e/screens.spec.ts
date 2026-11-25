// Release screenshots (README, Nexus): 1920x1080 desktop shots and phone shots of the main pages, with demo data only
// (the mock's invented players and server, accounts and schedules made here). Skipped unless KWA_RELEASE_SCREENS names
// the output folder. KWA_CONSUMER must be the mod id the dev host was built with (the launcher login's consumer).
//   KWA_RELEASE_SCREENS=<dir> KWA_MOCK_SERVER_NAME=Embervale KILN_UI_WORKTREE=<kiln checkout> bunx playwright test e2e/screens.spec.ts
import { test, expect, type Page } from '@playwright/test';
import { execFileSync } from 'node:child_process';
import { mkdirSync } from 'node:fs';
import { resolve } from 'node:path';

const OUT = process.env.KWA_RELEASE_SCREENS;
const dir = process.env.KWA_E2E_DIR!;
const password = 'OnlyDisposableDemo-42!';
const DESKTOP = { width: 1920, height: 1080 };
const PHONE = { width: 390, height: 844 };
/** How long the mock's metrics run before the metrics page is shot (its 10-minute range draws every 2 s sample). */
const WARMUP_MS = Number(process.env.KWA_SCREENS_WARMUP_MS ?? 200_000);

test.skip(!OUT, 'release screenshots only (KWA_RELEASE_SCREENS)');

async function call(page: Page, path: string, method: string, body?: unknown) {
  return page.evaluate(async ({ path, method, body }) => {
    const r = await fetch(path, { method, headers: body === undefined ? {} : { 'Content-Type': 'application/json' }, body: body === undefined ? undefined : JSON.stringify(body) });
    return { status: r.status, data: r.status === 204 ? null : await r.json().catch(() => null) };
  }, { path, method, body });
}

async function ok(page: Page, path: string, method: string, body?: unknown) {
  const r = await call(page, path, method, body);
  expect(r.status, `${method} ${path}: ${JSON.stringify(r.data)}`).toBeLessThan(300);
  return r.data;
}

async function settle(page: Page) {
  await expect(page.locator('.wa-loading')).toHaveCount(0, { timeout: 15_000 }).catch(() => undefined);
  await page.waitForTimeout(700);
}

async function shot(page: Page, file: string) {
  await page.screenshot({ path: resolve(OUT!, `${file}.png`), animations: 'disabled' });
}

test('release screenshots', async ({ browser }) => {
  test.setTimeout(WARMUP_MS + 300_000);
  mkdirSync(OUT!, { recursive: true });
  const ctx = await browser.newContext({ viewport: DESKTOP });
  const page = await ctx.newPage();
  // the owner, through the launcher's one-time login (as "Open web panel" does)
  const secret = execFileSync(process.env.KWA_PYTHON ?? 'python3', ['-I', resolve('e2e/kap-mock.py'), 'mint', '--dir', dir], { encoding: 'utf8' }).trim();
  await page.goto(`/launcher#secret=${secret}`);
  await page.getByLabel('Owner name', { exact: true }).fill('ashwyn');
  await page.getByLabel('Password', { exact: true }).fill(password);
  await page.getByLabel('Password again', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Create the owner account' }).click();
  await expect(page.getByRole('navigation', { name: 'Modules' })).toBeVisible();
  const started = Date.now();

  // demo data: helpers, saved commands, schedules, the public page, a hosted name (never switched on: no relay)
  await ok(page, '/api/auth/reauth', 'POST', { password });
  for (const [name, roles] of [['mara', ['admin']], ['tobin', ['moderator']], ['wren', ['moderator']], ['finch', ['viewer']]] as const)
    await ok(page, '/api/users', 'POST', { name, password: `Disposable-Helper-${roles[0]}-7!`, roles });
  const morning = await ok(page, '/api/saved-commands', 'POST', { name: 'Morning', description: 'Sets the time to half past seven', line: '/time set 07:30', confirm: false, shared: true });
  const rain = await ok(page, '/api/saved-commands', 'POST', { name: 'Clear skies', description: 'Stops the rain', line: '/weather clear', confirm: true, shared: true });
  for (const c of [morning, rain]) await ok(page, `/api/saved-commands/${c.id}/pin`, 'PUT', { pinned: true });
  const zone = 'America/New_York';
  const base = { enabled: true, timeZone: zone, skipWhenEmpty: false, missed: 'skip', catchUpMinutes: 30 };
  await ok(page, '/api/schedules', 'POST', { ...base, name: 'Nightly restart', kind: 'restart', when: { type: 'daily', times: ['04:00'] },
    params: { countdownSeconds: 300, message: 'Nightly restart: find a safe spot!', allowStopOnly: true } });
  await ok(page, '/api/schedules', 'POST', { ...base, name: 'Backup every 3 hours', kind: 'backup', skipWhenEmpty: true, when: { type: 'interval', everyMinutes: 180 }, params: { name: 'auto' } });
  await ok(page, '/api/schedules', 'POST', { ...base, name: 'Rules reminder', kind: 'announce', when: { type: 'daily', times: ['12:00', '20:00'] },
    params: { text: 'Be kind, no griefing, and have fun. Rules: see the server page.' } });
  await ok(page, '/api/schedules', 'POST', { ...base, name: 'Morning on weekends', kind: 'command', when: { type: 'daily', times: ['09:00'], days: ['sat', 'sun'] },
    params: { savedCommandId: morning.id } });
  const pub = (await ok(page, '/api/public/settings', 'GET')).settings;
  await ok(page, '/api/public/settings', 'PUT', { ...pub, enabled: true, showMapDots: true, mapDotsGrid: 250, showPlayerNames: true, showMods: true,
    rules: '# House rules\n\n- Be **kind**\n- No griefing\n- Ask before you build near someone\n', joinInfo: 'Search for *Embervale* in the server list.' });

  // let the mock push a few minutes of metrics (and the players' sessions grow) before anything is shot
  const wait = WARMUP_MS - (Date.now() - started);
  if (wait > 0) await page.waitForTimeout(wait);

  // desktop pages
  const pages: [string, string][] = [['/', 'overview'], ['/servers/this/admin/players', 'players'], ['/activity', 'activity'],
    ['/schedules', 'schedules'], ['/saved-commands', 'saved-commands'], ['/mods', 'mods'], ['/users', 'users'], ['/servers/this/admin/world', 'world']];
  for (const [path, file] of pages) {
    await page.goto(path);
    await settle(page);
    await shot(page, `desktop-${file}`);
  }
  await page.goto('/metrics');
  await settle(page);
  await page.getByRole('tab', { name: '10 min' }).click({ timeout: 5_000 }).catch(() => undefined);
  await settle(page);
  await shot(page, 'desktop-metrics');
  // a command as a form: a player from the picker, an item, a count
  await page.goto('/commands');
  await page.getByRole('button', { name: '/give' }).click();
  await page.getByRole('button', { name: 'Brannoc', exact: true }).click();
  await page.getByPlaceholder('an item name or id').fill('Test Sword T2');
  await page.getByRole('spinbutton').first().fill('3', { timeout: 5_000 }).catch(() => undefined);
  await settle(page);
  await shot(page, 'desktop-commands');
  await page.getByRole('button', { name: '/weather' }).click();
  await page.getByLabel('kind', { exact: true }).selectOption('storm');
  await settle(page);
  await shot(page, 'desktop-commands-weather');
  // hosted access: a name and its claim link (made locally, signed with the panel's relay key; nothing is sent)
  await ok(page, '/api/auth/reauth', 'POST', { password });
  await page.goto('/settings');
  await page.getByRole('tab', { name: 'Hosted access', exact: true }).click();
  await page.getByLabel('Registered name', { exact: true }).fill('embervale.kiln.nexus');
  await page.getByRole('button', { name: 'Save', exact: true }).click();
  await expect(page.getByText('Saved.', { exact: true })).toBeVisible();
  await settle(page);
  await shot(page, 'desktop-hosted-access-top');
  await page.getByRole('button', { name: 'Make a claim link', exact: true }).click();
  await expect(page.getByRole('link', { name: 'Open the claim page', exact: true })).toBeVisible();
  await page.getByText('Claim this name', { exact: true }).first().evaluate((el) => el.scrollIntoView({ block: 'start' }));
  await settle(page);
  await shot(page, 'desktop-hosted-access');
  for (const [tab, file] of [['Sign-in', 'signin'], ['Address and TLS', 'address'], ['Certificate', 'certificate']] as const) {
    await page.getByRole('tab', { name: tab, exact: true }).click();
    await settle(page);
    await shot(page, `desktop-settings-${file}`);
  }
  // the public page
  await page.goto('/api/public/page');
  await page.waitForTimeout(700);
  await shot(page, 'desktop-public-page');

  // phone
  const state = await ctx.storageState();
  const phone = await browser.newContext({ viewport: PHONE, deviceScaleFactor: 3, isMobile: true, hasTouch: true, storageState: state });
  const p = await phone.newPage();
  for (const [path, file] of [['/', 'overview'], ['/servers/this/admin/players', 'players'], ['/schedules', 'schedules'], ['/activity', 'activity'], ['/metrics', 'metrics']] as const) {
    await p.goto(path);
    await settle(p);
    await shot(p, `phone-${file}`);
  }
  await p.goto('/commands');
  await p.getByRole('button', { name: '/weather' }).click();
  await p.getByLabel('kind', { exact: true }).selectOption('storm');
  await p.getByTestId('command-line').evaluate((el) => el.scrollIntoView({ block: 'center' }));
  await settle(p);
  await shot(p, 'phone-commands');
  await p.getByRole('button', { name: 'Menu', exact: true }).click();
  await settle(p);
  await shot(p, 'phone-menu');
  await phone.close();
  await ctx.close();
});
