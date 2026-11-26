// The Map page in the panel (0.1.3), at 1280x800 and 390x844: the map fills the height the panel has, the same on the
// side panel's three tabs (Markers, Find & filter, Live), the side panel scrolls on its own, the side toggle and the
// search results; and the map art: without it the page says so with the one action that fixes it (copy the Kiln
// Launcher's art from this computer, the dev host's --launcher-assets stand-in), or the steps behind Details. It never
// says "Preparing map art…". Runs after panel.spec.ts (the owner it makes). Screenshots in KWA_SCREENS (map-*).
import { test, expect, type Browser, type BrowserContext, type Page } from '@playwright/test';
import { execFileSync } from 'node:child_process';
import { mkdirSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { ownerThroughWizard } from './wizard-flow';

const dir = process.env.KWA_E2E_DIR!;
const SHOTS = process.env.KWA_SCREENS ?? resolve('test-results', 'screens');
const DESKTOP = { width: 1280, height: 800 };
const PHONE = { width: 390, height: 844 };
const LAUNCHER = resolve(dir, 'launcher-admin-assets');
const ART = resolve(dir, 'kiln/data/kilnwarden/map-assets');
mkdirSync(SHOTS, { recursive: true });
test.describe.configure({ mode: 'serial' });

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
    await ownerThroughWizard(page, 'panel-owner', 'OnlyDisposableTests-42!');
    await expect(home).toBeVisible();
  }
  state = await ctx.storageState();
  await ctx.close();
  return state;
}

async function signedIn(browser: Browser, viewport: { width: number; height: number }) {
  const ctx = await browser.newContext({ viewport, storageState: await ownerState(browser) });
  return { ctx, page: await ctx.newPage() };
}

/** The map's box, its side panel's and the scrolling panel inside it. */
const boxes = (page: Page) => page.evaluate(() => {
  const r = (sel: string) => { const b = document.querySelector(sel)?.getBoundingClientRect(); return b ? { top: Math.round(b.top), height: Math.round(b.height), width: Math.round(b.width), bottom: Math.round(b.bottom) } : null; };
  const panel = document.querySelector<HTMLElement>('.ka-map-panel');
  return { root: r('.ka-map-root'), area: r('.ka-map-area'), side: r('.ka-map-side'), main: r('.wa-main'), panelScrolls: !!panel && getComputedStyle(panel).overflowY === 'auto', vh: innerHeight };
});

function fakeArt(root: string) {
  for (const f of ['map/tiles/0/0_0.webp', 'map/gpu/d/0_0.webp', 'data/find.json']) {
    mkdirSync(dirname(resolve(root, f)), { recursive: true });
    writeFileSync(resolve(root, f), f.endsWith('.json') ? '{}' : 'not really a picture');
  }
}

for (const [label, viewport] of [['desktop', DESKTOP], ['phone', PHONE]] as const) {
  test(`${label}: the map keeps its height on every tab of its side panel`, async ({ browser }) => {
    rmSync(LAUNCHER, { recursive: true, force: true });
    const { ctx, page } = await signedIn(browser, viewport);
    await page.goto('/servers/this/admin/map');
    await expect(page.getByRole('searchbox', { name: 'Search the map' })).toBeVisible();
    await expect(page.getByTestId('map-art-note')).toBeVisible();
    await page.waitForTimeout(600);
    const heights: number[] = [];
    for (const tab of ['Markers', 'Find & filter', 'Live']) {
      await page.locator('.ka-map-tabs').getByText(tab, { exact: true }).click();
      await page.waitForTimeout(300);
      const b = await boxes(page);
      heights.push(b.root!.height);
      // the map reaches the bottom of the screen (less the page's margin), whatever the tab holds
      expect(b.root!.bottom, `${tab}: the map's bottom`).toBeGreaterThan(b.vh - 30);
      expect(b.root!.bottom, `${tab}: the map's bottom`).toBeLessThanOrEqual(b.vh);
      expect(b.panelScrolls, `${tab}: the side panel scrolls on its own`).toBe(true);
      // the map itself is never squeezed to a sliver beside the side panel
      expect(b.area!.width, `${tab}: the map's width`).toBeGreaterThan(viewport.width / 3);
      expect(b.area!.height, `${tab}: the map's height`).toBeGreaterThan(200);
      await page.screenshot({ path: resolve(SHOTS, `map-${label}-${tab.toLowerCase().replace(/[^a-z]+/g, '-')}.png`) });
    }
    expect(new Set(heights).size, `the map's height on the three tabs: ${heights.join(', ')}`).toBe(1);
    // the side toggle hides the panel: the map takes its room, same height
    await page.locator('.ka-map-sidetoggle').click();
    const hidden = await boxes(page);
    expect(hidden.root!.height).toBe(heights[0]);
    expect(hidden.area!.width).toBeGreaterThan(hidden.root!.width - 4);
    await page.screenshot({ path: resolve(SHOTS, `map-${label}-side-hidden.png`) });
    await page.locator('.ka-map-sidetoggle').click();
    // the search results stay on screen
    await page.getByRole('searchbox', { name: 'Search the map' }).fill('Ys');
    const results = page.locator('.ka-map-results');
    await expect(results).toBeVisible();
    const rb = (await results.boundingBox())!;
    expect(rb.x + rb.width).toBeLessThanOrEqual(viewport.width);
    expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(viewport.width);
    await page.screenshot({ path: resolve(SHOTS, `map-${label}-search.png`) });
    await ctx.close();
  });
}

