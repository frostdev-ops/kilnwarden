// Dialogs and menus over the Map page take clicks (0.1.4), at 1280x800 and 390x844. The owner's report on 0.1.3: the
// identity prompt over the map "passes clicks through to the map and I cannot interact with the popup at all". The map's
// Leaflet layers (z-index 400 to 1200) shared the page's stacking context with the dialogs the panel puts on the body
// (z-index 50), so they sat above every dialog: the shell's identity prompt, the admin pages' confirmations, and the
// panel's own menus (the account menu, the phone's page menu) and toasts. Each check spreads clicks over the whole dialog or menu
// (not only its buttons, which can happen to sit beside the map) and then uses it. Runs after panel.spec.ts (the owner
// it makes). Screenshots in KWA_SCREENS (map-dialog-*).
import { test, expect, type Browser, type BrowserContext, type Locator, type Page } from '@playwright/test';
import { execFileSync } from 'node:child_process';
import { mkdirSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { ownerThroughWizard } from './wizard-flow';

const dir = process.env.KWA_E2E_DIR!;
const SHOTS = process.env.KWA_SCREENS ?? resolve('test-results', 'screens');
const DESKTOP = { width: 1280, height: 800 };
const PHONE = { width: 390, height: 844 };
const PASSWORD = 'OnlyDisposableTests-42!';
const LAUNCHER = resolve(dir, 'launcher-admin-assets');
const ART = resolve(dir, 'kiln/data/kilnwarden/map-assets');
const MAP = '/servers/this/admin/map';
// a waypoint of the admin pages' own (they keep them in the browser, per server), away from the players and the zones
const WAYPOINT = { name: 'Dialog test cairn', pos: { x: 7000, y: 60, z: 7000 }, at: 1764000000 };
mkdirSync(SHOTS, { recursive: true });

let state: Awaited<ReturnType<BrowserContext['storageState']>> | null = null;

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
    await ownerThroughWizard(page, 'panel-owner', PASSWORD);
    await expect(home).toBeVisible();
  }
  state = await ctx.storageState();
  await ctx.close();
  return state;
}

async function signedIn(browser: Browser, viewport: { width: number; height: number }) {
  const ctx = await browser.newContext({ viewport, storageState: await ownerState(browser) });
  await ctx.addInitScript((w) => localStorage.setItem('kiln.admin.history.v1.this', JSON.stringify({ waypoints: [w] })), WAYPOINT);
  return { ctx, page: await ctx.newPage() };
}

function fakeArt(root: string) {
  for (const f of ['map/tiles/0/0_0.webp', 'map/gpu/d/0_0.webp', 'data/find.json']) {
    mkdirSync(dirname(resolve(root, f)), { recursive: true });
    writeFileSync(resolve(root, f), f.endsWith('.json') ? '{}' : 'not really a picture');
  }
}

/** Clicks spread over the box (a 5 x 5 grid, 10 px in from its edges): each must reach the box or what it holds. The
 * ones that do not, with what took them (on 0.1.3: Leaflet's canvas and controls). */
function misses(box: Locator) {
  return box.evaluate((el) => {
    const r = el.getBoundingClientRect();
    const name = (n: Element) => n.tagName.toLowerCase() + [...n.classList].slice(0, 2).map((c) => '.' + c).join('');
    const out: string[] = [];
    for (let i = 0; i < 5; i++) {
      for (let j = 0; j < 5; j++) {
        const x = r.left + 10 + ((r.width - 20) * i) / 4, y = r.top + 10 + ((r.height - 20) * j) / 4;
        const hit = document.elementFromPoint(x, y);
        if (!hit || !el.contains(hit)) out.push(`${Math.round(x)},${Math.round(y)}: ${hit ? name(hit) + (hit.parentElement ? ' in ' + name(hit.parentElement) : '') : 'nothing'}`);
      }
    }
    return out;
  });
}

/** How much of the box lies over the map (its share of the box's area), so a check over the map means something. */
async function overMap(page: Page, box: Locator) {
  const [a, m] = [(await box.boundingBox())!, (await page.locator('.ka-map-area').boundingBox())!];
  const w = Math.min(a.x + a.width, m.x + m.width) - Math.max(a.x, m.x), h = Math.min(a.y + a.height, m.y + m.height) - Math.max(a.y, m.y);
  return w > 0 && h > 0 ? (w * h) / (a.width * a.height) : 0;
}

async function openMap(page: Page) {
  await page.goto(MAP);
  await expect(page.getByRole('searchbox', { name: 'Search the map' })).toBeVisible();
  await expect(page.locator('.ka-map.leaflet-container')).toBeVisible();
  await page.waitForTimeout(600);
}

/** The map without its side panel, so it takes the page's whole width (on a phone the side panel sits above it): a
 * dialog in the middle of the screen lies over the map. */
async function wholeMap(page: Page) {
  await page.locator('.ka-map-sidetoggle').click();
  await expect(page.locator('.ka-map-side')).toBeHidden();
  await page.waitForTimeout(300);
}

