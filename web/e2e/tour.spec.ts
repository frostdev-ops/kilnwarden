// Every page and the main flows, at 1280x800 and 390x844, with full-page screenshots in out/complete/screens. Runs after
// panel.spec.ts (it signs in as the owner through the launcher's one-time login, so the owner's two-factor setup from
// that file does not matter); run alone, it creates the owner first.
import { ownerThroughWizard } from './wizard-flow';
import { test, expect, type Browser, type BrowserContext, type Page } from '@playwright/test';
import { execFileSync } from 'node:child_process';
import { mkdirSync } from 'node:fs';
import { resolve } from 'node:path';

const dir = process.env.KWA_E2E_DIR!;
const SHOTS = process.env.KWA_SCREENS ?? resolve('test-results', 'screens');
const password = 'OnlyDisposableTests-42!';
const name = 'panel-owner';
const DESKTOP = { width: 1280, height: 800 };
const PHONE = { width: 390, height: 844 };
mkdirSync(SHOTS, { recursive: true });

/** The web app's own pages: path, h1, screenshot name. */
const WEB_PAGES: [string, string, string][] = [
  ['/activity', 'Player activity', 'activity'], ['/commands', 'Commands', 'commands'], ['/saved-commands', 'Saved commands', 'saved-commands'],
  ['/schedules', 'Schedules', 'schedules'], ['/mods', 'Mods', 'mods'], ['/metrics', 'Metrics', 'metrics'], ['/webhooks', 'Webhooks', 'webhooks'],
  ['/public-page', 'Public page', 'public-page-settings'], ['/users', 'Users and roles', 'users'], ['/audit', 'Audit log', 'audit'],
  ['/settings', 'Panel settings', 'panel-settings'], ['/account', 'My account', 'account'],
];
const SETTINGS_TABS: [string, string][] = [['Address and TLS', 'address'], ['Certificate', 'certificate'], ['Sign-in', 'signin'], ['Hosted access', 'relay'],
  ['Branding', 'branding'], ['Modules', 'modules'], ['Map art', 'map-art']];
/** Console errors that are the dev host's (it has no map art: the map's tiles answer 404). Since @kiln/admin-ui
 * 0.2.0-dev.31.13 (kiln 01a6ca7f) the map's health bars need no inline style, so no page may log a CSP violation. */