test('without map art: the plain notice, the steps behind Details, never "Preparing"', async ({ browser }) => {
  rmSync(LAUNCHER, { recursive: true, force: true });
  rmSync(ART, { recursive: true, force: true });
  const { ctx, page } = await signedIn(browser, DESKTOP);
  await page.goto('/servers/this/admin/map');
  const note = page.getByTestId('map-art-note');
  await expect(note).toContainText('This server has no map art yet');
  await expect(page.getByText('Preparing map art…')).toHaveCount(0);
  // the admin pages' own banner over the map would say it twice: the panel hides it (app.css, .wa-map-noart)
  await expect(page.locator('.ka-map-area .k-banner')).toHaveCount(1);
  await expect(page.locator('.ka-map-area .k-banner')).toBeHidden();
  await expect(note.getByRole('button', { name: 'Copy it from the Kiln Launcher' })).toHaveCount(0);
  await expect(note.getByText('In the Kiln Launcher, open Settings, then Game art.')).toBeHidden();
  await note.getByText('How to add it', { exact: true }).click();
  await expect(note.getByText('In the Kiln Launcher, open Settings, then Game art.')).toBeVisible();
  await page.screenshot({ path: resolve(SHOTS, 'map-desktop-no-art-steps.png') });
  // other pages do not show it
  await page.goto('/servers/this/admin/players');
  await expect(page.getByTestId('map-art-note')).toHaveCount(0);
  await ctx.close();
});

test('the Kiln Launcher on this computer: one button copies its map art, and the map has it', async ({ browser }) => {
  rmSync(ART, { recursive: true, force: true });
  fakeArt(LAUNCHER);
  const { ctx, page } = await signedIn(browser, DESKTOP);
  await page.goto('/servers/this/admin/map');
  const note = page.getByTestId('map-art-note');
  const copy = note.getByRole('button', { name: 'Copy it from the Kiln Launcher' });
  await expect(copy).toBeVisible();
  await expect(page.getByText('Preparing map art…')).toHaveCount(0);
  await page.screenshot({ path: resolve(SHOTS, 'map-desktop-no-art-launcher.png') });
  await copy.click();
  // a copy asks for a recent proof (the shell's dialog), then goes when pressed again
  const prompt = page.getByRole('dialog', { name: 'Confirm your identity', exact: true });
  await expect(prompt).toBeVisible();
  await prompt.getByLabel('Your password', { exact: true }).fill('OnlyDisposableTests-42!');
  await prompt.getByRole('button', { name: 'Confirm identity', exact: true }).click();
  await prompt.getByRole('button', { name: 'Return to the action', exact: true }).click();
  await copy.click();
  await expect(note).toHaveCount(0, { timeout: 15_000 });
  const status = await page.evaluate(async () => (await fetch('/api/core/map-assets-status')).json());
  expect(status).toMatchObject({ present: true, launcher: true, copy: { state: 'done', files: 3 } });
  await page.screenshot({ path: resolve(SHOTS, 'map-desktop-art-copied.png') });
  // Panel settings > Map art says it is there
  await page.goto('/settings');
  await page.getByRole('tab', { name: 'Map art' }).click();
  await expect(page.getByText('present', { exact: true })).toBeVisible();
  await ctx.close();
  // the rest of the run has no map art again (the tour's pages expect none)
  rmSync(ART, { recursive: true, force: true });
  rmSync(LAUNCHER, { recursive: true, force: true });
});
