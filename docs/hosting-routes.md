# Reaching the panel: a route for every kind of server

Kilnwarden runs inside your dedicated server. How you reach it depends on where that server runs. This page has
one section per setup: the steps (plain words, for the wiki and the README), what the log says when something is
wrong, and who can reach what. Everything the panel keeps is in `kiln/data/kilnwarden/` in the server folder; the
log is `kiln/logs/kiln.log`.

| Your server | Route | What you need |
|---|---|---|
| Rented game server: FTP and a web console, no SSH, maybe no extra port | **Hosted access**: `https://<name>.kiln.nexus` through frostdev.io's relay. Or **direct HTTPS** on one extra port from your host | file access (FTP/SFTP), outbound HTTPS (TCP 443) from the server, a Discord account in the Kiln Discord to claim the name; or one more TCP port |
| VPS or cloud VM, or a home server with a public IP | **Your own address** (direct HTTPS on an open port), or **the Kiln Launcher over SSH**, or hosted access | an open TCP port and a name with a real certificate; or SSH; or as above |
| Docker, Pterodactyl | **A published port**, or **hosted access** | one more TCP port (allocation), `bind` 0.0.0.0 inside the container; or outbound TCP 443 |
| Home server behind CGNAT, or with IPv6 only | **Hosted access**; with IPv6, direct IPv6 | outbound TCP 443 (IPv4 or IPv6) |

**Hosted access is live.** frostdev.io's relay and its claim pages at `https://claim.kiln.nexus` are open. Panels from
Kilnwarden 0.1.0 work with it; 0.1.1 adds the 24-hour move notice with Undo (section 1). Sections 1 and 2C describe how
to set it up.

**kiln.nexus is frostdev's own domain for Kiln. It has no affiliation with Nexus Mods (nexusmods.com).**

Hosted access never decrypts: the relay reads only the name your browser asks for (the TLS ClientHello's server
name) and passes the encrypted bytes on. TLS ends inside your game server, with the panel's own Let's Encrypt
certificate for `<name>.kiln.nexus`, whose key never leaves the server.

**The Kiln Launcher's "Open web panel"** (sections 2A and 2C) needs Kiln 0.2.0-dev.31.14 or newer: earlier launchers
look for the mod's old name. With an older launcher, use the setup link from `kiln.log` and an SSH tunnel.

## 1. A rented game server (FTP and the host's web console): hosted access

Without hosted access, a rented server needs one extra TCP port from the host: set `bind` and `port` as in section 2B
and open the panel with its self-signed certificate. The first sign-in works the same way as step 6 below.

The server dials out to `frostdev.io` on port 443 (a WebSocket) and keeps that link open. Browsers open
`https://<name>.kiln.nexus`; the relay hands each connection to your server over that link. No inbound port, no port
forwarding, works behind the host's NAT and when only outbound HTTPS is allowed. The panel itself stays on the
server's loopback address.

### Steps

1. **Install.** Stop the server. Over FTP, put Kiln's server pack and Kilnwarden into the server folder
   (`kiln/mods/kilnwarden/kiln.mod.json` must exist). Start the server once, so the panel makes its data folder,
   then stop it.
2. **Pick a name**: 3 to 40 of `a-z`, `0-9` and `-`, not starting or ending with `-`. Some names are reserved
   (`www`, `api`, `relay`, `panel`, `admin`, `app`, `auth`, `mail`, `docs`, `wiki`, `status`, `help`, `support`,
   `cdn`, `dev`, `beta`, `kiln`, `frostdev`, `discord`, `nexus` and a few more) and `kiln.nexus` itself is never a
   panel. Below, `myserver` stands for your name.
3. **Edit three files** in `kiln/data/kilnwarden/` over FTP (create them if they are missing; keep any other
   settings they hold):
   - `relay.json`: `{"enabled": true, "origin": "https://frostdev.io", "host": "myserver.kiln.nexus"}`
   - `settings.json`: `"tls": {"mode": "acme", "hostnames": ["myserver.kiln.nexus"]}` and `"remoteSetup": true`.
     Leave `bind` at `127.0.0.1`: nothing needs to listen outside the server.
   - `acme.json`: `{"format": 1, "acceptTerms": true, "email": "you@example.com", "challenge": "dns-01", "names": ["myserver.kiln.nexus"]}`
     (`acceptTerms` accepts Let's Encrypt's terms; the email is optional).