const EXPECTED = [/status of 404 .* @ .*\/api\/core\/map-assets\//];
/** ...and the flows' own doing: the refusals they provoke (a recent proof, a refused settings file, a failed read). */
const PROVOKED = [...EXPECTED, /status of 403 .* @ .*\/api\/core\/settings\/files\/branding/, /status of 422 .* @ .*\/api\/core\/settings\/files\/acme/,
  /status of 500 .* @ .*\/api\/activity\/players/, /status of 409 .* @ .*\/api\/core\/relay\/move\/undo/];

let state: Awaited<ReturnType<BrowserContext['storageState']>> | null = null;

function errorsOf(page: Page) {
  const errors: string[] = [];
  page.on('pageerror', (e) => errors.push(`pageerror: ${e.message}`));
  page.on('console', (m) => { if (m.type() === 'error') errors.push(`${m.text()} @ ${m.location().url}`); });
  return errors;
}

async function jsonCall(page: Page, path: string, method: string, body?: unknown) {
  return page.evaluate(async ({ path, method, body }) => {
    const r = await fetch(path, { method, headers: body === undefined ? {} : { 'Content-Type': 'application/json' }, body: body === undefined ? undefined : JSON.stringify(body) });
    return { status: r.status, data: r.status === 204 ? null : await r.json().catch(() => null) };
  }, { path, method, body });
}

/** Signs in as the owner (the launcher's one-time login; creates the owner when there is none) and keeps the cookies. */
async function ownerState(browser: Browser) {
  if (state) return state;
  const ctx = await browser.newContext({ viewport: DESKTOP });
  const page = await ctx.newPage();
  const secret = execFileSync(process.env.KWA_PYTHON ?? 'python3', ['-I', resolve('e2e/kap-mock.py'), 'mint', '--dir', dir], { encoding: 'utf8' }).trim();
  await page.goto(`/launcher#secret=${secret}`);
  const setup = page.getByRole('heading', { name: 'Set up your panel' });
  const home = page.getByRole('heading', { level: 1, name: 'Kilnwarden', exact: true });
  await expect(setup.or(home)).toBeVisible();
  if (await setup.isVisible()) {
    await ownerThroughWizard(page, name, password);
    await expect(home).toBeVisible();
  }
  state = await ctx.storageState();
  await ctx.close();
  return state;
}

async function settle(page: Page) {
  await expect(page.locator('.wa-loading')).toHaveCount(0, { timeout: 15_000 }).catch(() => undefined);
  await page.waitForTimeout(400);
}

/** A full-page screenshot: the shell scrolls inside its main area, so it is let out to the page's height first. */
async function shot(page: Page, file: string, opts: { full?: boolean } = {}) {
  const full = opts.full ?? true;
  if (full) {
    await page.evaluate(() => {
      const set = (sel: string, p: Record<string, string>) => document.querySelectorAll<HTMLElement>(sel).forEach((el) => { for (const [k, v] of Object.entries(p)) el.style.setProperty(k, v); });
      set('body', { overflow: 'visible' });
      set('#root', { height: 'auto' });
      set('.wa-shell', { height: 'auto', 'min-height': '100dvh' });
      set('.wa-main', { overflow: 'visible' });
    });
  }
  await page.screenshot({ path: resolve(SHOTS, `${file}.png`), fullPage: full, animations: 'disabled' });
  if (full) {
    await page.evaluate(() => {
      for (const sel of ['body', '#root', '.wa-shell', '.wa-main'])
        document.querySelectorAll<HTMLElement>(sel).forEach((el) => { for (const k of ['overflow', 'height', 'min-height']) el.style.removeProperty(k); });
    });
  }
}

/** Nothing wider than the viewport: the document, and the shell's scrolling main area. */
async function noOverflow(page: Page, where: string) {
  const o = await page.evaluate(() => {
    const main = document.querySelector<HTMLElement>('.wa-main');
    const wide = [...document.querySelectorAll<HTMLElement>('.wa-main *')].filter((el) => el.getBoundingClientRect().right > innerWidth + 1 && getComputedStyle(el).position !== 'fixed')
      .filter((el) => !el.closest('.wa-table-wrap, .k-table-scroll, .wa-pre, pre, .leaflet-container, [data-scroll]'))
      .slice(0, 3).map((el) => `${el.tagName.toLowerCase()}.${[...el.classList].join('.')}`);
    return { doc: document.documentElement.scrollWidth, main: main ? main.scrollWidth - main.clientWidth : 0, width: innerWidth, wide };
  });
  expect.soft(o.doc, `${where}: the page is wider than the screen`).toBeLessThanOrEqual(o.width);
  expect.soft(o.main, `${where}: the main area scrolls sideways (${o.wide.join(', ')})`).toBeLessThanOrEqual(1);
}

function unexpected(errors: string[], allowed = EXPECTED) {
  return errors.filter((e) => !allowed.some((r) => r.test(e)));
}

async function signedIn(browser: Browser, viewport: { width: number; height: number }) {
  const ctx = await browser.newContext({ viewport, storageState: await ownerState(browser) });
  const page = await ctx.newPage();
  return { ctx, page, errors: errorsOf(page) };
}

/** Every page of the navigation (the admin pages too), from the module list. */
async function navPaths(page: Page): Promise<string[]> {
  const nav = page.getByRole('navigation', { name: 'Modules' });
  await expect(nav.getByRole('link', { name: 'Player activity', exact: true })).toBeVisible();
  const hrefs = await nav.getByRole('link').evaluateAll((links) => links.map((a) => a.getAttribute('href') ?? ''));
  return [...new Set(hrefs.filter((h) => h.startsWith('/')))];
}

const slug = (path: string) => path === '/' ? 'overview' : path.replace(/^\/servers\/this\/(admin\/)?/, 'admin-').replace(/^\//, '').replace(/[^a-z0-9]+/gi, '-');

for (const [label, viewport] of [['desktop', DESKTOP], ['phone', PHONE]] as const) {
  test(`${label}: every page loads with its heading, no console errors${label === 'phone' ? ', no sideways scrolling' : ''}`, async ({ browser }) => {
    test.setTimeout(420_000);
    const { ctx, page, errors } = await signedIn(browser, viewport);
    await page.goto('/');
    await expect(page.getByRole('heading', { level: 1, name: 'Kilnwarden', exact: true })).toBeVisible();
    if (label === 'phone') await page.getByRole('button', { name: 'Menu', exact: true }).click();
    const paths = await navPaths(page);
    if (label === 'phone') await page.getByRole('button', { name: 'Menu', exact: true }).click();
    // the navigation has no placeholders left
    await expect(page.getByText(/coming soon|step 2/i)).toHaveCount(0);
    const known = new Map(WEB_PAGES.map(([p, h, f]) => [p, [h, f] as const]));
    for (const path of ['/', ...paths]) {
      const before = errors.length;
      await page.goto(path);
      const [heading, file] = path === '/' ? ['Kilnwarden', 'overview'] : known.get(path) ?? [null, slug(path)];
      if (heading) await expect(page.getByRole('heading', { level: 1, name: heading, exact: true }), path).toBeVisible();
      else if (path.endsWith('/map')) await expect(page.getByRole('searchbox', { name: 'Search the map' }), path).toBeVisible();
      else {
        // the launcher's admin pages (vendored) render into the main area
        await expect(page.getByRole('main').locator('h1, h2, h3, input, table, [role=status]').first(), path).toBeVisible();
        await expect(page.getByRole('heading', { name: 'Not found' }), path).toHaveCount(0);
      }
      // every vendored page has exactly one page heading; the Items and Map ones are visually hidden (k-sr-only), so
      // they are counted rather than checked for visibility
      if (!heading) await expect(page.getByRole('main').locator('h1'), path).toHaveCount(1);
      await expect(page.getByText(/coming soon/i), path).toHaveCount(0);
      await settle(page);
      if (label === 'phone') await noOverflow(page, path);
      await shot(page, `${label}-${file}`, { full: !path.endsWith('/map') });
      expect.soft(unexpected(errors.slice(before)), `console errors on ${path}`).toEqual([]);
    }
    errors.length = 0; // checked page by page above
    // every web page is in the navigation
    for (const [p] of WEB_PAGES.filter(([p]) => p !== '/account')) expect(paths, p).toContain(p);
    // the panel settings' sections
    await page.goto('/settings');
    for (const [tab, file] of SETTINGS_TABS) {
      await page.getByRole('tab', { name: tab, exact: true }).click();
      await expect(page.getByRole('tabpanel')).toBeVisible();
      await settle(page);
      if (label === 'phone') await noOverflow(page, `/settings ${tab}`);
      await shot(page, `${label}-panel-settings-${file}`);
    }
    // an address the app does not have, and an extension page that is not there (the dev host has no extension)
    await page.goto('/no-such-page');
    await expect(page.getByRole('heading', { level: 1, name: 'Not found' })).toBeVisible();
    await shot(page, `${label}-not-found`);
    await page.goto('/m/no-such-mod/page');
    await expect(page.getByRole('heading', { level: 1, name: 'Not found' })).toBeVisible();
    await shot(page, `${label}-extension-missing`);
    expect(unexpected(errors)).toEqual([]);
    await ctx.close();
  });

  test(`${label}: sign-in, two-factor, setup and launcher pages`, async ({ browser }) => {
    test.setTimeout(120_000);
    const ctx = await browser.newContext({ viewport });
    const page = await ctx.newPage();
    const errors = errorsOf(page);
    await page.goto('/signin');
    await expect(page.getByRole('heading', { name: 'Sign in', exact: true })).toBeVisible();
    if (label === 'phone') await noOverflow(page, '/signin');
    await shot(page, `${label}-signin`, { full: false });
    await page.getByLabel('Name', { exact: true }).fill(name);
    await page.getByLabel('Password', { exact: true }).fill(password);
    await page.getByRole('button', { name: 'Sign in', exact: true }).click();
    const twoFactor = page.getByRole('heading', { name: 'Two-factor sign-in' });
    await expect(twoFactor.or(page.getByRole('heading', { level: 1, name: 'Kilnwarden', exact: true }))).toBeVisible();
    if (await twoFactor.isVisible()) {
      await shot(page, `${label}-signin-two-factor`, { full: false });
      await page.getByRole('button', { name: 'Start over' }).click();
      await expect(page.getByRole('heading', { name: 'Sign in', exact: true })).toBeVisible();
    } else await ctx.clearCookies();
    await page.goto('/launcher');
    await expect(page.getByText("The launcher's sign-in did not work")).toBeVisible();
    await shot(page, `${label}-launcher-error`, { full: false });
    // the setup page as a new server shows it (the status answer says there is no owner yet; nothing is sent)
    await page.route('**/api/auth/status', (r) => r.fulfill({ json: { ownerExists: false, signedIn: false, launcherLogin: true, providers: ['local'] } }));
    await page.goto('/setup#token=only-a-picture');
    await expect(page.getByRole('heading', { name: 'Set up your panel' })).toBeVisible();
    if (label === 'phone') await noOverflow(page, '/setup');
    await shot(page, `${label}-setup`, { full: false });
    expect(unexpected(errors)).toEqual([]);
    await ctx.close();
  });
}

test('flows: dialog, filter, empty and error states, the re-auth prompt, settings refusals, the overview layout', async ({ browser }) => {
  test.setTimeout(300_000);
  const { ctx, page, errors } = await signedIn(browser, DESKTOP);
  page.setDefaultTimeout(20_000); // a step that cannot happen fails in seconds, not at the test's end
  // a dialog: focus goes in, Escape closes it
  await page.goto('/schedules');
  await page.getByRole('button', { name: 'New schedule' }).first().click();
  const dialog = page.getByRole('dialog', { name: 'New schedule' });
  await expect(dialog).toBeVisible();
  await expect(dialog.getByLabel('Name', { exact: true })).toBeFocused();
  await shot(page, 'desktop-dialog-new-schedule', { full: false });
  await page.keyboard.press('Escape');
  await expect(dialog).toBeHidden();
  // empty states
  await page.goto('/saved-commands');
  await expect(page.getByText('Nothing saved yet')).toBeVisible();
  await page.goto('/webhooks');
  await expect(page.getByText('No webhooks yet')).toBeVisible();
  await page.getByRole('button', { name: 'Add webhook' }).first().click();
  const hook = page.getByRole('dialog', { name: 'Add a webhook' });
  await hook.getByRole('checkbox', { name: /A player died/ }).check();
  await expect(hook.getByText('{{killer}}')).toBeVisible();
  await expect(hook.getByRole('group', { name: 'Players' })).toBeVisible();
  await shot(page, 'desktop-dialog-webhook', { full: false });
  await page.keyboard.press('Escape');
  // a filter with nothing in it, then the activity log's error state (the server is made to fail once)
  await page.goto('/activity');
  await expect(page.getByRole('heading', { level: 1, name: 'Player activity' })).toBeVisible();
  await expect(page.getByRole('list', { name: 'Players online' })).toBeVisible();
  await page.getByLabel('Show', { exact: true }).selectOption('death');
  await expect(page.getByText('Nothing matches this filter')).toBeVisible();
  await shot(page, 'desktop-activity-filter-empty');
  await page.route('**/api/activity/players', (r) => r.fulfill({ status: 500, json: { error: { code: 'internal', message: 'the activity store is busy' } } }));
  await page.reload();
  await expect(page.getByText('Playtime did not load')).toBeVisible();
  await expect(page.getByText('The activity store is busy.')).toBeVisible();
  await shot(page, 'desktop-activity-error');
  await page.unroute('**/api/activity/players');
  await page.getByRole('button', { name: 'Try again' }).click();
  await expect(page.getByText('Playtime did not load')).toHaveCount(0);
  // the re-auth prompt: saving the branding needs a recent proof; nothing is saved until the explicit second try
  await page.goto('/settings#branding');
  await page.getByLabel('Server name', { exact: true }).fill('Tour Test Server');
  const refusal = page.waitForResponse((r) => r.url().endsWith('/api/core/settings/files/branding') && r.request().method() === 'PUT');
  await page.getByRole('button', { name: 'Save', exact: true }).click();
  expect((await refusal).status()).toBe(403);
  const prompt = page.getByRole('dialog', { name: 'Confirm your identity', exact: true });
  await expect(prompt).toBeVisible();
  await expect(prompt.getByLabel('Your password', { exact: true })).toBeFocused();
  await shot(page, 'desktop-reauth-prompt', { full: false });
  await prompt.getByLabel('Your password', { exact: true }).fill(password);
  await prompt.getByRole('button', { name: 'Confirm identity', exact: true }).click();
  await prompt.getByRole('button', { name: 'Return to the action', exact: true }).click();
  await expect(page.getByText('Nothing was saved.')).toBeVisible();
  expect((await jsonCall(page, '/api/core/branding', 'GET')).data.name).not.toBe('Tour Test Server');
  await page.getByRole('button', { name: 'Save', exact: true }).click();
  await expect(page.getByText('Saved.', { exact: true })).toBeVisible();
  await expect(page.locator('.wa-brand-name')).toHaveText('Tour Test Server');
  await page.getByLabel('Server name', { exact: true }).fill('');
  await page.getByRole('button', { name: 'Save', exact: true }).click();
  await expect(page.locator('.wa-brand-name')).toHaveText('Kilnwarden');
  // a settings file the server refuses: its problems as a list
  await page.getByRole('tab', { name: 'Certificate', exact: true }).click();
  await page.getByLabel('Contact email (optional)').fill('not-an-address');
  await page.getByRole('button', { name: 'Save', exact: true }).click();
  await expect(page.getByText('Not saved: fix these first.')).toBeVisible();
  await expect(page.getByText('Email is not an address.')).toBeVisible();
  await shot(page, 'desktop-panel-settings-refused');
  await page.getByRole('button', { name: 'Discard changes' }).click();
  // the challenge notes
  await page.getByLabel('Challenge', { exact: true }).selectOption('tls-alpn-01');
  await expect(page.getByText(/connects to port 443, so the panel must listen on 443/)).toBeVisible();
  await page.getByLabel('Challenge', { exact: true }).selectOption('dns-01');
  await expect(page.getByText(/Needs hosted access \(the relay\)/)).toBeVisible();
  await page.getByRole('button', { name: 'Discard changes' }).click();
  // a public address warns before anything is saved
  await page.getByRole('tab', { name: 'Address and TLS', exact: true }).click();
  await page.getByLabel('Listen on').selectOption('0.0.0.0');
  await expect(page.getByText('TLS is off on a public address')).toBeVisible();
  await shot(page, 'desktop-panel-settings-public-warning');
  await page.getByRole('button', { name: 'Discard changes' }).click();
  // sign-in providers: the role mapping editors (nothing saved)
  await page.getByRole('tab', { name: 'Sign-in', exact: true }).click();
  // a switch's input is hidden behind its track: its label is what a person clicks
  await page.getByText('Sign in with Steam', { exact: true }).click();
  await page.getByText('Sign in with Discord', { exact: true }).click();
  await expect(page.getByRole('switch', { name: /Sign in with Discord/ })).toBeChecked();
  await page.getByRole('group', { name: 'Panel roles from Discord roles' }).getByRole('button', { name: 'Add a line' }).click();
  await expect(page.getByLabel('Discord role id')).toBeVisible();
  await shot(page, 'desktop-panel-settings-role-mapping');
  await page.getByRole('button', { name: 'Discard changes' }).click();
  // hosted access: Claim this name (lane L1). The name first, then a one-time link to the relay's Discord claim page,
  // signed with the panel's relay key (the recent proof from the branding save still holds)
  await page.getByRole('tab', { name: 'Hosted access', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Make a claim link', exact: true })).toBeVisible();
  // owner decision 2026-11-25: word for word, at the top of the tab
  await expect(page.getByTestId('kiln-nexus-note')).toHaveText("kiln.nexus is frostdev's own domain for Kiln. It has no affiliation with Nexus Mods (nexusmods.com).");
  await expect(page.getByTestId('kiln-nexus-note')).toBeInViewport();
  await page.getByLabel('Registered name', { exact: true }).fill('tour.kiln.nexus');
  await page.getByRole('button', { name: 'Save', exact: true }).click();
  await expect(page.getByText('Saved.', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Make a claim link', exact: true }).click();
  const claim = page.getByRole('link', { name: 'Open the claim page', exact: true });
  // 0.1.1 (lane L1b): the claim pages' own origin, claim.<the name's zone>
  await expect(claim).toHaveAttribute('href', /^https:\/\/claim\.kiln\.nexus\/claim\?v=1&host=tour\.kiln\.nexus&key=[A-Za-z0-9_-]+&time=\d+&nonce=[A-Za-z0-9_-]{43}&sig=[A-Za-z0-9_-]+$/);
  await expect(claim).toHaveAttribute('target', '_blank');
  await expect(claim).toHaveAttribute('rel', 'noopener noreferrer');
  await expect(page.getByText(/whoever opens it first can claim/)).toBeVisible();
  await expect(page.getByText('Hosted access is off: switch it on and save, or the claimed name lapses an hour after the claim.')).toBeVisible();
  const fp = (await jsonCall(page, '/api/core/relay', 'GET')).data.fingerprint as string;
  await expect(page.getByText(fp.slice(0, 32).match(/.{4}/g)!.join(' '), { exact: true })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Your panel name', exact: true })).toHaveAttribute('href', 'https://claim.kiln.nexus/');
  await shot(page, 'desktop-panel-settings-claim-link');
  // lane L1b: the relay tells the panel its name is being moved (a 24-hour wait asked with the Discord account): the banner
  // on every page, with Undo for the owner. The dev host has no relay, so its status is given the notice here; the undo
  // first goes to the real panel (hosted access is off: nothing to undo), then to a stand-in for the relay's answer.
  const at = new Date(Date.now() + 24 * 3600_000).toISOString();
  await page.route('**/api/core/relay', async (route) => {
    const res = await route.fetch();
    route.fulfill({ response: res, json: { ...(await res.json()), enabled: true, scheduled: { to: 'ab'.repeat(32), requested: new Date().toISOString(), at } } });
  });
  await page.goto('/');
  const banner = page.getByRole('status').filter({ hasText: "This panel's name tour.kiln.nexus is being moved to another server on" });
  await expect(banner).toBeVisible();
  await expect(banner.getByText('Not you?')).toBeVisible();
  await expect(banner.getByText(/secure your Discord account/)).toBeVisible();
  await expect(banner.getByRole('link', { name: 'claim.kiln.nexus', exact: true })).toHaveAttribute('href', 'https://claim.kiln.nexus/');
  await shot(page, 'desktop-move-banner');
  await banner.getByRole('button', { name: 'Undo', exact: true }).click();
  await expect(banner.getByText('Hosted access is off: there is nothing to undo.')).toBeVisible();
  await page.route('**/api/core/relay/move/undo', (route) => route.fulfill({ json: { undone: true, host: 'tour.kiln.nexus' } }));
  await banner.getByRole('button', { name: 'Undo', exact: true }).click();
  await expect(page.getByText('Undone: tour.kiln.nexus stays with this server.')).toBeVisible();
  await shot(page, 'desktop-move-banner-undone');
  await page.unroute('**/api/core/relay/move/undo');
  await page.unroute('**/api/core/relay');
  await page.goto('/settings#relay');
  await page.getByLabel('Registered name', { exact: true }).fill('');
  await page.getByRole('button', { name: 'Save', exact: true }).click();
  await expect(page.getByText('Saved.', { exact: true })).toBeVisible();
  // command forms: a duration from a number and a unit, a time of day from hour:minute, x and z from the world plane
  await page.goto('/commands');
  await page.getByRole('button', { name: '/time' }).click();
  await page.getByRole('tab', { name: 'add', exact: true }).click();
  await page.getByLabel('duration', { exact: true }).fill('5');
  await page.getByLabel('Unit', { exact: true }).selectOption('m');
  await expect(page.getByTestId('command-line')).toHaveText('/time add 5m');
  await page.getByRole('tab', { name: 'set', exact: true }).click();
  await page.getByLabel('time', { exact: true }).fill('07:30');
  await expect(page.getByTestId('command-line')).toHaveText('/time set 07:30');
  await page.getByRole('button', { name: '/zone' }).click();
  await page.getByRole('tab', { name: 'create', exact: true }).click();
  await page.getByRole('button', { name: 'Pick on the map' }).click();
  const plane = page.locator('.wa-plane');
  const box = (await plane.boundingBox())!;
  await page.mouse.click(box.x + box.width * 0.25, box.y + box.height * 0.25);
  await expect(page.getByLabel('x', { exact: true })).toHaveValue('2560');
  await expect(page.getByLabel('z', { exact: true })).toHaveValue('7680');
  await shot(page, 'desktop-commands-pickers');
  // the overview: customize (0.1.3: drag and drop; here the keyboard and the switches), saved per account, back to the default
  // (dashboard.spec.ts drags with a mouse and a finger too)
  await page.goto('/');
  await page.getByRole('button', { name: 'Customize' }).click();
  await page.getByRole('button', { name: 'Move Quick links' }).focus();
  await page.keyboard.press('Space');
  await page.keyboard.press('ArrowUp');
  await page.keyboard.press('Space');
  await page.locator('label.k-toggle', { has: page.getByRole('switch', { name: 'Show Panel', exact: true }) }).click();
  await expect(page.getByRole('switch', { name: 'Show Panel' })).not.toBeChecked();
  await shot(page, 'desktop-overview-customize');
  await page.getByRole('button', { name: 'Save layout' }).click();
  await expect(page.getByText(/Your overview is saved/)).toBeVisible();
  expect((await jsonCall(page, '/api/core/dashboard', 'GET')).data).toMatchObject({ hidden: ['panel/info'] });
  await page.reload();
  await expect(page.locator('.wa-dash-item .k-card-title').first()).toHaveText('Quick links');
  await expect(page.locator('.k-card-title', { hasText: /^Panel$/ })).toHaveCount(0);
  await page.getByRole('button', { name: 'Customize' }).click();
  await page.getByRole('button', { name: 'Use the default' }).click();
  await page.getByRole('button', { name: 'Save layout' }).click();
  await expect(page.locator('.wa-dash-item .k-card-title').first()).toHaveText('Server');
  // the public page, on, with map dots and markdown rules (seeded through the API with a recent proof)
  expect((await jsonCall(page, '/api/auth/reauth', 'POST', { password })).status).toBe(200);
  const pub = (await jsonCall(page, '/api/public/settings', 'GET')).data.settings;
  expect((await jsonCall(page, '/api/public/settings', 'PUT', {
    ...pub, enabled: true, showMapDots: true, mapDotsGrid: 250, showPlayerNames: true, showMods: true,
    rules: '# House rules\n\n- Be **kind**\n- No griefing\n\nSee [the wiki](https://example.com/wiki).', joinInfo: 'Search for *Tour Test* in the server list.',
  })).status).toBe(200);
  await page.goto('/public-page');
  await expect(page.getByText('Dots show roughly where players are, to anyone')).toBeVisible();
  await settle(page);
  await shot(page, 'desktop-public-page-settings-dots');
  for (const [label, viewport] of [['desktop', DESKTOP], ['phone', PHONE]] as const) {
    await page.setViewportSize(viewport);
    await page.goto('/api/public/page');
    await expect(page.getByText('House rules')).toBeVisible();
    await page.waitForTimeout(500);
    await page.screenshot({ path: resolve(SHOTS, `${label}-public-page.png`), fullPage: true, animations: 'disabled' });
    if (label === 'phone') expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(PHONE.width);
  }
  await page.setViewportSize(DESKTOP);
  await page.goto('/public-page'); // the public page's own CSP allows no requests from it
  expect((await jsonCall(page, '/api/public/settings', 'PUT', { ...pub, enabled: false })).status).toBe(200);
  // phone: the dialog and the drawer
  await page.setViewportSize(PHONE);
  await page.goto('/schedules');
  await page.getByRole('button', { name: 'New schedule' }).first().click();
  await expect(page.getByRole('dialog', { name: 'New schedule' })).toBeVisible();
  await shot(page, 'phone-dialog-new-schedule', { full: false });
  await page.keyboard.press('Escape');
  await page.getByRole('button', { name: 'Menu', exact: true }).click();
  await expect(page.getByRole('navigation', { name: 'Modules' }).getByRole('link', { name: 'Panel settings', exact: true })).toBeVisible();
  await shot(page, 'phone-menu', { full: false });
  expect(unexpected(errors, PROVOKED)).toEqual([]);
  await ctx.close();
});
