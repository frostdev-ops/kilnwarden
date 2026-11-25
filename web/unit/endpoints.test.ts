import { expect, test } from 'bun:test';
import { endpoints } from '../src/api/endpoints';
import { createApi } from '../src/api/http';

test('auth proofs and launcher/setup secrets are JSON bodies, never URLs', async () => {
  const calls: { url: string; method: string; body: unknown }[] = [];
  const ep = endpoints(createApi(async (url, init) => {
    calls.push({ url, method: init.method!, body: init.body ? JSON.parse(String(init.body)) : null });
    return Response.json({ ok: true });
  }));
  await ep.setup('setup-secret', 'owner', 'password');
  await ep.launcher('launcher-secret');
  await ep.reauth({ password: 'proof' });
  await ep.reauth({ code: '123456' });
  expect(calls).toEqual([
    { url: '/api/auth/setup', method: 'POST', body: { token: 'setup-secret', name: 'owner', password: 'password' } },
    { url: '/api/auth/launcher', method: 'POST', body: { secret: 'launcher-secret' } },
    { url: '/api/auth/reauth', method: 'POST', body: { password: 'proof' } },
    { url: '/api/auth/reauth', method: 'POST', body: { code: '123456' } },
  ]);
});

test('session handles and audit filters are encoded as single values', async () => {
  const urls: string[] = [];
  const ep = endpoints(createApi(async url => { urls.push(url); return Response.json([]); }));
  await ep.revokeSession('handle/other?x');
  await ep.audit({ limit: 100, user: 'name&a=b', op: 'auth.', before: '2026-01-01T00:00:00Z' });
  expect(urls[0]).toBe('/api/auth/sessions/handle%2Fother%3Fx');
  const q = new URL(urls[1], 'https://test.invalid').searchParams;
  expect(q.get('user')).toBe('name&a=b');
  expect(q.get('op')).toBe('auth.');
});
