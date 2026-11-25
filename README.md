# Kilnwarden

![Kilnwarden](art/nexus_banner_1920x480.png)

**Kilnwarden is a web admin panel for Enshrouded dedicated servers**, as a [Kiln](https://github.com/frostdev-ops/kiln)
mod. It runs inside your server and serves the panel over HTTPS, so you run the server from any browser, on a phone or
a desktop, with no extra program to install or keep running. Players join with an unmodded game.

It has the same admin pages as the Kiln Launcher, plus accounts and roles, every command as a form, schedules, mod
updates, player activity, metrics, webhooks, an audit log and an optional public page.

![The overview](art/screens/overview.png)

## What you get

- **The Kiln Launcher's admin pages, in a browser:** players, the live map, zones, items, spawning, world and weather,
  combat, creatures, content, game data, saves and backups, configs, permissions and crash reports.
- **Every command as a form:** the commands of Kiln and of every mod on your server, with pickers for players and
  items, sliders for numbers, and switches and dropdowns, checked before they run. A console with completion and
  history for the rest. Save the commands you use often and pin them to the overview.
- **Player activity:** who is on and for how long, joins, leaves and deaths, and playtime per player over 7 and 30 days.
- **Schedules:** restarts with an in-game countdown, backups, announcements and saved commands. Every day at set times,
  every few hours, or once, and skipped when nobody is on if you like.
- **Mods:** switch a mod off or on for the next start, load and reload mods, and install or update them from a zip or
  a release URL. Every package is checked (SHA-256, and minisign signatures from publishers you trust), applied at the
  next start, and can be rolled back once.
- **Metrics:** players, tick and frame time, memory, CPU, GC pauses, mod faults and the time since the last save. Live,
  for 24 hours and for 30 days, with alert rules.
- **Webhooks** to Discord or any HTTPS endpoint: joins, leaves, deaths, restarts, crashes, mod faults, failed saves,
  schedule runs, metric alerts and admin actions, with your own message text.
- **Accounts and roles:** owner, admin, moderator, viewer and roles of your own, with permissions down to single
  commands. Two-factor sign-in with recovery codes, sessions you can see and end, and an audit log of every change.
  Sign in with Steam once you link your account. Discord sign-in is coming soon.
- **A public page if you want one:** whether the server is up, the player count, a map with rough player dots, your
  rules and how to join. It's off until you turn it on, and each part has its own switch.
- **Your look:** your server's name, colour, logo and favicon.
- **For mod authors:** an extension API, so a mod can add its own page, buttons, overview widgets, settings and
  webhook events.

| | |
|---|---|
| ![Commands as forms](art/screens/commands.png) | ![Schedules](art/screens/schedules.png) |
| ![Metrics](art/screens/metrics.png) | ![Users and roles](art/screens/users.png) |
| ![Players](art/screens/players.png) | ![Mods](art/screens/mods.png) |

On a phone, tables become cards and the menu folds away:

<p>
  <img src="art/screens/phone-overview.png" width="260" alt="The overview on a phone">
  <img src="art/screens/phone-commands.png" width="260" alt="A command form on a phone">
</p>

The screenshots use demo data: a test server with made-up players.

## Requirements

- An Enshrouded **dedicated server** with **Kiln 0.2.0-dev.31.10 or later**, including Kiln's admin part (Kiln.Admin,
  installed by default on a server). An older Kiln refuses to load the mod.
- Windows, or Linux with Wine or Proton. The panel works the same on both.
- Players need nothing: Kilnwarden runs only on the server.
- A current browser (Chrome, Edge, Firefox or Safari) on whatever device you use to run the server.

## Install

1. Stop the server.
2. Unzip `kilnwarden-<version>.zip` into the server's `kiln/mods/` folder, so that
   `kiln/mods/kilnwarden/kiln.mod.json` exists.
3. Start the server. `kiln/logs/kiln.log` shows `Kilnwarden <version> started`, the panel's address, the certificate's
   fingerprint and a one-time **setup link**. The address is `https://127.0.0.1:<query port + 100>/`, so
   `https://127.0.0.1:15737/` with the default ports.
4. Open the setup link on the server's machine and create the owner account. The first certificate is self-signed, so
   the browser asks you to accept it: check that the fingerprint it shows is the one in `kiln.log`.
5. Turn on two-factor sign-in in **My account**, and add the people who help you in **Users and roles**.

The panel only answers on the server's own machine until you choose otherwise, so nobody else can reach it before you
have an owner account.