4. **Start the server, then claim the name** with your Discord account (you need to be a member of the Kiln Discord
   server; one name per account). Get a claim link from the server's console: upload a text file holding
   `kilnwarden claim` as `kiln/control/console/claim.txt`, rename it to `claim.cmd`, and download `claim.result.json` a
   few seconds later. Open the link in its answer yourself (it works once, for 15 minutes; whoever opens it first can
   claim the name for your server), sign in with Discord on the claim pages (`https://claim.kiln.nexus`), check the name and the key
   fingerprint, and press **Claim**. Until then the log says the relay does not know the name; the panel keeps trying
   every 4 seconds. (If you can already open the panel, Panel settings > Hosted access > **Make a claim link** does the
   same. Or send `kiln/data/kilnwarden/relay-public.pem` with the name you want to the relay's operator: see
   "Registration" below.)
5. **Once the name is claimed**, the next try connects (`Kilnwarden relay connected for myserver.kiln.nexus`)
   and the certificate follows by DNS-01 within a few minutes (`ACME: new certificate for myserver.kiln.nexus`). A
   claimed name waits an hour for the server's first connection, then lapses.
6. **The first sign-in.** Get the one-time setup link, either way:
   - from `kiln/logs/kiln.log` (download it over FTP; search for `setup#token=`). With `remoteSetup` on, the line
     says "from any machine" and names `https://myserver.kiln.nexus/setup#token=...` first;
   - or with a console command: upload a text file holding `kilnwarden setup` as `kiln/control/console/setup.txt`,
     rename it to `setup.cmd`, and download `setup.result.json` a few seconds later. Its link works once, for an
     hour. (`kilnwarden status` says where the panel listens, its certificate, whether the owner exists and what the
     relay is doing.)
   Open the link in any browser, choose the owner's name and password, and you are in.
7. In **My account**, turn on two-factor sign-in. `remoteSetup` has no effect once the owner exists; you may remove it.

The Kiln Launcher's "Open web panel" needs the server on your PC or reachable over SSH, so for a rented server just
open `https://myserver.kiln.nexus` in your browser and sign in with your panel account.

### What the log says (`kiln/logs/kiln.log`; Panel settings > Hosted access shows the same)

