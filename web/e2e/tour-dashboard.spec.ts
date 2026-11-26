// The overview's Customize (0.1.3), the same editor as the setup wizard's "Customize your dashboard": drag a card by its
// handle with a mouse, with a finger (touch, at phone width) or with the keyboard (Space, the arrows, Space; Escape puts
// it back), with what a screen reader hears; switch cards off and on where they are; the default in one click; saved per
// account and the same after a reload. Runs after panel.spec.ts (the owner it makes). A frame sequence of a mouse drag goes to KWA_SCREENS/dnd-frames.
import { test, expect, type Browser, type BrowserContext, type Page } from '@playwright/test';
import { execFileSync } from 'node:child_process';
import { mkdirSync } from 'node:fs';
import { resolve } from 'node:path';
import { ownerThroughWizard } from './wizard-flow';

const dir = process.env.KWA_E2E_DIR!;
const SHOTS = process.env.KWA_SCREENS ?? resolve('test-results', 'screens');
const FRAMES = resolve(SHOTS, 'dnd-frames');
const DESKTOP = { width: 1280, height: 800 };
const PHONE = { width: 390, height: 844 };
mkdirSync(FRAMES, { recursive: true });
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

/** The editor's order (every card, hidden ones too). */
const order = (page: Page) => page.locator('.wa-sort > [data-sort-key]').evaluateAll((els) => els.map((e) => (e as HTMLElement).dataset.sortKey!));
/** The overview's cards as shown (not editing), once they are there. */
async function shown(page: Page) {
  await expect(page.locator('.wa-dash > [data-card]').first()).toBeVisible();
  return page.locator('.wa-dash > [data-card]').evaluateAll((els) => els.map((e) => (e as HTMLElement).dataset.card!));
}
const center = async (page: Page, sel: string) => { const b = (await page.locator(sel).boundingBox())!; return { x: b.x + b.width / 2, y: b.y + b.height / 2 }; };
const grip = (key: string) => `.wa-sort > [data-sort-key="${key}"] .wa-grip`;
const card = (key: string) => `.wa-sort > [data-sort-key="${key}"]`;
/** A card's Show switch, clicked as a person does (on the switch's label; its track covers the input). */
const flip = (page: Page, title: string) => page.locator('label.k-toggle', { has: page.getByRole('switch', { name: `Show ${title}`, exact: true }) }).click();

async function customize(page: Page) {
  await page.goto('/');
  await page.getByRole('button', { name: 'Customize' }).click();
  await expect(page.locator('.wa-sort')).toBeVisible();
  // start from the default every time
  await page.getByRole('button', { name: 'Use the default' }).click();
}

async function save(page: Page) {
  await page.getByRole('button', { name: 'Save layout' }).click();
  await expect(page.getByText(/Your overview is saved/)).toBeVisible();
}

test('a mouse drags a card by its handle; the layout is saved and the same after a reload', async ({ browser }) => {
  const ctx = await browser.newContext({ viewport: DESKTOP, storageState: await ownerState(browser) });
  const page = await ctx.newPage();
  await customize(page);
  const before = await order(page);
  expect(before.slice(0, 3)).toEqual(['panel/server', 'panel/links', 'panel/info']);
  // Server goes onto Panel (the third card): it takes Panel's place, the cards between move up
  const from = await center(page, grip('panel/server'));
  const to = await center(page, card('panel/info'));
  await page.mouse.move(from.x, from.y);
  await page.mouse.down();
  let n = 0;
  const frame = async () => page.screenshot({ path: resolve(FRAMES, `frame-${String(n++).padStart(2, '0')}.png`), clip: { x: 248, y: 160, width: 1032, height: 640 } });
  await frame();
  for (let i = 1; i <= 12; i++) {
    await page.mouse.move(from.x + ((to.x - from.x) * i) / 12, from.y + ((to.y - from.y) * i) / 12);
    if (i === 2) await expect(page.locator('.wa-sort-ghost')).toHaveText('Server');
    if (i % 2 === 0) await frame();
  }
  await expect(page.locator(card('panel/server'))).toHaveClass(/dragging/);
  await page.mouse.up();
  await frame();
  const after = await order(page);
  expect(after.indexOf('panel/server')).toBe(2);
  expect(after.slice(0, 2)).toEqual(['panel/links', 'panel/info']);
  await expect(page.locator('[aria-live="assertive"]')).toContainText('Server dropped at position 3');
  await save(page);
  expect((await page.evaluate(async () => (await fetch('/api/core/dashboard')).json())).order.slice(0, 3)).toEqual(['panel/links', 'panel/info', 'panel/server']);
  await page.reload();
  expect((await shown(page)).slice(0, 3)).toEqual(['panel/links', 'panel/info', 'panel/server']);
  await ctx.close();
});

