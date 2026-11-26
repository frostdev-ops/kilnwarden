// The setup wizard (0.1.2), every page and its error states, on a server of its own (playwright.config.ts: HTTPS,
// a relay and a CA address that never answer, so nothing leaves this PC; the claim pages are a stand-in). The account
// and the settings the wizard writes are real; what only a real relay and Let's Encrypt can do (the claim noticed,
// the certificate, the address check) is the server's answer given by the test here, and runs for real in
// the relay's own end-to-end tests. Screenshots of every page at 1280x800 and 390x844 go to KWA_WIZARD_SCREENS, with the
// words each page shows (words.json).
import { test, expect, type Browser, type BrowserContext, type Page, type Route } from '@playwright/test';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';

const dir = process.env.KWA_E2E_WIZARD_DIR!;
const BASE = `https://127.0.0.1:${process.env.KWA_E2E_WIZARD_PORT}`;
const SHOTS = process.env.KWA_WIZARD_SCREENS ?? resolve('test-results', 'wizard-screens');
const DESKTOP = { width: 1280, height: 800 };
const PHONE = { width: 390, height: 844 };
const password = 'OnlyDisposableWizard-42!';
const words: Record<string, number> = {};
mkdirSync(SHOTS, { recursive: true });

test.use({ baseURL: BASE, ignoreHTTPSErrors: true });
test.describe.configure({ mode: 'serial' });

let ownerState: Awaited<ReturnType<BrowserContext['storageState']>> | null = null;

const data = (file: string) => JSON.parse(readFileSync(resolve(dir, 'kiln/data/kilnwarden', file), 'utf8'));

/** No request leaves this PC: the claim pages are a stand-in, anything else off this machine is refused. */
async function offline(ctx: BrowserContext) {
  await ctx.route(/^https?:\/\/(?!127\.0\.0\.1[:/])/, (r) => {
    if (r.request().url().startsWith('https://claim.kiln.nexus/'))
      return r.fulfill({ contentType: 'text/html', body: '<!doctype html><title>Claim</title><h1>The claim pages (a stand-in)</h1>' });
    return r.abort();
  });
}

async function newPage(browser: Browser, viewport: { width: number; height: number }, signedIn: boolean) {
  const ctx = await browser.newContext({ viewport, ignoreHTTPSErrors: true, baseURL: BASE, storageState: signedIn ? ownerState ?? undefined : undefined });
  await offline(ctx);
  return { ctx, page: await ctx.newPage() };
}

/** A screenshot of the page, and the words its card shows (closed Details hold the rest). */
async function shot(page: Page, name: string) {
  const card = page.locator('main.wa-wizard');
  await expect(card).toBeVisible();
  await page.waitForTimeout(250);
  const text = await card.innerText();
  words[name] = text.split(/\s+/).filter((w) => /[\p{L}\p{N}]/u.test(w)).length;
  await page.screenshot({ path: resolve(SHOTS, `${name}.png`), animations: 'disabled' });
}

/** The server's answer to the wizard's state, changed by the test (what a real relay and CA would make it say). */
async function stubState(page: Page, change: (s: any) => any) {
  let last: any = null;
  await page.unroute('**/api/setup/state');
  await page.route('**/api/setup/state', async (r: Route) => {
    try {
      const res = await r.fetch();
      last = await res.json();
    } catch {
      // the listener restarting for a moment: the last answer
    }
    await r.fulfill({ json: change(structuredClone(last ?? {})) });
  });
}

const steps = (...s: [string, string, any?][]) => s.map(([id, state, extra]) => ({ id, state, note: null, problem: null, ...extra }));

function hosted(s: any, list: any[]) {
  s.hosted = { host: 'embervale.kiln.nexus', name: 'embervale', url: 'https://embervale.kiln.nexus/', steps: list, ready: list.every((x) => x.state === 'done') };
  s.discord = { enabled: false, guildId: '', available: true };
  return s;
}