| Line | Meaning, what to do |
|---|---|
| `relay for myserver.kiln.nexus: the relay does not know myserver.kiln.nexus for this server's key: claim the name (Panel settings > Hosted access > Claim this name, or the console command kilnwarden claim; or send relay-public.pem to the relay's operator), or it was released or banned (relay HTTP 403)` | Not claimed yet (step 4), the claim lapsed (no connection within an hour: claim again), the key changed (you deleted `relay-key.pem`: a claim link from the new key moves your name to it), or the name was released or banned. |
| The claim page: "This claim link was used already", "has expired", "the name service restarted" | Each link works once, for 15 minutes, and not across a relay restart: run `kilnwarden claim` again for a new one. |
| The claim page: "This account already has <name>" | One name per Discord account: release the other on [Your panel name](https://claim.kiln.nexus/) first (a name being released no longer counts). |
| A notice on every page: "This panel's name ... is being moved to another server on ..." | Someone signed in with the Discord account that holds the name asked to move it. Not you? **Undo** (the owner), then secure that Discord account. |
| `relay for myserver.kiln.nexus: myserver.kiln.nexus is moving to this server's key: it switches on ...` | This server is the new home of a name in its 24-hour move. Nothing to do: it connects then. |
| `relay: myserver.kiln.nexus moved to another server's key on ...` | The name moved away (asked on the claim pages, not undone within 24 hours). Not you? Ask the relay's operator on the Kiln Discord. |
| The claim page: "<name> is taken" or "is reserved" | Pick another name (`relay.json` `host`, and the same in `settings.json` `tls.hostnames` and `acme.json` `names`), restart, make a new link. |
| The claim page: "Only for members of the Kiln Discord" | Join the Kiln Discord server with that account, then sign in again. |
| `... the relay refused this server's signature: the server's clock is off by more than two minutes, or the relay restarted a moment ago (retrying) (relay HTTP 401)` | Fix the server's clock (ask the host); a relay restart clears by itself. |
| `... cannot connect to frostdev.io:443: outbound HTTPS is blocked here or the network is down` / `... within 8 s: outbound HTTPS is blocked (dropped) here` | The host blocks outbound TCP 443. Ask them to allow it to frostdev.io. |
| `... cannot look up frostdev.io:443: this server's DNS does not answer for it` | The server's DNS is broken or filtered. |
| `... TLS to frostdev.io:443 failed: something between this server and the relay intercepts HTTPS, or this server's clock or certificate store is wrong` | An intercepting proxy or firewall, or a wrong clock. |
| `... the relay is limiting this address` (429), `... the relay is full or restarting` (503) | The relay's limits (many servers behind one address) or maintenance; it retries. |
| `relay for ...: relay-key.pem cannot be read` | Delete `relay-key.pem` and `relay-public.pem`, restart, register the new public key. |
| `no owner account yet. Open this link on this machine ...` | `remoteSetup` is not on: the setup only opens on the server's own machine. Set it (step 3) and restart, or use `kilnwarden setup` after setting it. |
| `REMOTE SETUP: no owner account yet and settings.json remoteSetup is on ...` | Expected until you create the owner (step 6). |
| Panel settings > Certificate: `dns-01 needs the enabled relay and its registered hostname as the only ACME name` | `acme.json` `names` and `relay.json` `host` must be the same one name. |
| `no owner account yet (settings.json remoteSetup orders before it)` | The certificate waits for the owner; turn `remoteSetup` on to get it before. |
| The browser: "connection closed" / "can't be reached" at `https://myserver.kiln.nexus` | The server is offline, the name is not registered, or not connected yet: the relay closes the connection without answering. |
| The browser warns about the certificate | The Let's Encrypt certificate is not there yet (the panel serves its self-signed one meanwhile). Wait for `ACME: new certificate`; check `acme.json`. |
| `the setup link is wrong or was used` | Each link works once; get a new one with `kilnwarden setup` (or restart for a new one in the log). |

### Who can reach what

- Anyone on the internet can open `https://myserver.kiln.nexus` and see the sign-in page (and, before the owner
  exists with `remoteSetup` on, the setup page). Only the one-time link creates the owner: it is in `kiln.log` and
  in the console's answer file, which only someone with your FTP or console access can read, and they control the
  server anyway. Sign-in limits, lockouts, two-factor and the IP allowlist apply as everywhere; the relay passes the
  browser's real address to the panel.
- frostdev.io sees which name is asked for, when, how many bytes, and the visitor's address. The relay keeps counts,
  not addresses. nginx in front of it records a visitor's address only when a connection fails, and keeps it 14 days.
  It never sees inside the TLS connection. Details: [the Kiln privacy page](https://frostdev.io/kiln/privacy).
- The relay's operator can create the `_acme-challenge` record for your name (that is how your certificate is
  issued), so the operator could in principle get a certificate for `myserver.kiln.nexus` too. We commit not to, and
  the [Kiln privacy page](https://frostdev.io/kiln/privacy) says so. A `kiln.nexus` CAA record forbids wildcard
  certificates, so no single certificate can cover every panel's name. You can't add a CAA record of your own: the
  zone is the operator's.
- Limits per server: 20 browser connections at once (10 per visitor address), 4 MiB/s, 8 GiB a day.
- The claim: frostdev.io keeps your Discord user ID with the name (for the one-name rule and bans), nothing else. Your
  name is yours to release or to move to a new key on [Your panel name](https://claim.kiln.nexus/);
  names nobody connects with for 90 days are released.
- Moving or releasing a name in use waits 24 hours, and your server's panel can undo it (below): someone who gets into
  your Discord account can't take the name from your server in one go.

### Registration

**Claim the name.** Members of the Kiln Discord server claim a name for their server themselves on the claim pages,
`https://claim.kiln.nexus` (an address of their own; Kilnwarden 0.1.1 and later link there, and 0.1.0's links are sent
there too):
1. The panel makes a claim link: Panel settings > Hosted access > **Make a claim link** (the owner, after confirming
   their password), or the console command `kilnwarden claim` (its answer file). The link carries the server's public key
   and the name from `relay.json` `host`, signed with the server's private relay key, so only the server's owner can
   make one. It works once, for 15 minutes.
2. Open it yourself, sign in with Discord (Discord's consent page; frostdev.io reads your user ID, display name and
   membership of the Kiln Discord, then revokes its access), check the name and the key fingerprint (Hosted access
   shows the same one), press **Claim**.
3. The name is yours, held for an hour until the server connects, then live. One name per Discord account.

**Your panel name** (`https://claim.kiln.nexus/`, signed in with Discord): the name, whether the server is connected,
its key fingerprint, what is scheduled for it (with **Cancel**); **Release** (type the name to confirm); and to move it
to a new key (a reinstalled server, a new host, a deleted `relay-key.pem`), make a claim link for the same name on the
server with the new key and open it signed in to the same account.

**Moves and releases wait 24 hours** once the name is in use (a name whose server never connected moves or goes at
once):
- Until then the name keeps reaching the server it is on. That server's panel (Kilnwarden 0.1.1 or later) shows a
  notice on every page, "This panel's name ... is being moved to another server on ... Not you?", with **Undo** for the
  owner (after confirming it is you); kiln.log and `kilnwarden status` say it too.
- **Undo** keeps the name on that server, signed with its own relay key (a Discord account alone can't undo, or stop
  the undo), and unties the name from the Discord account that asked, which then can't simply ask again. To manage the
  name on the claim pages again, make a claim link on that server and claim it with a Discord account you trust. If you
  did not ask for the move, secure your Discord account too.
- The Discord account can **Cancel** the move or release on Your panel name until then.
- The server the name moves to says when it takes over, connects then by itself and gets its certificate after that
  (not before: until the switch the relay refuses its key, DNS-01 included).
- If the old server is gone for good, the move still happens after the 24 hours. After a release, anyone may claim
  the name.

The relay's operator can also register a name by hand for your `relay-public.pem` (optionally for your Discord
account), ban a name (nobody's afterwards) and ban a Discord account (its names too).

## 2. A VPS, a cloud VM, or a home server with a public IP

You have SSH and can open ports, so there are three routes. They combine: the launcher works alongside either of the
others.

### A. The Kiln Launcher over SSH (nothing open but SSH)

This needs Kiln 0.2.0-dev.31.14 or newer (earlier launchers look for the mod's old name). Add the server to the launcher with its SSH connection (the launcher needs `kilnctl` on the server). **Servers >
(your server) > Open web panel** tunnels to the panel's loopback port over SSH, pins its certificate, and signs you
in as the owner (before any owner exists, it opens the setup page straight away). The panel can stay on `127.0.0.1`; no port, no certificate, no
domain. Only you, through the launcher, reach it.

### B. Your own address (direct HTTPS on an open port)

1. In Panel settings > Address and TLS (or `settings.json`): `bind` `0.0.0.0` (or `::` for IPv6 too; one address
   works as well), `port` (default the query port + 100, so 15737), and the host names people type.
2. Open that TCP port in the provider's firewall (its cloud firewall or security group) and in the server's own
   (`ufw allow 15737/tcp`). Port 80 too for HTTP-01, or listen on (or
   forward) 443 for TLS-ALPN-01.
3. A certificate browsers trust needs a name pointing at the server. Without a domain of your own:
   - a free dynamic DNS name pointing at the server, with Let's Encrypt **HTTP-01** (port 80
     open) or **TLS-ALPN-01** (port 443 reaches the panel);
   - or hosted access (route C) and its `<name>.kiln.nexus` certificate by DNS-01, needing no inbound
     port at all; traffic then goes through the relay, not directly to your VPS. A `<name>.kiln.nexus` name pointing
     straight at your VPS is not offered.
   - With a domain of your own: point a name at the server and use HTTP-01, TLS-ALPN-01, or your own PEM files
     (`tls.mode` `file`, reloaded when they change).
   - Self-signed (the default) works with the launcher's pin, but browsers warn on every visit.
4. Create the owner: with SSH, the simplest is the launcher (route A), or `ssh -L 15737:127.0.0.1:15737 <vps>` and the
   setup link from `kiln.log` on your PC's browser. A public bind only takes effect once the owner exists (or with
   `remoteSetup`, as for a rented server).

The log for this route: `bind 0.0.0.0 waits until the owner account exists; listening on 127.0.0.1 until then`, then
`PUBLIC: Kilnwarden listens on 0.0.0.0:15737 ...`; `cannot listen on 0.0.0.0:15737: AddressAlreadyInUse
(settings.json: port)`; `tls.mode off on a public address needs reverseProxy.enabled`; ACME: `ordering a
certificate for ... failed (...)`, `ACME tls-alpn-01: the CA connects to port 443 of ...; this panel listens on
15737, so 443 must be forwarded to it`.

Who can reach what: whoever can reach the port sees the sign-in page. Limit it with `ipAllowlist` (addresses or CIDR
ranges), the provider's firewall, or by using only route A.

### C. Hosted access

Exactly as for a rented server (section 1), and you may skip `remoteSetup`: create the owner through the launcher or
an SSH tunnel first.

## 3. Docker and Pterodactyl

The panel is inside the game server's process, so it runs in the same container (Wine image or not). It needs no
environment variables: its settings are files in `kiln/data/kilnwarden/`.

- **Hosted access** works from a container unchanged (section 1): it needs outbound TCP 443 and DNS
  from the container, nothing published. On Pterodactyl that is the default.
- **A published port** (Pterodactyl: an extra allocation): set `settings.json` `port` to the container port and
  **`bind` to `0.0.0.0`**. Docker forwards a published port to the container's own address, never to its loopback,
  so the default `127.0.0.1` never answers there. A public bind waits for the owner, so for the first run set
  `remoteSetup` (and get the setup link from the log or `kilnwarden setup`), or create the owner through hosted access.
- **The data folder** must be on the persistent volume (Pterodactyl's `/home/container` is). The panel writes its
  secrets (accounts, sessions, keys) readable by the container's user only (mode 600); back the folder up with the
  server. Don't mount it read-only.
- **TLS under Wine**: the mod loads its own TLS library, `kwtls-<version>.dll` (in the mod folder). If Wine can't load
  it, the panel falls back to .NET's SslStream (Wine's schannel/GnuTLS) and says so: `TLS: kwtls not available (...);
  falling back to SslStream (RSA, TLS 1.2)`. Both work; the `panel at ...` line names the engine in use.
- **Restarts**: the panel's Restart stops the server with exit code 0; Pterodactyl does not start it again by itself
  (the panel says "restart: unknown" and offers a stop). Start it from Pterodactyl.

Checked in our end-to-end tests (the dev host in the .NET runtime image as a non-root user, the server folder a
volume): the default bind behind a published port does not answer; `bind` 0.0.0.0 with `remoteSetup` serves the
setup page there before the owner exists; the whole relay route works from the container; the secrets on the volume
are mode 600, owned by the container's user.

## 4. A home server behind CGNAT, or with IPv6 only

Your ISP gives you no public IPv4 address you can forward (CGNAT, DS-Lite), or only IPv6. Use **hosted access**
(section 1; on your own machine the launcher works too, so `remoteSetup` is optional). The server dials out over IPv4
or IPv6, whichever it has: frostdev.io answers on both. Browsers reach `<name>.kiln.nexus` over IPv4 or IPv6
(it has both A and AAAA records), so IPv4-only visitors reach a server that has only IPv6.

Direct IPv6 (`bind` `::` and a firewall rule) works too, but then IPv4-only visitors can't reach it.

## Tests behind this page

| Route | Test |
|---|---|
| Rented server: outbound TCP 443 only, no inbound port, IPv4; first owner with file access alone | our end-to-end tests against the relay (network namespace, nftables) |
| IPv6 only | the same, IPv6 |
| Docker | the same, in a container |
| The console folder over FTP (`kilnwarden setup`, `kilnwarden status`) | `tests/Offline/Mod/ModLoadTests.cs` (Kiln's test harness) |
| remoteSetup, setup links, relay errors | `tests/Offline/Hosting/RemoteSetupTests.cs` |
| Claim links (Settings, `kilnwarden claim`) | `tests/Offline/Relay/ClaimLinkTests.cs`; the web tour (`web/e2e/tour.spec.ts`) |
| The claim through Discord, then the panel through the relay | our end-to-end tests (the real mod, the claim pages, a fake Discord) |