**With the Kiln Launcher**, **Servers > (your server) > Open web panel** opens the panel and signs you in as the owner,
for a local server and for a remote one over its SSH connection. Before an owner exists, it opens the setup page.
The button shows for Kilnwarden from Kiln 0.2.0-dev.31.14 (coming soon): earlier launchers look for the mod's old name.
Until then, use the setup link from `kiln.log`.

No browser on the server's machine, for example a rented game server with FTP only? See the table below.

## Pick your route

How you reach the panel depends on where your server runs.
[docs/hosting-routes.md](docs/hosting-routes.md) has the step-by-step guide for each route, with the log lines you
may see.

| Your server | Route | What you need |
|---|---|---|
| **Home PC or LAN** | Open it on the server's machine, or use the Kiln Launcher. For other devices at home, set the bind address and add an IP allowlist for your network. | Nothing extra; for other devices, one open port on that PC |
| **VPS or cloud machine** | The Kiln Launcher over SSH (nothing open but SSH), or your own address: open the port and get a free Let's Encrypt certificate from the panel (HTTP-01 on port 80, TLS-ALPN-01 on port 443) or use your own certificate files | SSH, or an open TCP port and a name pointing at the server (a free dynamic DNS name works) |
| **Rented game server** (FTP and a web console only) | Hosted access at `https://<name>.kiln.nexus` (coming soon). With one extra port from your host: direct HTTPS with a self-signed certificate. | File access over FTP/SFTP; outbound HTTPS from the server |
| **Docker or Pterodactyl** | A published port (an allocation) with `bind` set to `0.0.0.0` inside the container, or hosted access (coming soon) | One more TCP port, or outbound HTTPS only; the data folder on the persistent volume |
| **Behind CGNAT, or IPv6 only** | Hosted access (coming soon). With IPv6 only, a direct IPv6 address also works, but IPv4-only visitors can't reach it. | Outbound HTTPS (IPv4 or IPv6) |

The Kiln Launcher routes need Kiln 0.2.0-dev.31.14 or newer (coming soon; see Install). You can also put the panel behind your own reverse
proxy: turn its TLS off and mark the proxy as trusted.

For the first sign-in on a server you can only reach over FTP, the setup link is in `kiln/logs/kiln.log`, or comes
from the console command `kilnwarden setup` through Kiln's console folder (`kiln/control/console/`). Set
`"remoteSetup": true` in `kiln/data/kilnwarden/settings.json` to open the setup page to other machines before the
owner exists; it stays off unless you turn it on.

## Security in plain words

- **No default password.** The owner account is made with a one-time setup link from the server's log or console.
- **It starts closed.** The panel listens only on the server's own machine until there is an owner and you open it up.
  A public address makes the log warn loudly.
- **Strong sign-in.** Passwords are stored as PBKDF2 hashes (600,000 rounds). Two-factor sign-in, with recovery
  codes, can be required per role. A second password or code check comes before sensitive changes.
- **Least privilege.** Each role sees and does only what it is allowed to, down to single commands. Moderators can't
  run admin commands, and the console is owner-only by default.
- **Hard to attack from a browser.** HTTPS with HSTS, a strict content security policy, SameSite cookies, Origin
  checks, rate limits and lockouts, request size and time limits, and an optional IP allowlist.
- **Everything is written down.** The audit log records every change and who made it.
- **Checked packages.** Mod packages are checked by SHA-256, and by minisign signature against keys you trust. You
  can require signatures.
- **Your data stays on your server.** Accounts, logs, metrics and settings are in `kiln/data/kilnwarden/`. Secret
  files there (accounts, sessions, keys) are written owner-only. The panel only calls out to what you turn on: Let's Encrypt,
  Steam, the Discord sign-in broker, the relay, your webhooks, and the URLs you install mods from.

Before release, the code had a security review and fuzz testing. If you find a problem, please report
it privately: see [SECURITY.md](SECURITY.md).

## Hosted access on kiln.nexus (coming soon)

Hosted access gives your panel an address like `https://myserver.kiln.nexus` with no port forwarding. Your server
keeps one outbound connection to frostdev.io's relay on port 443, and the relay hands each browser connection to it.
That is what makes it work on rented game servers, behind CGNAT, with IPv6 only and in containers.

- **frostdev never decrypts your traffic.** The relay reads only the name the browser asks for and passes the
  encrypted bytes through. TLS ends inside your game server, with your panel's own Let's Encrypt certificate, whose
  key never leaves your server, so the relay has nothing to decrypt with.
