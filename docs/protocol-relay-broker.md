# Kilnwarden client protocols: relay, DNS-01 and Discord broker

This file describes, exactly, what the mod sends to and expects from the frostdev.io hosted services. It is written
from the code (`src/Kilnwarden.Core/Relay/*`, `Tls/Acme/AcmeManager.cs` `OrderViaRelayAsync`,
`Identity/DiscordBroker.cs`, `Identity/IdentityRoutes.cs`). frostdev.io's hosted side (relay, DNS endpoint, broker,
claim pages; live, not part of this repository) is built against it. A change to anything here is a protocol change: it is recorded in the change log at the end, with the
mod version that first sends it.

Protocol version: **relay `kwa-relay/1`**, **broker `v=1`**. Mod versions: 0.1.0 and later.

Conventions:
- "base64url" means RFC 4648 §5 without padding (`=`), as written by .NET `System.Buffers.Text.Base64Url`.
- `ORIGIN` is `relay.json` `origin` (relay, DNS) or `identity.json` `discord.broker` (broker): an `https://host[:port]`
  origin with no path, query, fragment or user info. The default is `https://frostdev.io`.
- `HOST` is `relay.json` `host`: the registered, lower-case DNS name (`<name>.kiln.nexus` on frostdev.io's relay; one
  label, never the zone's apex or a reserved label such as `www`, `api` or `relay`).
- All times are Unix seconds (UTC), written as decimal integers.

## 1. The relay key and registration

- On first use the mod generates an **RSA-2048** key pair. It keeps the private key as PKCS#8 PEM in
  `kiln/data/kilnwarden/relay-key.pem` (owner-only file) and writes the public key as SubjectPublicKeyInfo PEM
  (`-----BEGIN PUBLIC KEY-----`) to `relay-public.pem` in the same folder, every start.
- **Registration** binds `HOST` to the SPKI in `relay-public.pem`, outside the signed requests below, one of two ways:
  - the owner **claims** `HOST` on the relay operator's claim pages with a claim link the mod makes (§1.1), signed in
    with a Discord account that is a member of the operator's Discord server (frostdev.io: the Kiln Discord). One name
    per Discord account;
  - or the operator registers it by hand (the owner sends `relay-public.pem`).
  The relay accepts a signed request only if its `X-Kwa-Host` is registered and the signature verifies with that
  host's registered key.
- The key never rotates by itself. Deleting both files makes a new key, which needs a new registration (or a claim
  link from the new key for the same name, by the Discord account that owns it: the name moves to the new key).

### 1.1 Claim links (mod 0.1.0; the claim pages' own origin since 0.1.1)

The mod makes one when the owner asks (Panel settings > Hosted access > **Make a claim link**, or the console command
`kilnwarden claim`) for `HOST` (`relay.json` `host`). It is the signed block of §2 with kind **`claim`**, never sent as
headers:

```
kwa-relay/1
claim
<HOST>
<id: SHA-256 of the SPKI DER, 64 lower-case hex>
<time>
<nonce>
```

signed RSASSA-PKCS1-v1_5 SHA-256 with the relay key, and carried in a URL on the claim pages' own origin,
`https://claim.<ZONE>`, where `ZONE` is `HOST` without its first label (`myserver.kiln.nexus`: `https://claim.kiln.nexus`;
`claim` is a reserved label, never a panel):

```
https://claim.<ZONE>/claim?v=1&host=<HOST>&key=<base64url SPKI DER>&time=<time>&nonce=<nonce>&sig=<base64url signature>
```

(0.1.0 put the link on the relay's origin, `ORIGIN/kiln/webadmin/names/claim?...`; frostdev.io sends those on to the
claim pages unchanged.)

Exactly these six parameters, each once (`Uri.EscapeDataString` on `host`; the rest are already URL-safe). The relay
operator's claim pages and relay:
- accept the link only for a `HOST` that is `<label>.<zone>` with a valid, unreserved label;
- verify the signature with the `key` in the link, whose SHA-256 is the signed `id`;
- take it **once**, within **15 minutes** after `time` (and not more than the 120 s clock window ahead of it), and not
  one made before the relay last started;
- register `HOST` for that key and the signed-in Discord account: pending until the key's first signed request (the
  mod's control link), at most an hour, then active. A link for a name the account already holds with another key
  moves the name to the new key: at once while the name is pending, **24 hours later** once it is active (§3.1: the
  old key keeps the name until then, its panel is told and can undo the move; the new key's requests are refused with
  `403` and `X-Kwa-Moving-At: <time>`). The account's release of an active name waits the same 24 hours. A link for a
  name registered for this very key without an owner (by the operator, or after an undo) gives it to the account. A
  link from the key the name has cancels what is scheduled for it.

The mod never logs the link (only the console's answer file and the Settings page show it); whoever opens it first
may claim the name for this key, so the owner opens it themselves. A Discord account holds one live name; the
operator can ban names and accounts. The relay keeps the owner's Discord user id per name and nothing else about them.

## 2. Signed requests (every relay and DNS request)

Every request the mod makes to the relay carries five headers:

| Header | Value |
|---|---|
| `X-Kwa-Host` | `HOST` |
| `X-Kwa-Id` | the request's identifier (below); **an empty value for `control`** |
| `X-Kwa-Time` | the mod's current time, Unix seconds |
| `X-Kwa-Nonce` | 43 base64url characters (32 random bytes), new for every request |
| `X-Kwa-Signature` | base64url of the signature |

The signed string is UTF-8, six lines joined by `\n`, **no trailing newline**:

```
kwa-relay/1
<kind>
<HOST>
<id>
<time>
<nonce>
```

The signature is **RSASSA-PKCS1-v1_5 with SHA-256** over those bytes, with the relay key.

| kind | Request | id |
|---|---|---|
| `control` | WebSocket `GET ORIGIN/kiln/webadmin/relay/control` | empty string |
| `data` | WebSocket `GET ORIGIN/kiln/webadmin/relay/data` | the stream id from the control message (32 lower-case hex) |
| `dns-add` | `PUT ORIGIN/kiln/webadmin/relay/dns` | the TXT value (43 base64url characters) |
| `dns-remove` | `DELETE ORIGIN/kiln/webadmin/relay/dns` | the same TXT value |
| `claim` | never a request of its own: the claim link of §1.1 | the SPKI's SHA-256, 64 lower-case hex |
| `move-undo` | `DELETE ORIGIN/kiln/webadmin/relay/move` (0.1.1, §3.1), no body | empty string |

The kind is not sent as a header. The relay derives it from the path and method and rebuilds the signed string. The
mod expects the relay to:
- refuse a time more than a short window away from its own clock (the mod's clock is the server's clock and may be off
  by some seconds; 120 s is a reasonable window);
- remember nonces for that window and refuse a repeat (replay);
- refuse a `data` id it did not announce on that host's current control link.

WebSocket URLs are built from `ORIGIN` with the scheme changed to `wss` and the path above; the port is kept.

HTTP client behaviour on the mod side: no proxy, no cookies, no redirects followed, TCP connect timeout 8 s.

## 3. Control link

- The mod opens one control WebSocket per relay session while all of these hold: `relay.json` `enabled` is true, the
  owner account exists, `settings.json` `tls.mode` is not `off`, the listener is HTTPS, and `HOST` is in
  `settings.json` `allowedHosts` or `tls.hostnames`.
- The WebSocket opening handshake must finish within 10 s. The mod sends a WebSocket ping every 20 s and drops the link
  if no pong arrives within 15 s (the relay's WebSocket stack must answer pings; any standard one does).
- The mod **sends no messages** on the control link. Since 0.1.1 it adds the request header `X-Kwa-Notices: 1` (not
  signed: it only asks for §3.1's notices; a relay sends them to no link that did not ask, so a 0.1.0 mod never gets
  one). The relay sends one **text** message per new browser connection:

```json
{"id":"<32 lower-case hex>","ip":"<browser IP literal>","port":<browser source port>}
```

  - `id`: exactly 32 characters `[0-9a-f]`, unique per browser connection.
  - `ip`: an IPv4 or IPv6 literal (IPv4-mapped IPv6 is treated as IPv4). It is the browser's real address: the mod uses
    it for the IP allowlist, per-IP rate limits, sign-in lockouts and the audit log. The relay is trusted to report it
    correctly. It must be a public unicast address: the mod ignores an announcement of a loopback, private, link-local,
    CGNAT, multicast, unspecified or documentation address (no data link is opened; it logs one warning per control
    link), and applies the IP allowlist to relayed peers without the loopback exemption direct connections have.
  - `port`: 1 to 65535.
  - Property names match case-insensitively; unknown properties are ignored.
  - Each message must be **one complete text frame of at most 1024 bytes**. A binary message, a fragmented or longer
    message, or one that does not have the shape above makes the mod close the control link (it reconnects later).
    Since 0.1.1 a message with a `notice` property is a notice (§3.1), never an announcement; a notice of a kind the
    mod does not know, or one it can't read, is ignored and never closes the link.
- For each message the mod opens a `data` WebSocket for that `id`, unless it already has a stream with that id or
  already has **24** streams open on this control link. An announcement it does not take is ignored without an answer;
  the relay should time out that browser connection (10 s is enough: the mod dials the data link at once).
- The control link ends when the relay closes it, on a network error, or when the mod's settings, listener or the
  conditions above change. Ending it also aborts every data stream of that session. The mod retries **4 s** after a
  session ends, for as long as the relay is enabled. It logs a warning per failed attempt (exception type only).

### 3.1 Notices about the name (0.1.1)

On a link that asked (`X-Kwa-Notices: 1`) the relay sends, at once and whenever it changes, what is scheduled for the
name:

```json
{"notice":"name","host":"<HOST>","scheduled":null}
{"notice":"name","host":"<HOST>","scheduled":{"to":"<64 lower-case hex>","requested":<time>,"at":<time>}}
{"notice":"name","host":"<HOST>","scheduled":{"to":null,"requested":<time>,"at":<time>}}
```

- `scheduled` non-null: the name's Discord owner asked on the claim pages to move it to the key with that SPKI SHA-256
  (`to`), or to release it (`to` null), at `requested`. It takes effect at `at` (24 hours later). Until then the name
  stays with this key: browsers, signed requests and DNS-01 records go to this server only.
- The panel shows it to whoever may see its settings, and logs it once per change. The owner (`core.settings.edit`,
  with a recent proof) can undo it: `DELETE ORIGIN/kiln/webadmin/relay/move` with the five signed headers of §2, kind
  `move-undo`, an empty id, no body. The relay takes the undo only signed by the key the name has: `204` undone (the
  name stays with this key and is no longer tied to the Discord account that asked; a claim link from this key ties it
  to an account again), `404` (`nothing_scheduled`), or §2's refusals.
- When the change takes effect, a link that is up gets one last notice, then the relay closes it:

```json
{"notice":"name","host":"<HOST>","scheduled":null,"done":{"to":"<64 lower-case hex>"|null,"at":<time>}}
```

  The mod then says that the name moved away (or was released) instead of the usual refusal explanations.
- A server whose key the name is scheduled to move to gets its requests (control, data, dns) refused with `403` and the
  header `X-Kwa-Moving-At: <at>` until then; the mod reads it from the refused WebSocket upgrade, says when it takes the
  name over, and connects then by itself. No DNS-01 record, so no certificate, for that key before the switch.

## 4. Data streams

- A data WebSocket carries the browser's **raw TCP byte stream** in both directions: the browser's TLS records, never
  decrypted by the relay. The game server terminates TLS with the panel's own certificate for `HOST`.
- Only **binary** messages. Message boundaries carry no meaning; the relay may split or join bytes freely. The mod
  sends at most 16 KiB per message. A text message from the relay aborts the stream.
- A WebSocket close from either side means end of stream. The relay closes the browser's TCP connection when the data
  link closes, and closes the data link when the browser's TCP connection closes.
- On a relayed stream the mod never trusts `X-Forwarded-*` headers, even from an address listed as a trusted proxy.
  Host-header checks still apply: browsers must use `HOST`.
- The mod applies its normal connection limits to relayed streams (total, per IP, per-IP connection rate) using the
  announced `ip`. A refused stream is closed at once.

## 5. DNS-01 through the relay

Used when `acme.json` has `challenge: "dns-01"`. The ACME order must name exactly one identifier, equal to `HOST`, and
the relay must be enabled with `relay-key.pem` present.

1. The ACME server gives the mod a `dns-01` challenge for `HOST`. The mod computes the TXT value
   `base64url(SHA-256(keyAuthorization))` (RFC 8555 §8.4), 43 characters.
2. The mod sends `PUT ORIGIN/kiln/webadmin/relay/dns` with the signed headers (kind `dns-add`, id = TXT value) and **no
   body**. The relay creates the TXT record `_acme-challenge.HOST` with that value. Any 2xx answer is success; anything
   else, or no answer within **20 s**, fails the order.
3. The mod waits `acme.json` `dns01PropagationSeconds` (5 to 300, default 60), then asks the ACME server to validate.
4. When the order ends (success or failure), the mod sends `DELETE ORIGIN/kiln/webadmin/relay/dns` (kind
   `dns-remove`, same id, no body). A failure here is only logged. The relay should also expire challenge records on
   its own (for example after an hour), because a server that stops mid-order sends no DELETE.
5. Only one challenge per order. A second dns-01 challenge in one order fails it.

The relay should only accept `dns-add` / `dns-remove` for the signing host's own `_acme-challenge` name, and limit how
often a host may call them (the mod itself makes at most `maxOrdersPerWeek`, default 5, orders a week).

## 6. Discord sign-in through the broker (`v=1`)

### 6.1 Start

The panel sends the browser to:

```
ORIGIN/kiln/auth/discord?v=1&panel=<panelId>&guild=<guildId>&return=<returnUrl>&nonce=<nonce>
```

Each value is percent-encoded (`Uri.EscapeDataString`).

| Parameter | Value |
|---|---|
| `v` | `1` |
| `panel` | this panel's id: 32 lower-case hex, random, kept in `identity-state.json` |
| `guild` | `identity.json` `discord.guildId`: the Discord server (guild) id, 5 to 20 digits |
| `return` | `<panel origin>/api/identity/discord/callback?state=<state>` where `state` is 43 base64url characters |
| `nonce` | 43 base64url characters, one use |

`<panel origin>` is the origin the browser used to reach the panel (scheme, host and port). The broker must keep
`return` byte for byte and must not add or reorder its query.

### 6.2 Answer

The broker completes Discord OAuth itself (scopes `identify` and `guilds.members.read`) and redirects the browser
(302 or 303) to `return` with one more query parameter appended with `&`:

- `&assertion=<JWS>` on success, or
- `&error=<code>` on failure. `error=access_denied` shows "the sign-in was canceled"; any other code shows "the broker
  refused the sign-in". Codes should be short ASCII words (`[a-z_]`); the panel does not show them.

No Discord access or refresh token ever reaches the panel.

The redirect must be a plain top-level navigation (an HTTP 302 or 303, not a form POST or a script): the panel only
accepts the answer in the browser that started the sign-in, which it recognises by a `SameSite=Lax` cookie that
browsers send with such a navigation. (Clarification in mod 0.1.0; the parameters are unchanged.)

### 6.3 The assertion

A compact JWS (`header.payload.signature`, each base64url), at most **8192** characters.

Header (JSON): `{"alg":"EdDSA","typ":"kiln-broker+jws","kid":"<key id>"}`. `kid` is 1 to 64 characters
`[A-Za-z0-9._-]`.

Payload (JSON object, max nesting depth 8):

| Claim | Type | Rule |
|---|---|---|
| `v` | number | `1` |
| `iss` | string | the broker origin, exactly `ORIGIN` without a trailing slash |
| `aud` | string | `"kiln-webadmin"` (a protocol constant from the mod's working name; it stays) |
| `panel` | string | the `panel` parameter |
| `ret` | string | the `return` parameter, exactly |
| `nonce` | string | the `nonce` parameter |
| `iat` | integer | issue time; not more than 60 s ahead of the panel's clock |
| `exp` | integer | expiry; after the panel's current time, after `iat`, and `exp - iat` at most 300 |
| `sub` | string | the Discord user id, 5 to 20 digits |
| `name` | string, optional | the Discord display name (the panel keeps the first 64 characters) |
| `guild` | string | the `guild` parameter |
| `roles` | array of strings | the user's role ids in that guild, each 5 to 20 digits, at most 250 entries; `[]` if none |

- The signature is **Ed25519** (RFC 8032, pure) over the ASCII bytes `<header b64>.<payload b64>`.
- A top-level claim whose name contains `token` (any case) makes the panel refuse the assertion.
- The panel verifies only with keys **pinned in the build** for that exact origin and `kid`. Pins are a JSON array
  passed at build time (`-p:WebAdminBrokerPins=<file>`):
  `[{"origin":"https://frostdev.io","kid":"<kid>","publicKeyHex":"<64 hex>"}]`. Several `kid`s per origin may be
  pinned, which is how the broker rotates keys: pin the new key in a release before signing with it.
- A user who is not a member of the guild: the broker answers `error=not_member` (or any code other than
  `access_denied`). Role filtering is the panel's (`discord.allowedRoles`); the broker reports all roles.
- The assertion only signs in a panel account whose user linked that Discord id earlier. It never creates accounts or
  grants panel roles.

## 7. Launcher handoff (for the launcher's "Open web panel")

Not a hosted-service protocol, but the launcher integration depends on it:
- While the panel listens, the mod writes `kiln/data/kilnwarden/launcher-endpoint.json`:
  `{"format":1,"host":"<address>","port":<port>,"sha256":"<certificate SHA-256 hex>"|null}`. `host` is the bind
  address, or loopback when the bind is all addresses. The file is deleted when the panel stops. `sha256` is null on
  plain HTTP.
- Since 0.1.4 the same file can also hold the panel's address on the internet, still `format` 1 (a launcher that does
  not know the two fields ignores them):
  `"public":"https://<name>.<ZONE>/"` (hosted access) or `"https://<own name>[:<port>]/"` (an own address), and
  `"publicSha256":"<SHA-256 of the trusted certificate served there>"`. Both are written only while that address
  really works, which is what the setup wizard's "Ready" checks: the relay link is up, the name is this server's (not
  unclaimed, moving or moved), the trusted certificate for the name is installed **and** is the one the listener serves,
  and the panel's own check of `https://<address>/` (the name in DNS, through the relay for hosted access, the machine's
  trust store, this panel's certificate, `GET /api/core/health` answering 200) passed for that certificate. An own
  address needs the same checklist for its name. They are never written before the owner account exists, nor while
  the launcher's login is switched off (auth.json `launcherLogin`). They go as soon as any of these stops: the relay
  link drops, the name moves, hosted access is switched off, the certificate changes or stops being served, the
  launcher's login is switched off, or the address check fails. A dropped relay link, a new listener and the owner's
  creation update the file at once, the rest within one round of the panel's checks (15 seconds).
- The address check runs again 5 minutes after it passes, then after 10, 20 and at most 30 minutes while each check
  passes (10 seconds after a failure). Any failure, and any change of the certificate, the relay link (up or down),
  the name or the listener, starts that again at 5 minutes. So an address that stops answering while everything else
  holds goes within about 30½ minutes in the worst case (30 minutes, then the next 15-second round, and a check that
  gets no answer gives up after 12 seconds). Before 0.1.4 a passed check was not repeated on a schedule (nothing used
  it but the setup checklist, and no file named the address). The launcher checks the address itself at every click
  (below), so that gap only costs a few seconds and a fallback to this machine, never a secret.
  A pass counts for the file only until its recheck is a minute overdue, so even a check that never ends leaves the
  address in the file for at most about 31¼ minutes (that minute, then the next round). While there is no owner or
  the launcher's login is off, a passed check is not run again at all (nothing needs it: the file names no address
  then). When that ends, the last pass names the address again if it still counts (its recheck is not yet a minute
  overdue, as at any other time); after a longer pause the address comes back once a new check passes.
- The launcher mints a single-use owner login with Kiln.Admin's KAP op `login_mint` (consumer `kilnwarden`) and
  opens `https://<host>:<port>/launcher#secret=<64 lower-case hex>`. The page posts the secret in a JSON body to
  `POST /api/auth/launcher`; the mod redeems it with `login_redeem` for `kilnwarden`, and once more for the old id
  `kiln-webadmin` when Kiln.Admin refuses the first (launchers made before the rename mint for that id).
- With `public`, a launcher that knows it first checks the address in a few seconds: a TLS handshake that accepts only
  the certificate `publicSha256` names, then `GET /api/auth/status` answering 200 with `ownerExists` and
  `launcherLogin` true. If that passes it opens `<public>launcher#secret=<...>` instead; the same page and the same
  redeem sign the owner in there, through the relay. If the field is missing, the address does not answer in time, or
  it answers with another certificate or anything else, it opens the panel on this machine as before. "Open on this
  computer instead" keeps that route one click away.
- Kiln Launchers before Kiln 0.2.0-dev.31.14 look for the old id (`kiln-webadmin`) and its data folder, so "Open web
  panel" shows for Kilnwarden from Kiln 0.2.0-dev.31.14.

### 7.1 The launcher's login at the public address: threat model (0.1.4)

The secret signs in as the primary owner (two-factor counted as done, no recent proof). `POST /api/auth/launcher`
has never looked at whether a request came through the relay; 0.1.4 keeps that and writes down why, and adds one
refusal.

- **Why setup links stay direct-only (0.1.2), and why this secret is different.** kiln.log's setup link sits in a log
  file people paste into support threads, and it works until the owner exists; whoever reads it could create the owner
  from anywhere. So it works on the server's own computer only (`setup_link_this_machine`). The launcher's secret is
  minted on demand by Kiln.Admin (`login_mint`) for a KAP client holding the admin token, which only someone who can
  already read the server's files has (and that is full control of the server anyway). It is 256 random bits, works
  once (Kiln.Admin checks and forgets it in constant time), lives 60 seconds, is bound to this mod's id (or its old
  one, `kiln-webadmin`, for launchers made before the rename: §7), and neither the launcher nor the mod writes it to a
  log, a command line or a file: the launcher hands the browser a one-use local page that moves it on with
  `location.replace`, the secret stays in the URL fragment (never sent in a request line or Referer), and the panel's
  page removes it from the address bar before posting it in a JSON body. A browser's history may still keep the
  address it opened, with the secret, which is used up by then (and any unused one dies after its 60 seconds).
- **The relay.** It forwards bytes by SNI and never holds the panel's key: TLS ends at the panel, so the relay sees no
  secret. What remains is section 8's: the relay's operator controls the zone's DNS and could get a certificate for
  the name and intercept the browser. The launcher's check pins the panel's own certificate (`publicSha256`) but the
  browser makes its own connection, so an operator who intercepts selectively could take one secret and redeem it
  within its 60 seconds. That gives an owner session without recent proof (Users & Roles, settings and the other
  sensitive actions still need the owner's password or code). It is less than what the owner's password typed at the
  same address already gives such an operator (a reusable credential that does grant recent proof), so it is the trust
  hosted access already asks for, not a new one. An owner who doesn't want it uses "Open on this computer instead".
- **The name pointing elsewhere.** A name that moved to another server's key, was released, or a DNS answer that
  goes somewhere else: the mod takes `public` out as soon as the relay says the name is not this server's, and the
  launcher's check refuses any certificate but this panel's, so the secret is not sent there. A secret sent anyway is
  only redeemable at this panel, once, for 60 seconds.
- **A stale file.** `public` can lag a real change (above); the launcher's own check (pinned, a few seconds) covers
  that gap, and anything but a clean answer falls back to this machine.
- **Guessing and replay.** 256 bits, one use, the sign-in budget per browser address (the relay reports it honestly,
  section 3), and a refused or used secret answers like a wrong one (`401 invalid_login`).
- **Before the owner exists** the launcher's login would be a setup token, which works on the server's own computer
  only. Through the relay it is now refused at once (`403 launcher_setup_this_machine`, the secret used up) instead of
  leading to a setup page the relay then refuses; on this machine nothing changed. The file names no public address
  before the owner exists either. settings.json `remoteSetup` (the old switch that lets this machine's setup links
  work through the relay) still applies.

## 8. Trust, in short

- The relay never holds the panel's TLS key and never decrypts relayed traffic.
- The relay operator can, however, create `_acme-challenge.HOST` records (section 5), so whoever runs the relay could
  obtain a certificate for `HOST` from a public CA. Browsers would then trust a relay that terminated TLS itself. The
  owner's protection is the operator's commitment (frostdev.io never decrypts) and, where the owner controls the DNS
  zone, a CAA record with `accounturi` pinning the panel's own ACME account (`tls/acme/account.json` holds its URL). On
  `kiln.nexus` the zone is the operator's, so only the commitment applies (the Kiln privacy page,
  https://frostdev.io/kiln/privacy, states it). A `kiln.nexus` CAA record forbids wildcard certificates, so no single
  certificate covers every panel name.
- The relay reports browser addresses honestly (section 3); the allowlist and per-IP limits rely on that.

## Change log

| Mod version | Change |
|---|---|
| 0.1.4 | `launcher-endpoint.json` names the panel's working public address and its certificate (`public`, `publicSha256`, §7); the launcher's login through the relay before the owner exists is refused (`launcher_setup_this_machine`, §7.1). No relay or broker change. |
| 0.1.1 | Claim links on the claim pages' own origin, `https://claim.<ZONE>/claim?...` (§1.1). The control link asks for notices (`X-Kwa-Notices: 1`) and takes the name notice (§3.1), ignoring notices it does not know; the `move-undo` request (§2, §3.1); the `403` with `X-Kwa-Moving-At` for a key a name moves to. |
| 0.1.0 | First version: relay `kwa-relay/1` (control, data, dns-add, dns-remove, and claim links, §1.1), broker `v=1`, the launcher handoff. Panel names are `<name>.kiln.nexus`; the relay's API (`ORIGIN`) and the broker are on `https://frostdev.io`. The broker refuses a `return` URL on frostdev.io or its subdomains, at the apex `kiln.nexus`, at a reserved name or deeper than one label under it. |