test('the keyboard: Space picks a card up, the arrows move it, Space drops it, Escape puts it back, all announced', async ({ browser }) => {
  const ctx = await browser.newContext({ viewport: DESKTOP, storageState: await ownerState(browser) });
  const page = await ctx.newPage();
  await customize(page);
  const live = page.locator('[aria-live="assertive"]');
  const handle = page.getByRole('button', { name: 'Move Panel', exact: true });
  await handle.focus();
  await page.keyboard.press('Space');
  await expect(live).toContainText('Picked up Panel. Position 3 of');
  await expect(handle).toHaveAttribute('aria-pressed', 'true');
  await page.keyboard.press('ArrowUp');
  await expect(live).toContainText('Panel, position 2 of');
  await page.keyboard.press('ArrowUp');
  await expect(live).toContainText('Panel, position 1 of');
  await expect(handle).toBeFocused(); // the handle keeps the focus while its card moves
  await page.screenshot({ path: resolve(SHOTS, 'desktop-overview-keyboard-grabbed.png') });
  await page.keyboard.press('Space');
  await expect(live).toContainText('Panel dropped at position 1');
  expect((await order(page)).slice(0, 3)).toEqual(['panel/info', 'panel/server', 'panel/links']);
  // Escape: back where it was
  await page.keyboard.press('Enter');
  await page.keyboard.press('ArrowDown');
  await page.keyboard.press('ArrowDown');
  await page.keyboard.press('Escape');
  await expect(live).toContainText('Move canceled. Panel is back at position 1.');
  expect((await order(page)).slice(0, 3)).toEqual(['panel/info', 'panel/server', 'panel/links']);
  // the instructions are the handle's description
  const described = await handle.evaluate((el) => document.getElementById(el.getAttribute('aria-describedby')!)?.textContent);
  expect(described).toContain('Press Space or Enter to pick it up');
  await save(page);
  await page.reload();
  expect((await shown(page)).slice(0, 3)).toEqual(['panel/info', 'panel/server', 'panel/links']);
  await ctx.close();
});

test('touch at phone width: a finger drags a card by its handle', async ({ browser }) => {
  const ctx = await browser.newContext({ viewport: PHONE, hasTouch: true, isMobile: true, storageState: await ownerState(browser) });
  const page = await ctx.newPage();
  await customize(page);
  expect((await order(page)).slice(0, 2)).toEqual(['panel/server', 'panel/links']);
  const cdp = await ctx.newCDPSession(page);
  await page.locator(card('panel/server')).scrollIntoViewIfNeeded();
  const from = await center(page, grip('panel/links'));
  const to = await center(page, card('panel/server'));
  await cdp.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [{ x: from.x, y: from.y }] });
  for (let i = 1; i <= 10; i++)
    await cdp.send('Input.dispatchTouchEvent', { type: 'touchMove', touchPoints: [{ x: from.x + ((to.x - from.x) * i) / 10, y: from.y + ((to.y - from.y) * i) / 10 }] });
  await page.screenshot({ path: resolve(SHOTS, 'phone-overview-touch-drag.png') });
  await cdp.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
  expect((await order(page)).slice(0, 2)).toEqual(['panel/links', 'panel/server']);
  // nothing wider than the phone while customizing
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(PHONE.width);
  await save(page);
  await page.reload();
  expect((await shown(page)).slice(0, 2)).toEqual(['panel/links', 'panel/server']);
  await ctx.close();
});

test('switch a card off and on where it is, then the default in one click', async ({ browser }) => {
  const ctx = await browser.newContext({ viewport: DESKTOP, storageState: await ownerState(browser) });
  const page = await ctx.newPage();
  await customize(page);
  await flip(page, 'Quick links');
  await expect(page.getByRole('switch', { name: 'Show Quick links', exact: true })).not.toBeChecked();
  // still in its place, folded
  expect((await order(page))[1]).toBe('panel/links');
  await expect(page.locator(card('panel/links'))).toHaveClass(/wa-card-off/);
  await expect(page.locator(card('panel/links')).getByText('Hidden', { exact: true })).toBeVisible();
  await page.screenshot({ path: resolve(SHOTS, 'desktop-overview-customize-hidden.png') });
  await save(page);
  expect(await shown(page)).not.toContain('panel/links');
  expect((await page.evaluate(async () => (await fetch('/api/core/dashboard')).json())).hidden).toEqual(['panel/links']);
  // on again: back in the same place
  await page.getByRole('button', { name: 'Customize' }).click();
  await flip(page, 'Quick links');
  await expect(page.getByRole('switch', { name: 'Show Quick links', exact: true })).toBeChecked();
  await save(page);
  expect((await shown(page))[1]).toBe('panel/links');
  // the default: one click (then saved)
  await page.getByRole('button', { name: 'Customize' }).click();
  const handle = page.getByRole('button', { name: 'Move Server', exact: true });
  await handle.focus();
  await page.keyboard.press('Space');
  await page.keyboard.press('End');
  await page.keyboard.press('Space');
  await flip(page, 'Panel');
  await expect(page.getByRole('switch', { name: 'Show Panel', exact: true })).not.toBeChecked();
  await page.getByRole('button', { name: 'Use the default' }).click();
  expect((await order(page)).slice(0, 3)).toEqual(['panel/server', 'panel/links', 'panel/info']);
  await expect(page.getByRole('switch', { name: 'Show Panel' })).toBeChecked();
  await save(page);
  expect((await shown(page)).slice(0, 3)).toEqual(['panel/server', 'panel/links', 'panel/info']);
  await ctx.close();
});