function token(): string {
  const log = readFileSync(resolve(dir, 'devhost.log'), 'utf8');
  const t = log.match(/\/setup#token=([A-Za-z0-9_-]{43})/)?.[1];
  expect(t).toBeTruthy();
  return t!;
}

test('before the account: no link, a used link, the welcome and the account', async ({ browser }) => {
  const { ctx, page } = await newPage(browser, DESKTOP, false);
  // the log's first-start block: three lines
  const log = readFileSync(resolve(dir, 'devhost.log'), 'utf8').split('\n');
  const at = log.findIndex((l) => l.includes('set up your panel (about 2 minutes)'));
  expect(at).toBeGreaterThanOrEqual(0);
  expect(log[at + 1]).toMatch(/On this computer, open https:\/\/127\.0\.0\.1:\d+\/setup#token=/);
  expect(log[at + 2]).toContain('kilnwarden setup hosted <a name for your panel>');

  await page.goto('/setup');
  await expect(page.getByRole('heading', { name: 'Open your setup link' })).toBeVisible();
  await shot(page, 'desktop-00-no-link');

  // a link that was used (or made up): said in plain words, with the way to a new one
  await page.goto('about:blank'); // a new load: the page reads its link once
  await page.goto(`/setup#token=${'A'.repeat(43)}`);
  await page.getByRole('button', { name: 'Start' }).click();
  await page.getByLabel('Your name', { exact: true }).fill('ashwyn');
  await page.getByLabel('Password', { exact: true }).fill(password);
  await page.getByLabel('Password again', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Next' }).click();
  await expect(page.getByText('This setup link was used or is too old.')).toBeVisible();
  await shot(page, 'desktop-02c-owner-link-used');

  await page.goto('about:blank');
  await page.goto(`/setup#token=${token()}`);
  await expect(page.getByRole('heading', { name: 'Set up your panel' })).toBeVisible();
  expect(new URL(page.url()).hash).toBe('');
  await shot(page, 'desktop-01-welcome');
  await page.getByRole('button', { name: 'Start' }).click();
  await expect(page.getByRole('heading', { name: 'Create your account' })).toBeVisible();
  await shot(page, 'desktop-02-account');
  await page.getByLabel('Your name', { exact: true }).fill('ashwyn');
  await page.getByLabel('Password', { exact: true }).fill('short');
  await page.getByLabel('Password again', { exact: true }).fill('shorter');
  await expect(page.getByText('At least 12 characters.')).toBeVisible();
  await expect(page.getByText('The passwords differ.')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Next' })).toBeDisabled();
  await shot(page, 'desktop-02b-account-errors');
  await page.getByLabel('Password', { exact: true }).fill(password);
  await page.getByLabel('Password again', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Next' }).click();
  await expect(page.getByRole('heading', { name: 'How will you open your panel?' })).toBeVisible();
  ownerState = await ctx.storageState();
  await ctx.close();
});

test('hosted access: the choice, the name, the claim with Discord, the checklist, Discord sign-in, done', async ({ browser }) => {
  const { ctx, page } = await newPage(browser, DESKTOP, true);
  await page.goto('/setup');
  await expect(page.getByRole('heading', { name: 'How will you open your panel?' })).toBeVisible();
  const anywhere = page.getByRole('radio', { name: /From anywhere/ });
  await expect(anywhere).toBeChecked(); // the suggestion, preselected
  await expect(page.locator('.wa-choice.on')).toContainText('Recommended');
  await shot(page, 'desktop-03-route');
  await page.getByRole('button', { name: 'Next' }).click();

  // the name, checked as it is typed
  await expect(page.getByRole('heading', { name: 'Pick a name for your panel' })).toBeVisible();
  const name = page.getByLabel('Name', { exact: true });
  await name.fill('ab');
  await name.blur();
  await expect(page.getByText('Use at least 3 characters.')).toBeVisible();
  await name.fill('admin');
  await expect(page.getByText('"admin" is kept for Kiln itself. Pick another name.')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Next' })).toBeDisabled();
  await shot(page, 'desktop-04b-name-error');
  await name.fill('my_server');
  await expect(page.getByText('Use only letters, digits and dashes.')).toBeVisible();
  await name.fill('Embervale');
  await expect(page.getByText('https://embervale.kiln.nexus')).toBeVisible();
  await shot(page, 'desktop-04-name');
  await page.getByRole('button', { name: 'Next' }).click();

  // one button: the claim page opens in a new tab with everything in its link
  await expect(page.getByRole('heading', { name: 'Claim it with Discord' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Kiln Discord' })).toHaveAttribute('href', 'https://discord.gg/7pjr8SMTfU');
  await expect(page.getByRole('link', { name: "Let's Encrypt terms" })).toHaveAttribute('href', 'https://letsencrypt.org/repository/');
  await shot(page, 'desktop-05-claim');
  const popup = page.waitForEvent('popup');
  await page.getByRole('button', { name: 'Claim with Discord' }).click();
  const tab = await popup;
  await tab.waitForURL(/^https:\/\/claim\.kiln\.nexus\/claim\?v=1&host=embervale\.kiln\.nexus&key=[A-Za-z0-9_-]+&time=\d+&nonce=[A-Za-z0-9_-]{43}&sig=[A-Za-z0-9_-]+$/);
  expect(await tab.evaluate(() => window.opener)).toBeNull();
  await tab.close();
  await expect(page.getByRole('heading', { name: 'Waiting for your claim' })).toBeVisible();
  await shot(page, 'desktop-06-claim-waiting');

  // every setting the old manual setup asked for, written by the wizard (no file edited by hand); the test relay kept
  const relay = data('relay.json');
  expect([relay.enabled, relay.host, relay.origin]).toEqual([true, 'embervale.kiln.nexus', 'https://127.0.0.1:9']);
  const acme = data('acme.json');
  expect([acme.challenge, acme.acceptTerms, acme.names, acme.directory]).toEqual(['dns-01', true, ['embervale.kiln.nexus'], 'https://127.0.0.1:9/directory']);
  const settings = data('settings.json');
  expect(settings.tls.mode === 'acme' || settings.tls.mode === 3).toBe(true);
  expect(settings.allowedHosts).toContain('embervale.kiln.nexus');
  expect(settings.bind).toBe('127.0.0.1');

  // the real relay answer here (nobody listens at the test relay): the checklist's first line failed, with the fix
  const real = async () => page.evaluate(async () => (await fetch('/api/setup/state')).json());
  await expect.poll(async () => (await real()).hosted.steps[0].state, { timeout: 20_000 }).toBe('failed');
  expect((await real()).hosted.steps[0].problem.title).toBe("This server can't reach 127.0.0.1.");

  // a reload (or a restart) comes back to the same page
  await page.reload();
  await expect(page.getByRole('heading', { name: 'Claim it with Discord' })).toBeVisible();

  // the claim noticed: the page moves on by itself
  await stubState(page, (s) => hosted(s, steps(['connected', 'done'], ['claimed', 'done'], ['secure', 'working'], ['ready', 'waiting'])));
  await expect(page.getByRole('heading', { name: 'Getting your panel ready' })).toBeVisible({ timeout: 15_000 });
  await expect(page.locator('.wa-check[data-step="secure"] > span:last-child')).toHaveText('Securing…');
  await shot(page, 'desktop-07-progress');

  // a failure: plain words and the fix; the technical detail behind Details
  await stubState(page, (s) => hosted(s, steps(['connected', 'failed', { problem: { title: "This server can't reach frostdev.io.", fix: 'Your host may block outgoing connections. Ask them to allow outgoing HTTPS (port 443) to frostdev.io.', detail: 'cannot connect to frostdev.io:443: outbound HTTPS is blocked here or the network is down' } }], ['claimed', 'waiting'], ['secure', 'waiting'], ['ready', 'waiting'])));
  await expect(page.getByText("This server can't reach frostdev.io.").first()).toBeVisible();
  await expect(page.getByText('cannot connect to frostdev.io:443')).toBeHidden();
  await shot(page, 'desktop-08-progress-failed');
  await page.getByText('Details', { exact: true }).click();
  await expect(page.getByText('cannot connect to frostdev.io:443')).toBeVisible();
  await page.screenshot({ path: resolve(SHOTS, 'desktop-08b-progress-failed-details.png') });
  await stubState(page, (s) => hosted(s, steps(['connected', 'done'], ['claimed', 'done'], ['secure', 'failed', { problem: { title: "The certificate didn't come through.", fix: 'It tries again by itself at 14:05. Nothing to do unless this stays for hours: then ask on the Kiln Discord.', detail: 'urn:ietf:params:acme:error:dns' } }], ['ready', 'waiting'])));
  await expect(page.getByText("The certificate didn't come through.").first()).toBeVisible();
  await shot(page, 'desktop-08c-progress-certificate-failed');

  // ready: the address, and on
  await stubState(page, (s) => hosted(s, steps(['connected', 'done'], ['claimed', 'done'], ['secure', 'done'], ['ready', 'done'])));
  await expect(page.getByRole('heading', { name: 'Your panel is ready' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'https://embervale.kiln.nexus/' })).toBeVisible();
  for (const [id, text] of [['connected', 'Connected'], ['claimed', 'Claimed'], ['secure', 'Secure'], ['ready', 'Ready']])
    await expect(page.locator(`.wa-check[data-step="${id}"] > span:last-child`)).toHaveText(text);
  await shot(page, 'desktop-09-ready');
  await page.getByRole('button', { name: 'Next' }).click();

  // Discord sign-in for helpers: optional, skippable; the server ID checked
  await expect(page.getByRole('heading', { name: 'Let helpers sign in with Discord?' })).toBeVisible();
  await shot(page, 'desktop-10-discord');
  await page.getByRole('button', { name: 'Set it up' }).click();
  const guild = page.getByLabel("Your Discord server's ID", { exact: true });
  await guild.fill('12ab');
  await expect(guild).toHaveValue('12');
  await expect(page.getByRole('button', { name: 'Save' })).toBeDisabled();
  await shot(page, 'desktop-10b-discord-form');
  await guild.fill('1568454645683195976');
  await page.getByRole('button', { name: 'Save' }).click();
  await expect(page.getByRole('heading', { name: "You're all set" })).toBeVisible();
  const identity = data('identity.json');
  expect([identity.discord.enabled, identity.discord.guildId]).toEqual([true, '1568454645683195976']);
  await expect(page.getByText('https://embervale.kiln.nexus/')).toBeVisible();
  await expect(page.getByRole('link', { name: 'Open your panel' })).toHaveAttribute('href', 'https://embervale.kiln.nexus/');
  await shot(page, 'desktop-11-done');
  await page.unroute('**/api/setup/state');
  await page.getByRole('button', { name: 'Stay on this address' }).click();
  await expect(page.getByRole('heading', { level: 1, name: 'Kilnwarden', exact: true })).toBeVisible();
  expect(data('setup.json').done).toBe(true);
  // the overview stays the overview once it is done
  await page.goto('/');
  await expect(page.getByRole('heading', { level: 1, name: 'Kilnwarden', exact: true })).toBeVisible();
  await ctx.close();
});

test('the Kiln Launcher and an own address, from Panel settings > Run the setup again', async ({ browser }) => {
  const { ctx, page } = await newPage(browser, DESKTOP, true);
  await page.goto('/settings');
  await page.getByRole('button', { name: 'Run the setup again' }).click();
  await expect(page.getByRole('heading', { name: 'How will you open your panel?' })).toBeVisible();
  await page.getByRole('radio', { name: /With the Kiln Launcher/ }).check();
  await page.getByRole('button', { name: 'Next' }).click();
  await expect(page.getByRole('heading', { name: 'Use the Kiln Launcher' })).toBeVisible();
  await expect(page.getByText('Open web panel')).toBeVisible();
  await shot(page, 'desktop-12-launcher');
  await page.getByRole('button', { name: 'Next' }).click();
  // a build without the broker's keys (this one): the Discord page has nothing to offer and is passed
  await expect(page.getByRole('heading', { name: "You're all set" })).toBeVisible();
  await shot(page, 'desktop-13-done-launcher');

  await page.getByRole('button', { name: 'Run the setup again' }).click();
  await page.getByRole('radio', { name: /At my own web address/ }).check();
  await page.getByRole('button', { name: 'Next' }).click();
  await expect(page.getByRole('heading', { name: 'Your own address' })).toBeVisible();
  await shot(page, 'desktop-14-own');
  // the server checks the address (a real refusal: nothing is changed)
  await page.getByLabel('Web address', { exact: true }).fill('https://nope');
  await page.getByRole('button', { name: 'Next' }).click();
  await expect(page.getByText('Type a web address such as panel.example.com (no https://, no port).')).toBeVisible();
  await shot(page, 'desktop-15-own-error');
  expect(data('settings.json').bind).toBe('127.0.0.1');
  // a good one: saving it would open this test PC's port to the network, so the server's answer is given here
  await page.route('**/api/setup/own', (r) => r.fulfill({ json: { ok: true } }));
  const own = (list: any[]) => (s: any) => {
    s.route = 'own';
    s.own = { host: 'panel.example.com', port: 443, challenge: 'http-01', url: 'https://panel.example.com/', steps: list, ready: list.every((x) => x.state === 'done') };
    return s;
  };
  await stubState(page, own(steps(['found', 'done'], ['reachable', 'failed', { problem: { title: "This server can't reach panel.example.com:443.", fix: 'Open port 443 in the server\'s firewall and your router. At home, routers often can\'t loop back to themselves: try opening the address on your phone with Wi-Fi off.', detail: 'cannot connect to panel.example.com:443 (ConnectionRefused)' } }], ['secure', 'waiting'], ['ready', 'waiting'])));
  await page.getByLabel('Web address', { exact: true }).fill('panel.example.com');
  await page.getByRole('button', { name: 'Next' }).click();
  await expect(page.getByRole('heading', { name: 'Getting your panel ready' })).toBeVisible();
  await expect(page.getByText("This server can't reach panel.example.com:443.").first()).toBeVisible();
  await shot(page, 'desktop-16-own-progress-failed');
  await stubState(page, own(steps(['found', 'done'], ['reachable', 'done'], ['secure', 'done'], ['ready', 'done'])));
  await expect(page.getByRole('heading', { name: 'Your panel is ready' })).toBeVisible();
  await shot(page, 'desktop-17-own-ready');
  await ctx.close();
});

test('the owner wizard recovers when its initial state request fails', async ({ browser }) => {
  const { ctx, page } = await newPage(browser, DESKTOP, true);
  let attempts = 0;
  let failures = 1;
  await page.route('**/api/setup/state', async (r) => {
    if (++attempts <= failures) return r.abort('connectionrefused');
    return r.fulfill({ json: { done: false, route: 'launcher', step: 'launcher', suggested: 'hosted', reason: 'default', zone: 'kiln.nexus', launcher: '0.2.0-dev.31.14', client: { thisMachine: true, hosted: false } } });
  });
  await page.goto('/setup');
  await expect(page.getByRole('heading', { name: 'Use the Kiln Launcher' })).toBeVisible();
  expect(attempts).toBeGreaterThan(1);

  // A sustained outage is bounded and offers a working retry, preserving the saved route.
  attempts = 0;
  failures = 4;
  await page.reload();
  await expect(page.getByRole('heading', { name: 'The panel did not answer' })).toBeVisible();
  expect(attempts).toBe(4);
  await page.getByRole('button', { name: 'Try again' }).click();
  await expect(page.getByRole('heading', { name: 'Use the Kiln Launcher' })).toBeVisible();
  await page.getByRole('button', { name: 'Next' }).click();
  await expect(page.locator('main.wa-wizard')).toHaveAttribute('data-step', 'discord');
  await ctx.close();
});

test('every page at phone width', async ({ browser }) => {
  // before the account (the server says there is none; nothing is sent)
  {
    const { ctx, page } = await newPage(browser, PHONE, false);
    await page.route('**/api/auth/status', (r) => r.fulfill({ json: { ownerExists: false, signedIn: false, launcherLogin: true, providers: ['local'] } }));
    await page.goto('/setup');
    await shot(page, 'phone-00-no-link');
    await page.goto('about:blank');
    await page.goto('/setup#token=only-a-picture');
    await shot(page, 'phone-01-welcome');
    await page.getByRole('button', { name: 'Start' }).click();
    await shot(page, 'phone-02-account');
    await ctx.close();
  }
  const { ctx, page } = await newPage(browser, PHONE, true);
  const put = (route: string, step: string) => page.evaluate(async (b) => { await fetch('/api/setup/progress', { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(b) }); }, { route, step });
  const pages: [string, string, string, ((s: any) => any)?][] = [
    ['', 'route', 'phone-03-route'],
    ['hosted', 'name', 'phone-04-name'],
    ['hosted', 'claim', 'phone-05-claim', (s) => hosted(s, steps(['connected', 'done'], ['claimed', 'waiting', { note: 'claim' }], ['secure', 'waiting'], ['ready', 'waiting']))],
    ['hosted', 'progress', 'phone-07-progress', (s) => hosted(s, steps(['connected', 'done'], ['claimed', 'done'], ['secure', 'working'], ['ready', 'waiting']))],
    ['hosted', 'progress', 'phone-08-progress-failed', (s) => hosted(s, steps(['connected', 'failed', { problem: { title: "This server can't reach frostdev.io.", fix: 'Your host may block outgoing connections. Ask them to allow outgoing HTTPS (port 443) to frostdev.io.', detail: 'x' } }], ['claimed', 'waiting'], ['secure', 'waiting'], ['ready', 'waiting']))],
    ['hosted', 'progress', 'phone-09-ready', (s) => hosted(s, steps(['connected', 'done'], ['claimed', 'done'], ['secure', 'done'], ['ready', 'done']))],
    ['hosted', 'discord', 'phone-10-discord', (s) => hosted(s, steps(['connected', 'done'], ['claimed', 'done'], ['secure', 'done'], ['ready', 'done']))],
    ['launcher', 'launcher', 'phone-12-launcher'],
    ['own', 'own', 'phone-14-own'],
    ['hosted', 'done', 'phone-11-done', (s) => hosted(s, steps(['connected', 'done'], ['claimed', 'done'], ['secure', 'done'], ['ready', 'done']))],
  ];
  await page.goto('/settings');
  for (const [route, step, file, change] of pages) {
    await put(route, step);
    await page.evaluate(() => sessionStorage.setItem('wa-setup-name', 'embervale'));
    await page.unroute('**/api/setup/state');
    // the server's answer for that page (the account's setup is done by now: shown as not done, at that page)
    await stubState(page, (s) => { const x = (change ?? ((y: any) => y))(s); x.done = false; x.step = step; x.route = route; return x; });
    await page.goto('/setup');
    await expect(page.locator('main.wa-wizard')).toHaveAttribute('data-step', step);
    // nothing wider than the phone
    expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(PHONE.width);
    await shot(page, file);
  }
  await ctx.close();
  writeFileSync(resolve(SHOTS, 'words.json'), JSON.stringify(words, null, 2) + '\n');
});
