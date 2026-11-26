# Identity and certificates: the contract

Covers `src/Kilnwarden.Core/Identity/*`, `src/Kilnwarden.Core/Tls/Acme/*`, `web/src/identity/*` and the tests under
`tests/Offline/Identity`, `tests/Offline/Acme`, `tests/Offline/Support/LaneHost.cs`. No Kiln mod API or ABI and no
KAP op is used.

## Settings (data folder, hand-edited, hot-reloaded; everything off by default)

`identity.json`
```json
{ "steam": { "enabled": false },
  "discord": { "enabled": false, "broker": "https://frostdev.io", "guildId": "", "allowedRoles": [] } }
```
- Discord sign-in goes through frostdev.io's broker, which is live; release builds pin its keys from 0.1.1 on. It is
  only active when enabled and the build pins a key for `broker`. Embed the public pin file with
  `-p:WebAdminBrokerPins=...` at build time. There is no setting that adds a key at runtime.
- `guildId` is required when Discord is enabled; `allowedRoles` (role ids) gate who may sign in (empty: any member).
  Roles never grant panel roles.
- `identity-state.json` holds this panel's random id (`panelId`, 32 hex) that broker assertions must name.

`acme.json` (used while `settings.json` `tls.mode` is `acme`)
```json
{ "directory": "https://acme-v02.api.letsencrypt.org/directory", "email": "", "acceptTerms": false, "names": [],
  "challenge": "http-01", "http01Bind": "", "http01Port": 80, "renewDaysBefore": 30, "minRetryMinutes": 60,
  "maxRetryHours": 24, "maxOrdersPerWeek": 5 }
```
- `names` empty: the DNS names of `settings.json tls.hostnames`. Wildcards, IPs and localhost are refused.
- `challenge`: `http-01` (port 80), `tls-alpn-01` (port 443: the panel's own listener answers it, so the panel listens
  on 443 or 443 is forwarded to it) or `dns-01` for a relay hostname. DNS-01 requires the relay to be enabled and its
  registered hostname to be the only name in the order; it waits `dns01PropagationSeconds` after publishing the signed
  challenge record. TLS-ALPN-01 (RFC 8737): while an order is validated the listener reads each ClientHello first and
  answers one that offers `acme-tls/1` for a pending name with the challenge certificate (always through SslStream:
  kwtls refuses the critical acmeIdentifier extension); every other connection is replayed to the normal engine.

## Endpoints

All JSON bodies are capped at 4 KiB; every POST/DELETE passes the existing CSRF (Origin/Sec-Fetch-Site) middleware.
Errors use the panel's `{ "error": { "code", "message" } }`.

| Method, path | Access | Contract |
|---|---|---|
| `POST /api/identity/start` `{provider: "steam"\|"discord", intent: "login"\|"link"}` | Public (login); link: signed in, unrestricted, **recent proof** | 200 `{url}` (the provider page) and sets the HttpOnly SameSite=Lax binder cookie `wa-ext` (`__Host-wa-ext` on HTTPS), 5 min. 404 `unknown_provider` (off), 400 `invalid`, 401 `unauthenticated`, 403 `restricted` / `reauth_required`, 429 `rate_limited`. |
| `GET /api/identity/steam/callback?state=&openid.*` | Public, with the starter's binder cookie | Verifies (below), settles the intent once, 303 to `/signin#ext=<state>` (login) or `/account#ext=<state>` (link); unknown state: `/signin#ext-error=expired`. `Cache-Control: no-store`, `Referrer-Policy: no-referrer`. |
| `GET /api/identity/discord/callback?state=&assertion=` (or `&error=`) | Public | Same, for the broker's assertion. |
| `POST /api/identity/complete` `{state}` | Public; needs the binder cookie of the browser that started it | Login: the sign-in answer of `/api/auth/login` (session cookie, `{ok, user}`), or 401 `not_linked`. Link: 200 `{ok, linked:{provider, subject}}`, only from the same session, still with recent proof; 409 `linked_elsewhere`. Failures: 400 `ext_expired` (unknown, used, other browser), 409 `ext_pending`, 400 `ext_canceled`, 403 `not_allowed` (Discord roles), 401 `ext_failed`. One use. |
| `GET /api/identity/links` | Signed in | `{providers:[{id,title}] (active ones), links:[{provider, subject, enabled}]}` (own account). |
| `DELETE /api/identity/links/{provider}` | Signed in, **recent proof** | 204, 404 when not linked, 403 `reauth_required`. |
| `GET /api/identity/status` | `users.view` | `{panelId, steam:{enabled,active}, discord:{enabled,active,broker,keyPinned,guildId,allowedRoles(count)}}`. |
| `GET /api/acme/status` | `acme.view` (Admin) | `{active, phase, blocked, directory, challenge, acceptTerms, names, certificate:{sha256,names,notAfter,keyType,chainLength}\|null, lastAttempt, lastSuccess, nextAttempt, lastError, failures, ordersThisWeek, maxOrdersPerWeek}`. |
| `POST /api/acme/renew` | `acme.renew` (Owner, **Sensitive**: recent proof via the gate) | 202 `{ok, started}`; 409/429 `acme_blocked` with the reason (mode, owner, terms, names, backoff, weekly budget). Audited `acme.renew`. |

