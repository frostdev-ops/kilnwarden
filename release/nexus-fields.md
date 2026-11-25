# Nexus fields: Kilnwarden - Web Admin

For the owner's Create/Publish clicks. Nothing here is posted.

| Field | Value |
|---|---|
| Game | Enshrouded |
| Mod name | Kilnwarden - Web Admin |
| Summary | `nexus-summary.txt` (under 350 characters, starts with "Kilnwarden is a web admin panel") |
| Description | `nexus-description.bbcode` |
| Category | **Utilities** (Enshrouded's categories on Nexus: Miscellaneous, Gameplay, Visuals, Utilities, User Interface, Characters; checked through the public API 2026-11-25) |
| Version | 0.1.0 |
| Language | English |
| Author | Frostdev |
| Primary image | `art/nexus_main_1280x720.png`; banner `art/nexus_banner_1920x480.png` |
| Gallery | `art/screens/`: overview, players, commands, schedules, mods, metrics, users, hosted-access, phone-overview, phone-commands (demo data only) |
| Source / homepage | https://github.com/frostdev-ops/kilnwarden |
| Contains adult content | No |
| Permissions | Open: the MIT license (credit and license notice kept); assets in modpacks allowed; no Nexus-only restriction. Credits: see the description's "Permissions and credits" |

## Tags

Pick the closest matches in Nexus's tag picker: **Server**, **Dedicated Server**, **Admin**, **Tools**, **Utilities**,
**Multiplayer**, **Quality of Life**, **Web**, **Kiln**.

## Requirements (every file)

| Requirement | Kind | Note |
|---|---|---|
| Kiln (Nexus mod 301) | Nexus mod | "Minimum version 0.2.0-dev.31.10, on the server, with its admin part (installed by default on a server). The SDK Kilnwarden 0.1.0 builds against." |

No requirement for players: tick "server only" if the form has it; the file is for dedicated servers.

## Files

| File | Category | Name | Version | Description |
|---|---|---|---|---|
| `kilnwarden-0.1.0.zip` | Main | Kilnwarden 0.1.0 | 0.1.0 | Unzip into the server's kiln/mods/ (kiln/mods/kilnwarden/kiln.mod.json). Needs Kiln 0.2.0-dev.31.10+. sha256 in the changelog post. |

## Still open (owner)

- Image URLs in the description are `TODO-upload:` placeholders: upload the images (gallery or frostdev.io) and paste
  their URLs.
- The README, the description and the changelog post say "Open web panel" needs Kiln 0.2.0-dev.31.14 or newer (today's
  launchers look for the old id): once that update ships, say the button works.
- Hosted access is written as "coming soon": change it when the relay and the claim pages are announced.
