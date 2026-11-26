# Changelog

## 0.1.4 (2026-11-26)

The Kiln Launcher's **Open web panel** opens your hosted address once it works, and dialogs over the Map page take your
clicks again.

### The Kiln Launcher
- **Open web panel opens your panel at its hosted address** (`https://<name>.kiln.nexus`, or your own web address),
  signed in, once that address really works: the relay connected, the name yours, its certificate in place, your owner
  account created, and the panel's own check of the address passing. Until then, and as soon as any of that stops, the
  launcher opens the panel on your computer, as before; it also checks the address itself before every open. This
  needs Kiln 0.2.0-dev.31.19 or later; older launchers keep opening the panel on your computer.
- The launcher's one-time sign-in is the same at that address: it works once, for one minute. Before your owner account
  exists it is refused at the hosted address, in plain words: create the owner on the server's own computer first
  (setup links only work there). docs/protocol-relay-broker.md §7.1 explains why this is safe.
- The panel keeps checking its hosted address while it works (0.1.3 stopped at the first check that passed): 5 minutes
  after a check passes, then after 10, 20 and at most 30 minutes, about 50 checks a day through the relay. Any failure,
  or a change of the certificate, the relay link, the name or the listener, starts again at 5 minutes. So the setup
  checklist's **Ready** (the wizard, `kilnwarden status`) now also shows when a working address stops answering. Such
  an address may stay on offer to the launcher for up to about half an hour, but the launcher checks it at every click
  and otherwise opens the panel on your computer.
- With no owner account yet, or with the launcher's sign-in switched off (`launcherLogin` in auth.json), the panel
  offers the launcher no address, and a check that passed isn't repeated (one that failed still is, every 10 seconds).

### The Map page
- **Dialogs over the map take your clicks.** Where a dialog lay over the map (the identity prompt, the map's own
  confirmations such as **Delete waypoint?**), the map's layers sat above it and took every click, so the dialog could
  not be used. The account menu, and the page menu on a phone, had the same problem over the map. Every dialog and
  menu now sits above the map, and above whatever any page shows.
- **Copy it from the Kiln Launcher** no longer asks for your password again: one click copies the map art. It is still
  for the server's owner only, and as before it reads only the launcher's own folder, copies only the map's files
  within the size limits, never follows a link, and moves the art in only once the copy is complete. After a copy that
  fails, the next can start half a minute later.

## 0.1.3 (2026-11-26)

The setup wizard, polished: Discord sign-in for you, then mods and your dashboard. The Map page fills the screen and
says plainly when it has no map art.

### Setup
- **Sign in with Discord** is about you first: one button links your own Discord, and from then on **Sign in with
  Discord** works for you; your password keeps working. It switches Discord sign-in on for owners only, checking the
  Discord server you named, or else the Kiln Discord. If Discord sign-in (frostdev.io) isn't answering, the page says
  so, nothing changes, and you can skip. The link is the panel's usual one: it asks you to confirm it's you, only the
  broker's built-in keys are trusted, and a Discord account already linked to someone else is refused.
- Then, optionally, **let others sign in with Discord too**: your Discord server's ID, as before.
- **Add mods to your server** (optional): what is installed, then upload a mod's zip or paste a link with its SHA-256.
  It is the Mods page's own check (allowed sources, the SHA-256, signatures) and installs at the next restart, with
  **Restart now** where the panel can restart the server.
- **Customize your dashboard** (optional): your overview's cards as a live preview; drag them into order, switch off
  what you don't need, or **Use the default**.
- Every page has **Back**, and the optional ones **Skip**, in the same place; the dots count the pages there are. The
  end lists what was set up and what was skipped, each with where to change it later. A restart comes back to the same
  page, the new ones too.

### The panel
- **Overview > Customize** is drag and drop now, the same as the wizard's: a card's handle drags it with a mouse or a
  finger, or with the keyboard (Space picks it up, the arrow keys move it, Space drops it, Escape puts it back), and
  screen readers hear each move. A switched-off card stays in its place, folded.
- **The Map page** fills the panel's height whatever its side panel shows (Markers, Find & filter, Live; before, the map
  grew and shrank with them), the side panel scrolls on its own, and on a phone it sits above the map instead of
  squeezing it.
