# Changelog

## 0.1.0 (2026-11-25)

The first release. It needs Kiln 0.2.0-dev.31.10 or later on the server, with the admin runtime component. Players
join with an unmodded game.

Why 0.1.0 and not 1.0.0: this is the first version anyone outside the lab will run, on a Kiln that is itself a dev
build, and some parts have only been checked against stand-ins (Let's Encrypt, Steam, the Discord broker and the
frostdev.io relay). The extension API that other mods compile against has its own version, 1.0.0, and keeps it.

### The name
Test builds called this mod Kiln Web Admin (mod id `kiln-webadmin`). It is **Kilnwarden** now (mod id `kilnwarden`).
- At its first start Kilnwarden moves `kiln/data/kiln-webadmin/` to `kiln/data/kilnwarden/`, with the accounts,
  settings, keys and history. A claimed relay name stays yours (the relay key comes along).
- It stays off, and says so in kiln.log, while the old `kiln/mods/kiln-webadmin/` is still installed: delete that folder.
- The console commands are `kilnwarden setup`, `kilnwarden status` and `kilnwarden claim`; `webadmin ...` still works.
- The extension API is `Kilnwarden.Api.dll` (namespace `Kilnwarden.Api`), still version 1.0.0.
- A one-time login from a Kiln Launcher minted for the old id still works. Today's Kiln Launchers look for the old id,
  so their "Open web panel" button shows for Kilnwarden from Kiln 0.2.0-dev.31.14 (coming soon).

### The panel
- A full admin panel served over HTTPS from inside the dedicated server: no extra program to run, and it answers only
  on the server's own machine until you choose otherwise. TLS 1.3 through its own rustls library (kwtls), with .NET's
  SslStream as the fallback.
- The Kiln Launcher's admin pages (players, map, zones, items, spawn, world, combat, creatures, content, game data,
  saves, configs, permissions, crashes), from Kiln 0.2.0-dev.31.10.
- Overview with widgets each user can reorder and hide.
- Commands: every command on the server, from Kiln and every mod, as a form built from its arguments, plus a console
  with completion and history. Saved commands with a confirmation step, pinned to the overview, used by schedules.
- Player activity: who is on, joins, leaves and deaths, and playtime per player. A linked Steam account marks you as
  yourself.
- Schedules: restarts with in-game countdowns, backups, announcements and saved commands, daily, every N minutes or
  once, optionally skipped when nobody is on.
- Mods: switch a mod on or off for the next start, load, unload and reload now, install or update from a zip or an
  allowed URL (checked by SHA-256 and, with trusted publisher keys, by minisign signature), staged for the next start,
  with one rollback.
- Metrics: players, frame and tick time, memory, CPU, GC pauses, mod faults and the time since the last save; live,
  24 hours and 30 days; alert rules.
- Webhooks to Discord or any HTTPS endpoint: joins, leaves, deaths, restarts, mod faults, crash reports, failed saves,
  schedule runs, metric alerts, admin actions and extension events, with message templates.
- Audit log of every change, filterable and exportable.
- An optional public page (off by default): online or not, player count, names, a map with player dots (rounded, no
  names), rules and join info in markdown, the mod list.
- Panel settings: address and TLS, Let's Encrypt (HTTP-01, TLS-ALPN-01, and DNS-01 through the relay once hosted
  access opens), hosted access (coming soon), sign-in providers, branding (name, colour, logo, favicon), modules on and
  off, map art.

### Accounts and security
- Local accounts with roles (owner, admin, moderator, viewer and your own), per-module and per-action permissions,
  command levels per role, two-factor sign-in with recovery codes, sessions you can see and end.
- A second confirmation (password or code) before sensitive changes; sign-in through Steam for accounts that linked
  it. Discord sign-in (through frostdev.io's broker) is coming soon.
- One-time setup link, no default password. Loopback only until the owner exists. Strict CSP, SameSite cookies, Origin
  checks, rate limits and lockouts, request size and time limits, an IP allowlist, and a reverse-proxy mode.
- Hosted access through the frostdev.io relay (coming soon; the panel side is in this release): the relay forwards
  encrypted traffic and never holds the panel's key.

### For mod authors
- Kilnwarden.Api 1.0.0: modules with permissions, actions, queries, pages of blocks or your own sandboxed page,
  dashboard and public widgets, events and webhooks, typed settings. A `dotnet new kilnwarden-module` template and a
  sample. Mods whose admin features are commands need nothing at all.
