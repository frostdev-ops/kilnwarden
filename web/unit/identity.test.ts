import { expect, test } from 'bun:test';
import { createApi } from '../src/api/http';
import { externalErrorText, identityEndpoints, safeProviderUrl, takeExternalFragment } from '../src/identity/external';

const STATE = 'A'.repeat(43);

function fakeHistory() {
  const calls: string[] = [];
  return { calls, h: { state: null, replaceState: (_s: unknown, _t: string, url?: string | URL | null) => { calls.push(String(url)); } } };
}

test('the callback state is read from the fragment and wiped from the address bar', () => {
  const { calls, h } = fakeHistory();
  expect(takeExternalFragment({ hash: '#ext=' + STATE, pathname: '/signin', search: '?next=/x' }, h)).toEqual({ state: STATE });
  expect(calls).toEqual(['/signin?next=/x']);
  expect(takeExternalFragment({ hash: '#ext-error=expired', pathname: '/signin', search: '' }, h)).toEqual({ error: 'expired' });
  expect(takeExternalFragment({ hash: '#ext=<script>', pathname: '/signin', search: '' }, h)).toEqual({ error: 'malformed' });
  // other fragments (setup tokens, anchors) are left alone
  const before = calls.length;
  expect(takeExternalFragment({ hash: '#token=abc', pathname: '/setup', search: '' }, h)).toBeNull();
  expect(takeExternalFragment({ hash: '', pathname: '/signin', search: '' }, h)).toBeNull();
  expect(calls.length).toBe(before);
});

test('only https provider pages are followed', () => {
  expect(safeProviderUrl('https://steamcommunity.com/openid/login?openid.mode=checkid_setup')).toStartWith('https://steamcommunity.com/');
  expect(safeProviderUrl('javascript:alert(1)')).toBeNull();
  expect(safeProviderUrl('http://frostdev.io/kiln/auth/discord')).toBeNull();
  expect(safeProviderUrl('/relative')).toBeNull();
});

test('identity routes: start/complete are JSON bodies, provider ids are encoded', async () => {
  const calls: { url: string; method: string; body: unknown }[] = [];
  const id = identityEndpoints(createApi(async (url, init) => {
    calls.push({ url, method: init.method!, body: init.body ? JSON.parse(String(init.body)) : null });
    return init.method === 'DELETE' ? new Response(null, { status: 204 }) : Response.json({ url: 'https://steamcommunity.com/openid/login' });
  }));
  await id.start('steam', 'login');
  await id.complete(STATE);
  await id.unlink('steam/../x');
  expect(calls).toEqual([
    { url: '/api/identity/start', method: 'POST', body: { provider: 'steam', intent: 'login' } },
    { url: '/api/identity/complete', method: 'POST', body: { state: STATE } },
    { url: '/api/identity/links/steam%2F..%2Fx', method: 'DELETE', body: null },
  ]);
});

test('error codes read as sentences', () => {
  expect(externalErrorText('not_linked', 'steam')).toContain('No panel account is linked to this Steam account');
  expect(externalErrorText('not_allowed')).toContain('Discord roles');
  expect(externalErrorText('whatever')).toContain('could not be verified');
});