- **No map art:** the Map page says so plainly, never "Preparing map art…". When the Kiln Launcher on the same computer
  has made its game art, **Copy it from the Kiln Launcher** copies it in one click (only from the launcher's own folder);
  otherwise the steps are behind **How to add it**.
- **Panel settings > Sign-in** shows and switches **Owners only** for Discord sign-in. With it on, a panel role mapped
  from Discord roles counts for owners only, and a helper's Discord link shows as switched off.
- A map art copy that fails says so in plain words (the server's log has the details), and a copy cut short by a crash
  leaves nothing behind after the next start.

## 0.1.2 (2026-11-26)

Setting up is now a step-by-step wizard, one question per page, and nothing has to be edited by hand.

### Setup
- **The first start says one thing**, in three lines in kiln.log and the server console: open the setup link on this
  computer, or, on a server with no browser, type `kilnwarden setup hosted <name>` in its console.
- **A setup wizard:** your account, then how you will open your panel (hosted access at `<name>.kiln.nexus` is
  preselected; the Kiln Launcher; or your own web address). For hosted access: pick a name (checked as you type), press
  **Claim with Discord**, and watch a checklist (Connected, Claimed, Secure, Ready) until your panel answers at its
  address. When a step fails, it says why and what to do. Optional Discord sign-in for helpers, then the address to
  bookmark. The wizard writes every setting itself (hosted access, the certificate's settings), comes back to where you
  were after a restart, and **Panel settings > Run the setup again** starts it over.
- **From the server console alone** (a rented server): `kilnwarden setup hosted <name>` sets up hosted access and gives
  the claim link; `kilnwarden setup` then gives the link to create your account at `https://<name>.kiln.nexus`, once
  the name has its certificate. `kilnwarden status` is a checklist of what is done and what to do next.
- **The link in kiln.log only works on the server's own computer** (unless you set `remoteSetup`, as before): a log you
  share for help never lets anyone create the account over the internet. The console's account link works once, for 30
  minutes, and is never written to kiln.log.
- The certificate for a hosted name is ordered as soon as the name is claimed, not before (an order before the claim
  failed and waited an hour).
- The page on the server's own computer (`https://127.0.0.1:<port>`) keeps its certificate when the Let's Encrypt one
  arrives, so it keeps working; your panel's name gets the new certificate. The Kiln Launcher's pin stays the same.

### Leftovers from the old name
- While `kiln/mods/kiln-webadmin` (Kiln Web Admin, the old name) keeps Kilnwarden off, kiln.log, every console answer
  and the panel's own address say exactly which folder to delete.

### The panel
- Panel settings > Hosted access shows less at once: the relay's address, the public key and the long explanations
  are behind **Details**; the key to compare with the claim page shows with the claim link.

## 0.1.1 (2026-11-26)

Hosted access is live, and so is Discord sign-in. The relay, the claim pages at `https://claim.kiln.nexus` and the
landing page at `https://kiln.nexus` are open. A name that is in use now moves or goes only after 24 hours, and the
server it is on can undo that.

kiln.nexus is frostdev's own domain for Kiln. It has no affiliation with Nexus Mods (nexusmods.com). Panel settings >
Hosted access, the claim pages and kiln.nexus itself now say so.

### Hosted access and Discord sign-in
- Hosted access at `https://<name>.kiln.nexus` is live: no port forwarding, one outbound connection from your server on
  port 443, and the relay passes the encrypted traffic through without decrypting it. Claim your name with Discord
  (below). Panels from 0.1.0 work with it too, but only 0.1.1 shows the move notice and its Undo.
- Discord sign-in works: this build pins the keys of frostdev.io's sign-in broker (0.1.0 had none, so its Discord
  sign-in stays off). Switch it on in Panel settings > Sign-in, then link Discord in My account.
- What frostdev.io sees and keeps for both is on the Kiln privacy page,
  [frostdev.io/kiln/privacy](https://frostdev.io/kiln/privacy).

### Claiming a name
- Claim links (Panel settings > Hosted access > **Make a claim link**, and the console's `kilnwarden claim`) now open
  the claim pages on an address of their own, `https://claim.kiln.nexus` (`claim.` and the zone of your name). The
  claim pages share it with nothing else. Links that 0.1.0 made are sent there too.
- Moving a name that is in use to another server, or releasing it, now waits **24 hours**. Until then the name still
  reaches the server it is on. That server's panel shows a notice on every page, with **Undo** for the owner (after
  confirming it is you), says it in kiln.log, and `kilnwarden status` shows it. Undo keeps the name on that server, with
  that server's own key, and unties it from the Discord account that asked; so someone who got into your Discord account
  can't take your name in one go, and can't simply ask again. You can also cancel the move or release on the claim
  pages. If the old server is gone for good, the move still happens after the 24 hours.
- The server a name moves to says in Panel settings and kiln.log when it takes the name over, and connects then by
  itself. The server it leaves says that the name moved away.

### The Kiln Launcher
- "Open web panel" on your server's page opens Kilnwarden and signs you in as the owner with Kiln 0.2.0-dev.31.14 or
  newer, which is out now: update the launcher if the button doesn't show. Older launchers look for the mod's old name.

### Under the hood
- The panel takes notices about its name on its relay connection, and ignores notices it does not know, so a later
  relay's new notices never drop the connection. A 0.1.0 panel gets no notices; the relay still keeps the 24 hours for it.

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
  so their "Open web panel" button shows for Kilnwarden from Kiln 0.2.0-dev.31.14.

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
- Panel settings: address and TLS, Let's Encrypt (HTTP-01, TLS-ALPN-01, and DNS-01 through the relay),
  hosted access, sign-in providers, branding (name, colour, logo, favicon), modules on and
  off, map art.

### Accounts and security
- Local accounts with roles (owner, admin, moderator, viewer and your own), per-module and per-action permissions,
  command levels per role, two-factor sign-in with recovery codes, sessions you can see and end.
- A second confirmation (password or code) before sensitive changes; sign-in through Steam for accounts that linked
  it. Discord sign-in through frostdev.io's broker (it works from 0.1.1, the first build with the broker's keys).
- One-time setup link, no default password. Loopback only until the owner exists. Strict CSP, SameSite cookies, Origin
  checks, rate limits and lockouts, request size and time limits, an IP allowlist, and a reverse-proxy mode.
- Hosted access through the frostdev.io relay (the panel side; the relay opened with 0.1.1): the relay forwards
  encrypted traffic and never holds the panel's key.

### For mod authors
- Kilnwarden.Api 1.0.0: modules with permissions, actions, queries, pages of blocks or your own sandboxed page,
  dashboard and public widgets, events and webhooks, typed settings. A `dotnet new kilnwarden-module` template and a
  sample. Mods whose admin features are commands need nothing at all.
