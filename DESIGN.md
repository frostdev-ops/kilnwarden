# Kilnwarden: design

**Kilnwarden** (mod id `kilnwarden`; the working name was Kiln Web Admin, `kiln-webadmin`). It is a standalone server-side Kiln mod that serves a full admin
panel over HTTPS from inside the dedicated server process.

## Decisions
| Question | Answer |
|---|---|
| Where it runs | Inside the server only (pure mod, no companion service) |
| Sign-in | Local accounts + roles, Steam, Discord (via frostdev.io's broker; from 0.1.1) |
| Reach | Open port + HTTPS, launcher tunnel, frostdev.io relay (live) |
| Modularity | Public extension API: any mod adds pages, widgets, actions, settings, permissions |
| UI | Shares the launcher's admin pages (one React codebase) |
| v1 extras | Schedules, mod management, audit log + Discord webhooks, live metrics |
| Public page | Yes, optional |
| Delivery | Standalone mod (own repo, own releases) |
| Relay | Encrypted traffic only: SNI passthrough, frostdev.io never decrypts |
| Kiln-side changes | In Kiln itself (section 9); the mod needs Kiln 0.2.0-dev.31.10 or later |
| TLS engine | Native rustls DLL (`kwtls`, versioned file name, static CRT) behind a `TlsServer` seam; SslStream only as a fallback (Wine's SChannel does RSA + TLS 1.2 only and sends no chains) |

## 1. Shape
```
browser ──HTTPS/WSS──► kilnwarden (in the server process, own threads)
                         ├─ HTTP/1.1 + WebSocket server (TcpListener + SslStream)
                         ├─ auth (accounts, roles, Steam, Discord), sessions, CSRF, rate limits
                         ├─ permission gate ─► KAP/1 client on 127.0.0.1 ─► Kiln.Admin op table (game thread)
                         ├─ own modules: schedules, mods, audit, webhooks, metrics, public page
                         └─ extension registry ◄─ other mods (Kilnwarden.Api.dll export)
frostdev.io relay ◄─outbound WSS─ (optional)      launcher ─SSH/KAP tunnel─► (optional)
```
- **It reuses Kiln.Admin and does not duplicate it.** Every live tool (players, map, zones, items, spawn, world, saves, configs,
  crashes, permissions, console) is already a Kiln.Admin op. The web mod speaks **KAP/1 to it over loopback**. It reads
  `kiln/data/kiln-admin/admin-token`, which sits in the same server folder. KAP/1 is documented and versioned, so the mod
  never links Kiln.Admin's internals. The manifest has `dependencies: {kiln: ">=0.2.0-dev.31.10", "kiln-admin": "*"}` and
  `side: "server"`.
- **The panel lives and dies with the server**. While the server is down there is no panel. "Restart" means
  a clean save and exit that the host brings back (section 7).
- **Threads:** the HTTP server runs on its own I/O threads, and game work goes through Kiln.Admin's ops, which already run
  on the tick thread. Sockets, timers and files are released in `OnUnload` (KILN106). A hot reload closes connections
  cleanly.
- **Dependencies:** only Microsoft.NETCore.App. Kiln ships no ASP.NET Core runtime, so the mod brings no Kestrel. It
  has its own small HTTP/1.1 + WebSocket server (about 1.5k lines, fuzz-tested). Storage is JSON and JSONL files under
  `kiln/data/kilnwarden/`, with no SQLite (that would need a native DLL).

## 2. Network and TLS
- **Defaults are safe:** it binds `127.0.0.1:<query port + 100>` until an owner account exists. A public bind is a
  setting with a loud warning.
- **TLS modes:**
  - `self-signed`: generated on first start; the fingerprint is shown in the log and on the launcher.
  - `file`: your own PEM cert and key, reloaded when the files change.
  - `acme`: Let's Encrypt over HTTP-01 on port 80 or TLS-ALPN-01 on the panel port, renewed in process.
  - `off`: allowed on loopback only, or with an explicit "I have a reverse proxy" switch that also turns on trust of
    X-Forwarded-*.
- **TLS engine (chosen after an early spike):** a small native rustls library (`kwtls-<n>.dll`,
  static CRT, versioned name because a native DLL can't be unloaded on hot reload) behind a `TlsServer` seam. Under
  Wine, SslStream/SChannel only does RSA over TLS 1.2, sends no intermediates and ignores protocol limits; it stays as
  a fallback (RSA, TLS 1.2 floor). The shim's glue is fuzzed with the HTTP parser. The shipped DLL is cross-built by the
  repo itself (`native/kwtls/build.sh windows`: cargo xwin like Kiln's release, static CRT, reproducible) with a build
  manifest (sha256, imports, exports, toolchain); KilnDeploy/KilnPack refuse a missing DLL or one its manifest does not
  match, and `native/kwtls/windows-build.sha256` records the expected build of the committed source.
- **Hardening:**
  - HSTS on HTTPS, a strict CSP, SameSite=Strict session cookies plus an Origin check (CSRF).
  - Per-IP and per-account rate limits with backoff lockouts, and a request and frame size cap.
  - Slowloris timeouts.
  - An optional IP allowlist.

## 3. Accounts, sign-in, roles
- **First run:** a one-time setup link (a random token) goes to `kiln.log` and the launcher. Whoever opens it creates the
  owner account. There is no default password, ever.
- **Local accounts:** PBKDF2-SHA256 (600k iterations, built into .NET), optional TOTP 2FA (the owner can require it per
  role), recovery codes, session list with revoke.
- **Steam:** OpenID 2.0 straight to steamcommunity.com, with the panel's own URL as the realm, so no broker is needed. A
  SteamID links to the in-game player, which gives "this is me" on the Players page and optional auto-roles from the
  server's permission groups.
- **Discord:** a fixed redirect URI can't serve thousands of self-hosted panels, so **frostdev.io brokers
  the OAuth**.
  1. The panel sends you to `frostdev.io/kiln/auth/discord?panel=<id>&return=<url>&nonce`.
  2. frostdev.io completes the OAuth and returns a short-lived Ed25519-signed assertion: user id, name, and roles in one
     guild the owner picks (`guilds.members.read`).
  3. The panel checks it with the pinned frostdev.io key.
  No Discord token reaches the panel or stays on frostdev.io, the same rule as the launcher's sign-in. The broker's
  key is pinned in the build (`-p:WebAdminBrokerPins`); release builds pin it from 0.1.1 on, so Discord sign-in works
  from that release.
- **Roles:** built-ins are owner, admin, moderator, viewer, and custom roles can be added. Permissions are per module and
  per action, using Kiln.Admin op names plus extension-declared names (`players.kill`, `spawn.*`, `mymod.edit`).
  Discord guild roles and Steam groups map to panel roles. The gate checks every KAP op and extension action on the
  server side; the UI only hides what you can't use.

### Explicit recent authentication
A session being signed in is not itself recent proof. Password, password + TOTP, recovery-code, setup and launcher
sign-ins all start with `session.reauthenticatedUntil: null` in `GET /api/auth/me`.

- `POST /api/auth/reauth` requires a signed-in, unrestricted session and same-origin JSON containing **exactly one**
  of `{ "password": "..." }` or `{ "code": "123456" }`. The password is the signed-in account's current local
  password. The code must be a fresh code from that account's **already enrolled** TOTP secret; the accepted time
  step is consumed and cannot be used again. Recovery codes and launcher login secrets do not grant recent proof.
- Success is `200 { "ok": true, "reauthenticatedUntil": "<UTC ISO8601>" }`; the same nullable timestamp is exposed by
  `/api/auth/me`. Missing/both proofs give `400 invalid_reauth`; invalid proof gives `401 invalid_credentials`;
  exhausted attempt budgets give `429 rate_limited` with `Retry-After`. A missing, revoked or expired session still
  gives `401` and must sign in again. Proof checks are subject to per-IP and per-account budgets.
- The server grants proof **only to the existing session**, until its own current time plus ten minutes. It is valid
  strictly before that timestamp, never at or after it, and only while the underlying session remains valid. Activity
  does not extend the proof window. Credential/privilege changes invalidate it; session-token rotation clears it.
  Another session of the same account does not inherit it. The gate also refuses a timestamp more than ten minutes
  ahead of its current server clock. The browser clock is not an authorization input.
- Sensitive HTTP writes (Users & Roles, panel settings/module changes and declared sensitive actions) return
  `403 { "error": { "code": "reauth_required", "message": "..." } }` without applying the action. Sensitive KAP ops
  (restart, settings/config writes, backup restore, permission writes, mod/update mutations), plus `command_run` by a
  Console-level web user, return a normal KAP response with `ok: false`, top-level `code: "reauth_required"` and an
  `error` string. The socket remains open and these ops remain in `ready.ops`; ordinary permission refusals remain
  forbidden. The gate checks the live session/proof again before dispatching queued work.
- The UI prompts for proof and then tells the user to **explicitly retry the original action**. Proof completion
  does not queue or replay a sensitive HTTP request or KAP op.

**Launcher sessions:** `/launcher#secret=...` sends its one-time secret only in the JSON body to
`POST /api/auth/launcher`; the fragment is removed, not sent in a request URL or Referer. With an existing owner,
redemption creates a session for the primary owner's real account (`via: "launcher"`, MFA counted as satisfied),
but does **not** grant recent proof. That session can step up with the primary owner's local password or a fresh
code from that owner's enrolled TOTP. The launcher secret is neither an account password nor a reauth credential;
submitting `{ "secret": "..." }` to `/reauth` does not satisfy the contract, and reusing the redeemed launcher link
fails. If the owner cannot supply an account credential, sensitive actions stay blocked: there is no launcher-secret
fallback. Before an owner exists, the launcher grants a setup token instead of bypassing owner creation; like
kiln.log's setup link it works on the server's own computer only (only the server console's hosted link is made for
the relay), so through the relay the launcher's login is refused until the owner exists. With the owner, it works at the panel's hosted or own address too (0.1.4: launcher-endpoint.json
`public`, written only while that address really works; the threat model is docs/protocol-relay-broker.md §7.1).
The panel's check of that address runs again 5 minutes after it passes, then after 10, 20 and at most 30 minutes
while it keeps passing; a failure or a change of the certificate, the relay link, the name or the listener starts it
again at 5 (before 0.1.4 a passed check was not repeated on a schedule). So the worst case for an address that stops
answering while everything else holds is about 30½ minutes before `public` goes; the launcher's own pinned check at
every click covers that gap, so security does not depend on it. While there is no owner or launcherLogin is off, a
passed check is not run again (a failed one still is, for the wizard) and the file names no public address.

## 4. UI: one codebase with the launcher
- **In Kiln:** the launcher's admin pages (`launcher/src/admin/**`) are a package (`@kiln/admin-ui`) with a transport
  seam: an `AdminTransport` interface with two implementations, Tauri (launcher) and WebSocket (web). Both panels get
  every new page. This repo vendors a build of it in `web/vendor/`.
- **The web app** is that package, plus:
  - sign-in screens, an account menu, and a module switcher;
  - the web-only pages: Schedules, Mods, Audit, Webhooks, Metrics, Users & Roles, Panel settings, Public page.
- It is the same Hearth look as the launcher and responsive down to phones; the map gets touch controls.
- **Branding:** the server name, logo, accent colour and favicon are configurable.
- It is built into the mod as hashed static files with brotli pre-compressed, served with immutable caching. It makes
  no CDN calls, and all fonts and map tiles are local or served by the server.

## 5. Modules (everything is one)
Every page, widget and background job is a **module** with an id, an on/off switch, settings (from a JSON Schema,
rendered as a form), declared permissions, and optional dashboard and public-page widgets. The first-party modules use
the same registry that extensions use.

| Module | Source | Notes |
|---|---|---|
| Dashboard | new | widget grid, per-user layout |
| Players, Map, Zones, Items, Spawn, World, Combat, Creatures, Content, Game data, Saves, Configs, Crashes, Permissions, Console | launcher pages over KAP | parity, the same ops |
| **Commands** | Kiln commands over KAP + new | every Kiln command from every mod, grouped by mod, each as a **ready-made form** built from its typed arguments (section 5a) |
| **Schedules** | new | restarts with in-game countdown broadcasts (30/10/5/1 min), timed backups (backup_create), recurring announcements, cron-like or "every N h", skip when the world is empty |
| **Mods** | Kiln.Admin ops + new | list, enable/disable, load/unload/reload, crash reports (existing ops). Install/update from a zip upload or an allowed URL (GitHub releases by default), checksum-verified, **staged for the next start**; Kiln self-update is also staged (see 7) |
| **Audit log** | new | every write op: who, when, op, args (redacted), result; append-only JSONL with rotation, filter and export |
| **Webhooks** | new | Discord (and generic JSON) webhooks per event: join/leave, deaths if Kiln exposes them, crash, restart, schedule warnings, admin actions; templates, per-event toggles, a test button |
| **Metrics** | new + ops | players, server frame/tick time, memory, CPU, save time, mod faults; 1 s live, downsampled history (24 h at 10 s, 30 d at 5 min) in ring files; charts; threshold alerts sent to webhooks |
| **Public page** | new | separate unauthenticated routes, off by default. Each part toggles: online/offline, player count, names, map (player dots off by default), rules (markdown), mod list, join info; cached, no admin data reachable |
| Users & Roles, Panel settings | new | owner and admin |

## 5a. Commands as forms
Kiln commands are typed (Brigadier-style: `[Command]` methods and `Commands.Register` trees, Kiln's docs/kiln/commands.md). The
panel turns every command on the server, Kiln's and every mod's, into a form with no web code from the mod:
- **Catalog:** grouped by mod, searchable, built from `commands_describe` (Kiln.Admin, run as the panel's source). A role
  only sees the commands it may run.
- **Inputs by argument type:**
  - `Player`: a picker of online players; `Players`: multi-select plus selector presets (`@a`, within N m of...).
  - `Entity`: pick from nearby creatures; `Position`: click the map, "at player X", or numbers with relative offsets.
  - `Item`: the ItemPicker from the Items page; `Enum`/`Choice`: dropdowns.
  - `Int`/`Double`: number fields, with sliders when they have a range; `Bool`: a toggle.
  - `Duration`/`TimeOfDay`: time pickers; `Greedy`/`String`: text fields.
  - Optional arguments show their defaults. Subcommands (`zone create`/`zone delete`) are tabs.
- **Run:** a live preview of the line; check it with `command_complete` before sending, then run with `command_run`. The
  output shows inline. "Run as" is the web user by default, or a chosen player (that player's permissions).
- **Permissions:** each command's permission node maps to panel roles. The defaults are Everyone-level commands for
  moderators and up, Admin-level for admins and up, and Console-level for the owner only. Each can be changed per
  command. The gate checks the node, and Kiln.Admin runs the line capped at the level the role was given. Every run goes
  to the audit log under the web user's name.
- **Saved commands:** a filled-in form saved under a name, with an optional confirmation step. Pin it to the dashboard
  as a button, or use it in Schedules ("every day 06:00: /weather clear").
- **Free-text console:** stays for power users, with completion and history (the launcher's console page).
- **For modders:** a mod whose admin features are commands gets a full web UI for free, with no dependency on
  Kilnwarden. The extension API (section 6) is only for richer pages.

## 6. Extension API (public, standalone, no Kiln mod-API bump)
- Ships as the mod's export `Kilnwarden.Api.dll` (manifest `exports`). Mods that want it depend on `kilnwarden`,
  optionally, so they still load without it. It is versioned on its own: semver, a shipped API baseline file and an
  analyzer gate like Kiln's.
- `IWebAdmin` is reached from `OnLoad` via the export:
  - `RegisterModule(spec)`: id, title, icon, permissions, settings schema, default on/off.
  - `AddAction(name, permission, handler, runOn: GameThread|Background)`: the panel and the API call it, the gate checks
    it, and the audit log records it.
  - `AddPage(...)` in one of two kinds:
    - **declarative** (no JS): tables, forms, stat cards and buttons bound to the mod's actions, enough for most mods;
    - **custom**: the mod's own ES-module bundle in a **sandboxed iframe** with a postMessage host API (call action,
      subscribe, theme tokens). Third-party JS never sees the session cookie.
  - `AddWidget(dashboard|public, ...)`, `Publish(topic, payload)` (live updates, webhooks, audit), and
    `Settings<T>()` (typed, hot-reloaded).
- **Templates:** a `dotnet new kilnwarden-module` template, a sample module and the docs page
  (`docs/extension-api.md`).

## 7. Limits of "inside the server only" (and how each is handled)
- **Restart:** save, broadcast, then exit. Under kilnctl's systemd unit Kiln.Admin exits with code 75, which the unit
  restarts. Elsewhere whether the server comes back depends on the host (Pterodactyl, for one, does not restart a clean
  stop). The panel shows "Restart: supported / unknown"; when unknown it offers "Stop" plus a hint.
- **Kiln and mod updates:** downloaded, checksum and signature checked, and **staged**; they are applied at the next start.
  - Mod DLLs that are in use can't be replaced live.
  - **In Kiln:** the core applies a staged mod folder and a staged Kiln update at boot, before mods load,
    keeping the old version for rollback. Kiln.Admin's backup restore already uses the same "staged for the next start"
    idea.
- **No offline view.** The launcher (or the relay page) says "server offline" instead.

## 8. Remote access
- **Launcher tunnel:** the launcher already tunnels KAP over SSH, and it adds the web port. "Open web panel" opens the
  panel and signs in with a one-time login link minted over KAP. The launcher holds the admin token, which counts as
  owner.
- **frostdev.io relay** (opt-in, live): no port forwarding needed. The panel side is in 0.1.0; 0.1.1 adds the 24-hour
  move notice with Undo and the claim pages' own address, claim.kiln.nexus.
  1. The mod dials out over WSS to frostdev.io's relay for `<name>.kiln.nexus`, signing with its own key pair. Panel
     names live on kiln.nexus, not under frostdev.io, so they are never same-site with frostdev.io's apps. The relay's
     API and the Discord broker are on frostdev.io. The apex kiln.nexus and the reserved labels (www, api, relay,
     wiki, ...) are never panels.
  2. Browsers connect to that address. The relay routes by **SNI passthrough**, so it never decrypts admin traffic.
  3. The mod holds a Let's Encrypt cert for its subdomain, obtained by DNS-01 with the relay setting the TXT record.
  4. The owner claims the name on frostdev.io's claim pages, signed in with a Discord account in the Kiln Discord,
     with a claim link the mod signs with its relay key (docs/protocol-relay-broker.md §1.1). One name per account;
     the operator bans names and accounts. frostdev.io keeps the owner's Discord user id per name, nothing else.
  Abuse and capacity limits apply per server.
  **Encrypted traffic only.** The relay never terminates TLS, and there is no fallback mode that decrypts at
  frostdev.io.

## 9. What Kiln provides
The pieces Kilnwarden relies on live in Kiln (0.2.0-dev.31.10 and later), not in this repo:
1. The `@kiln/admin-ui` package and its transport seam (section 4).
2. Staged mod and Kiln updates applied at boot, with rollback (section 7).
3. Kiln.Admin's ops: a `metrics` topic (frame/tick time, memory, CPU), a "restart requested" exit, one-time logins
   (`login_mint` / `login_redeem`) and, for commands as forms (section 5a), `commands_describe` with structured
   argument metadata and `command_run` with a named remote source (`web:<user>`) and a level cap.
None of these is Kiln mod API: they are KAP ops and Kiln internals, and existing launcher callers keep working.