/** The admin pages' own confirmation on the map: the waypoint's Delete waypoint? (ConfirmHost, on the body). */
async function askDeleteWaypoint(page: Page) {
  await page.locator('.ka-wp', { hasText: WAYPOINT.name }).click({ button: 'right' });
  await page.locator('.ka-map-menu').getByText('Delete waypoint', { exact: true }).click();
  const dialog = page.getByRole('dialog', { name: 'Delete waypoint?', exact: true });
  await expect(dialog).toBeVisible();
  await page.waitForTimeout(400); // its fade in
  return dialog;
}

for (const [label, viewport] of [['desktop', DESKTOP], ['phone', PHONE]] as const) {
  test(`${label}: the identity prompt over the map takes every click`, async ({ browser }) => {
    rmSync(ART, { recursive: true, force: true });
    fakeArt(LAUNCHER);
    const { ctx, page } = await signedIn(browser, viewport);
    // The prompt is the shell's, for every action that needs a recent password. The map art copy no longer asks for one
    // (0.1.4), so a stand-in for a server that does (as 0.1.3's did) answers the copy here.
    await page.route('**/api/core/map-assets/copy', (r) => r.fulfill({
      status: 403, contentType: 'application/json',
      body: JSON.stringify({ error: { code: 'reauth_required', message: 're-authenticate with your password or enrolled two-factor code, then retry' } }),
    }));
    await openMap(page);
    await wholeMap(page);
    await page.getByTestId('map-art-note').getByRole('button', { name: 'Copy it from the Kiln Launcher' }).click();
    const prompt = page.getByRole('dialog', { name: 'Confirm your identity', exact: true });
    await expect(prompt).toBeVisible();
    await page.waitForTimeout(400); // its fade in
    expect(await overMap(page, prompt), 'the share of the prompt over the map').toBeGreaterThan(0.25);
    await page.screenshot({ path: resolve(SHOTS, `map-dialog-${label}-identity.png`) });
    expect.soft(await misses(prompt), 'clicks on the identity prompt that went elsewhere').toEqual([]);
    await prompt.getByLabel('Your password', { exact: true }).click({ timeout: 5000 });
    await page.keyboard.type(PASSWORD);
    await prompt.getByRole('button', { name: 'Confirm identity', exact: true }).click({ timeout: 5000 });
    await expect(prompt.getByText('Identity confirmed', { exact: true })).toBeVisible();
    await prompt.getByRole('button', { name: 'Return to the action', exact: true }).click({ timeout: 5000 });
    await expect(prompt).toHaveCount(0);
    await ctx.close();
    rmSync(LAUNCHER, { recursive: true, force: true });
  });

  test(`${label}: the map's own confirmation takes every click`, async ({ browser }) => {
    rmSync(ART, { recursive: true, force: true });
    rmSync(LAUNCHER, { recursive: true, force: true });
    const { ctx, page } = await signedIn(browser, viewport);
    await openMap(page);
    await wholeMap(page);
    const waypoint = page.locator('.ka-wp', { hasText: WAYPOINT.name });
    const ask = () => askDeleteWaypoint(page);
    let dialog = await ask();
    expect(await overMap(page, dialog), 'the share of the confirmation over the map').toBeGreaterThan(0.25);
    await page.screenshot({ path: resolve(SHOTS, `map-dialog-${label}-confirm.png`) });
    expect.soft(await misses(dialog), "clicks on the map's confirmation that went elsewhere").toEqual([]);
    await dialog.getByRole('button', { name: 'Cancel', exact: true }).click({ timeout: 5000 });
    await expect(dialog).toHaveCount(0);
    await expect(waypoint).toBeVisible();
    dialog = await ask();
    await dialog.getByRole('button', { name: 'Delete', exact: true }).click({ timeout: 5000 });
    await expect(dialog).toHaveCount(0);
    await expect(waypoint).toHaveCount(0);
    await ctx.close();
  });
}