`GET /api/auth/status` keeps listing active providers (`providers: ["local", {id, title}...]`) through the shared
`SignInProviders`, kept in step with identity.json.

## Permissions (new)
- `acme.view` "See the certificate and its renewals": default Admin.
- `acme.renew` "Renew the certificate now": default Owner, Sensitive.
- Linking/unlinking is self-service on the own account, gated by recent proof (no permission).

## Trust rules
- **Steam** (OpenID 2.0, no broker): `openid.ns`/`mode=id_res`; `op_endpoint` exactly `https://steamcommunity.com/openid/login`;
  `return_to` exactly this intent's URL (origin + path + one-use state); `claimed_id == identity` =
  `https://steamcommunity.com/openid/id/7656119xxxxxxxxxx`; `signed` covers op_endpoint, claimed_id, identity,
  return_to, response_nonce, assoc_handle; `response_nonce` time within 5 min; then `check_authentication` POSTed to
  Steam (no redirects, 10 s, 4 KiB answer) must say `is_valid:true`. Realm = the panel origin.
- **Discord broker protocol v1**: panel -> `{broker}/kiln/auth/discord?v=1&panel=&guild=&return=&nonce=`;
  broker -> `{return}&assertion=<JWS>` or `&error=`. JWS compact, header `{"alg":"EdDSA","typ":"kiln-broker+jws","kid"}`,
  payload `{v:1, iss:<broker origin>, aud:"kiln-webadmin", panel, ret, nonce, iat, exp, sub, name, guild, roles[]}`;
  `exp-iat <= 300 s`, 60 s skew, any member naming a token refused; Ed25519 verified in managed code
  (`Identity/Ed25519.cs`, RFC 8032 vectors) against a pinned key only. No Discord token reaches the panel.
- **Intents**: random state + binder (cookie) per start, in memory, 5 min, one use, max 1000 pending. The binder cookie
  is `SameSite=Lax` so it comes back with the provider's top-level redirect: the callback verifies and settles an intent
  only for the browser that started it (an answer brought by any other browser, for example someone who followed a
  provider link the starter sent them, is not verified). `/complete` is same-origin with the
  binder too, which stops login CSRF. A link intent is bound to the starting session.
- **No takeover, no grants**: a provider only signs in an account its user linked after re-authenticating; no account is
  created; roles are unchanged; an external sign-in starts without recent proof (`reauthenticatedUntil: null`) and keeps
  role 2FA requirements (restricted session). Provider sign-ins are not reauth credentials.
- **ACME**: RFC 8555 client (RS256 account key: Wine CNG cannot import ECDSA; RSA-2048 certificate key, served by both
  engines), every followed URL https on the directory's host, bounded bodies/polling, badNonce retried once. Orders only
  when tls.mode is acme, the owner exists and terms are accepted. The http-01 answerer listens only during an order and
  answers only published tokens whose name equals the Host header. A new certificate must pass `CertificateCheck`
  (names, validity, key = leaf key, full chain with each signature verified) before it is written (chain + key in one
  owner-only file, atomic) and announced; the TLS engines keep the previous config if they refuse it. Failures keep the
  last good certificate (or the self-signed fallback) and back off (60 min doubling to 24 h); max 5 orders a week; both
  kept in `acme-state.json` across restarts. kwtls is unchanged (still kwtls-1.0.0), so no DLL version bump.