- **You claim your name with Discord.** In **Panel settings > Hosted access** (or with the console command
  `kilnwarden claim`), the panel makes a one-time claim link signed with its own key. Open it, sign in with a Discord
  account that is a member of the Kiln Discord, and claim the name. Each Discord account holds one name.
- **What the relay sees:** the panel's name, your visitors' IP addresses, when they connect, and how much data flows.
  It keeps counts, not addresses. One limit to know: the relay's operator controls the DNS for `kiln.nexus`, so in
  principle it could get a certificate for your name. frostdev commits not to, and the Kiln privacy page will say so
  before hosted access opens.

![Hosted access in Panel settings: the name and a claim link](art/screens/hosted-access.png)

The panel side is in this release. The relay and the claim pages go live soon; we'll announce it on the Kiln Discord.
Until then, use one of the other routes.

## Privacy

Kilnwarden keeps its data on your server. frostdev.io only takes part if you turn on hosted access or Discord sign-in
(both coming soon). What frostdev.io sees and keeps for them will be on the Kiln privacy page before they open:
**[frostdev.io/kiln/privacy](https://frostdev.io/kiln/privacy)**.

## For mod authors: the extension API

If your mod's admin features are commands, they already show up as forms on the Commands page: nothing to do.

For more, the panel exports `Kilnwarden.Api.dll` (its own version, 1.0.0). With it, your mod can add a page (made of
blocks, or your own script in a sandboxed frame), actions, overview widgets, typed settings, live events and webhook
events. Declare Kilnwarden as an **optional** dependency, so your mod still loads without it.

```sh
dotnet new install <this repo>/templates/kilnwarden-module
dotnet new kilnwarden-module -n Lighthouse --moduleId lighthouse --modId lighthouse-panel
```

Read [docs/extension-api.md](docs/extension-api.md) and see the sample in `samples/Kilnwarden.Sample`.

## Building from source

You need .NET 10, [Bun](https://bun.sh), Rust with `cargo-xwin` and LLVM (for the Windows TLS library), and the
`sdk/` folder of the Kiln release that `global.json` names (0.2.0-dev.31.10).

```sh
bash tools/link-sdk.sh /path/to/kiln-0.2.0-dev.31.10/sdk
bash native/kwtls/build.sh all              # the TLS library: dist/kwtls-1.0.0.dll and a host build for the tests
(cd web && bun install --frozen-lockfile && bun run build && bun run test)
dotnet test tests/Offline -c Release
dotnet build src/Kilnwarden -c Release -t:KilnPack
```

The pack writes `bin/Release/kilnwarden-<version>.zip` and prints its SHA-256. It refuses a build without the web app
or the TLS library, or with a TLS library that isn't the recorded build of its source
(`native/kwtls/windows-build.sha256`). The zip is reproducible: `tools/release/repro.sh [commit]` builds a commit twice
from clean exports and compares the bytes. Set `SOURCE_DATE_EPOCH=$(git log -1 --format=%ct)` to get the same zip.

Discord sign-in needs the broker's public key built in: add `-p:WebAdminBrokerPins=/absolute/path/to/pins.json` to
the build and pack commands.

Other checks:
- Browser tests: `cd web && KILN_UI_WORKTREE=<a Kiln checkout> bunx playwright test` (a dev host and a mock Kiln.Admin,
  no game needed).
- Fuzzing: `dotnet run -c Release --project tests/Fuzz -- <target|all> <seconds>`.

How it's built: [DESIGN.md](DESIGN.md). What the mod sends to the relay and the sign-in broker:
[docs/protocol-relay-broker.md](docs/protocol-relay-broker.md).

## License and credits

Kilnwarden is MIT licensed: see [LICENSE](LICENSE). It includes third-party code and fonts under their own licenses,
listed in [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt):

- the Kiln Launcher's admin pages (`@kiln/admin-ui`, MIT), whose live map builds on the
  [Enshrouded Interactive Map](https://github.com/nicolasferreiraxn/Enshrouded-Interactive-Map) by Nicolas Ferreira (MIT)
  and [Leaflet](https://leafletjs.com) (BSD-2);
- [rustls](https://github.com/rustls/rustls) and its dependencies for TLS;
- React, TanStack Query and Virtual, React Router, Zustand, Lucide icons and qrcode-generator;
- the Alegreya Sans, Grenze Gotisch and Inconsolata fonts (SIL Open Font License).

The map's terrain is the game's own art, so it isn't in the zip. Copy the Kiln Launcher's game art into
`kiln/data/kilnwarden/map-assets/` to see it. Players, markers and zones show without it.

Made by Frostdev. Kilnwarden is unofficial and not affiliated with or endorsed by Keen Games.