test("desktop: the account menu over the map takes clicks, and the map's own overlays still do", async ({ browser }) => {
  rmSync(LAUNCHER, { recursive: true, force: true });
  fakeArt(ART); // with its art the map fills the page from its top, under the account menu
  const { ctx, page } = await signedIn(browser, DESKTOP);
  await openMap(page);
  await expect(page.getByTestId('map-art-note')).toHaveCount(0);
  await page.locator('.wa-account-btn').click();
  const menu = page.getByRole('menu').filter({ has: page.getByRole('menuitem', { name: 'My account' }) });
  await expect(menu).toBeVisible();
  expect(await overMap(page, menu), 'the share of the account menu over the map').toBeGreaterThan(0.25);
  await page.screenshot({ path: resolve(SHOTS, 'map-dialog-desktop-account-menu.png') });
  expect.soft(await misses(menu), 'clicks on the account menu that went elsewhere').toEqual([]);
  await menu.getByRole('menuitem', { name: 'Sessions', exact: true }).click({ timeout: 5000 });
  await expect(page).toHaveURL(/\/account#sessions$/);
  // the map's own overlays, now inside the map's stacking context, are still above its layers: the search results and
  // the side toggle
  await openMap(page);
  await page.getByRole('searchbox', { name: 'Search the map' }).fill('Ys');
  const results = page.locator('.ka-map-results');
  await expect(results).toBeVisible();
  expect.soft(await misses(results), 'clicks on the search results that went elsewhere').toEqual([]);
  await page.getByRole('searchbox', { name: 'Search the map' }).fill('');
  const side = await page.locator('.ka-map-side').boundingBox();
  await page.locator('.ka-map-sidetoggle').click({ timeout: 5000 });
  await expect(page.locator('.ka-map-side')).toBeHidden();
  await page.locator('.ka-map-sidetoggle').click({ timeout: 5000 });
  expect((await page.locator('.ka-map-side').boundingBox())?.width).toBe(side?.width);
  await ctx.close();
  rmSync(ART, { recursive: true, force: true });
});

test("desktop: a toast over the map takes clicks, and so do the map's menu, route panel and zoom buttons", async ({ browser }) => {
  rmSync(LAUNCHER, { recursive: true, force: true });
  fakeArt(ART);
  const { ctx, page } = await signedIn(browser, DESKTOP);
  await openMap(page);
  await wholeMap(page);
  // the map's own context menu (inside the map's stacking context, above its layers)
  await page.locator('.ka-wp', { hasText: WAYPOINT.name }).click({ button: 'right' });
  const menu = page.locator('.ka-map-menu');
  await expect(menu).toBeVisible();
  expect.soft(await misses(menu), "clicks on the map's menu that went elsewhere").toEqual([]);
  await menu.getByText('Measure from here', { exact: true }).click({ timeout: 5000 });
  // its toast, on the body at the bottom left (z-index 60), partly over the map
  const toast = page.locator('.k-toast', { hasText: 'Start point set' });
  await expect(toast).toBeVisible();
  await page.waitForTimeout(500); // its spring in
  expect(await overMap(page, toast), 'the share of the toast over the map').toBeGreaterThan(0.25);
  await page.screenshot({ path: resolve(SHOTS, 'map-dialog-desktop-toast-route.png') });
  expect.soft(await misses(toast), 'clicks on the toast that went elsewhere').toEqual([]);
  // the route panel and the zoom buttons, the map's own overlays
  const route = page.locator('.ka-map-route');
  await expect(route).toBeVisible();
  expect.soft(await misses(route), 'clicks on the route panel that went elsewhere').toEqual([]);
  expect.soft(await misses(page.locator('.ka-map-zoom')), 'clicks on the zoom buttons that went elsewhere').toEqual([]);
  await route.getByTitle('Clear the route', { exact: true }).click({ timeout: 5000 });
  await expect(route).toHaveCount(0);
  await ctx.close();
  rmSync(ART, { recursive: true, force: true });
});

test('phone: the page menu over the map takes clicks', async ({ browser }) => {
  rmSync(LAUNCHER, { recursive: true, force: true });
  fakeArt(ART); // with its art the map fills the page from its top
  const { ctx, page } = await signedIn(browser, PHONE);
  await openMap(page);
  await wholeMap(page);
  await page.getByRole('button', { name: 'Menu', exact: true }).click();
  const nav = page.getByRole('navigation', { name: 'Modules' });
  await expect(nav).toBeVisible();
  await page.waitForTimeout(400); // it slides in
  expect(await overMap(page, nav), 'the share of the page menu over the map').toBeGreaterThan(0.25);
  await page.screenshot({ path: resolve(SHOTS, 'map-dialog-phone-page-menu.png') });
  expect.soft(await misses(nav), 'clicks on the page menu that went elsewhere').toEqual([]);
  // the menu's last link sits lowest, over the map
  const last = nav.getByRole('link').last();
  const href = await last.getAttribute('href');
  await last.click({ timeout: 5000 });
  await expect(page).toHaveURL((u) => u.pathname === href);
  await ctx.close();
  rmSync(ART, { recursive: true, force: true });
});

test('desktop: a dialog sits above whatever a page holds, whatever its z-index', async ({ browser }) => {
  rmSync(LAUNCHER, { recursive: true, force: true });
  rmSync(ART, { recursive: true, force: true });
  const { ctx, page } = await signedIn(browser, DESKTOP);
  await openMap(page);
  await wholeMap(page);
  const dialog = await askDeleteWaypoint(page);
  // the map root's own isolation undone (as a page without it would be), and a layer of the page's own at z-index
  // 99999 over the whole screen: the page area keeps both below the dialog
  await page.evaluate(() => {
    document.querySelector<HTMLElement>('.ka-map-root')!.style.isolation = 'auto';
    const cover = document.createElement('div');
    cover.dataset.testCover = '';
    Object.assign(cover.style, { position: 'fixed', inset: '0', zIndex: '99999', background: 'rgba(0, 80, 160, .25)' });
    document.querySelector('.wa-main')!.appendChild(cover);
  });
  await page.screenshot({ path: resolve(SHOTS, 'map-dialog-desktop-over-any-page.png') });
  expect.soft(await misses(dialog), 'clicks on the dialog that went elsewhere').toEqual([]);
  await dialog.getByRole('button', { name: 'Cancel', exact: true }).click({ timeout: 5000 });
  await expect(dialog).toHaveCount(0);
  await ctx.close();
});
