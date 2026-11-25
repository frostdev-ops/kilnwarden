# Schedules, mod updates, saved commands: the contract

The endpoint and permission contract the code and the UI implement. Everything here is one host component,
`Ops.OpsComponent` (src/Kilnwarden.Core/Ops), registered after the auth component. It uses only Kiln.Admin ops that
Kiln lists in `ready.ops` (since 0.2.0-dev.30.9): `server_restart`, `server_restart_cancel`, `backup_create`, `broadcast`,
`players`, `command_run`, `mods`, `mod_load`, `mod_unload`, `mod_reload`, `update_list`, `update_stage`,
`update_cancel`, `update_rollback`. No Kiln mod API, ABI or KAP change.

General rules (all three modules):
- Every route is gated by the core gate on a declared permission; every KAP op a route sends is checked again with
  `KapGate.CheckOp` for the signed-in user (the op's own permission, its module switch, the step-up proof for
  sensitive ops) and sent as that user (`KapCaller.ForUser`: `by` / `source` = `web:<name>`).
- Step-up: a 403 `{"error":{"code":"reauth_required"}}` never applies anything. The UI shows the existing Reauth dialog;
  the user retries explicitly. Sensitive permissions below are `Sensitive = true` (the core gate asks for proof on every
  non-GET); some routes add a per-kind proof check in the handler (said so per route).
- Every write is audited (`schedules.*`, `saved-commands.*`, `updates.*`); KAP-level failures keep the op name and code.
  Audit args never hold a command line, a URL query, URL credentials or a token: commands are recorded as their first
  word plus an argument count (the gateway's rule), URLs as `origin + path`.
- Errors: `{"error": {"code", "message"}}` with 400 (bad body), 403 (`forbidden`, `reauth_required`, `owner_only`),
  404, 409 (state conflict), 422 (validation, `problems` in the message), 502 (Kiln.Admin answered with a failure:
  `code` = `kap_<code>`), 503 (`kap_unavailable`).

## Permissions

| Permission | Default role | Sensitive | What |
|---|---|---|---|
| `schedules.view` | moderator | no | see schedules and their run history |
| `schedules.manage` | admin | no | add, edit, enable, disable, delete, skip the next run, run now. Restart and command schedules also need a recent proof (handler) and the specific op's permission |
| `saved-commands.view` | moderator | no | see your own and shared saved commands, run them (the run is checked as `command_run` for you), pin them |
| `saved-commands.manage` | moderator | no | save, edit and delete your own saved commands, share them |
| `saved-commands.manage_all` | admin | no | edit and delete other users' shared saved commands |
| `updates.view` | admin | no | mod list, staged/rollback/probation status, plans |
| `updates.prepare` | owner | no | upload a package, fetch one from an allowlisted URL, make a plan (writes only to the panel's scratch folder). Owner accounts only |
| `updates.stage` | owner | yes | stage a plan for the next start, cancel a staged change, request a rollback. Owner accounts only |
| `updates.sources` | owner | yes | change the URL allowlist and size limits (updates.json) |

"Owner accounts only": the handler also requires `account.isOwner` (403 `owner_only`), so a custom role granted the
permission still cannot stage code. Mod lifecycle (load, unload, reload) is gated by the KAP ops' own permissions
(`mods.mod_load`, `mods.mod_unload`, `mods.mod_reload`: owner, sensitive) through `KapGate`.

## Schedules (`/api/schedules`)

A schedule: `{id, name, enabled, kind, when, timeZone, skipWhenEmpty, missed, catchUpMinutes, params, actor, created,
updated, updatedBy, lastSlot, lastRun, nextRun, nextFire, skipSlot}`.
- `kind`: `restart` (params `{countdownSeconds: 0..3600 (default 300), message?: <=200 chars one line,
  allowStopOnly: bool}`), `backup` (params `{name?: [A-Za-z0-9._-]{1,40} prefix}`), `announce` (params `{text: 1..200
  chars, one line}`), `command` (params `{savedCommandId}`; the server pins `revision` and `line` from the saved command).
- `when`: `{type: "daily", times: ["HH:mm", ...1..24], days?: ["mon".."sun"]}` | `{type: "interval", everyMinutes:
  5..10080 (restart: >= 60), anchor?: ISO instant}` | `{type: "once", at: ISO instant with offset}`.
- `timeZone`: `"UTC"` (default), `"local"` (the server's zone) or a zone id the server's OS knows (checked when saved;
  422 otherwise; under Wine/Windows invariant globalization, IANA ids may be unknown: use UTC or local). Daily times are
  wall-clock times in that zone. A time that does not exist (spring forward) runs at the first minute that exists after
  it; a time that exists twice (fall back) runs once, at the first.
- Restart: `when` names the moment of the restart; the countdown starts `countdownSeconds` earlier (Kiln.Admin's chat
  notices: at the request, then 10/5/2/1 min, 30/10 s, every second from 5). When the server's restart support is not
  `supported` (no kilnctl unit), a run is refused (`restart_unsupported`) unless `allowStopOnly` is true.
- `skipWhenEmpty`: before the run the panel asks `players`; with nobody online the run is recorded as `skipped`
  (`empty`). An unreadable player list runs anyway (recorded `playersUnknown`).
- `missed`: `"skip"` (default) or `"catchUp"` (not for restarts). A run that is due fires on time (within 2 minutes
  of its moment, the panel's tick is 5 s). A moment more than 2 minutes in the past (server down, panel off) is
  "missed": skipped and recorded once (several missed moments collapse into one record), or with `catchUp` run once if
  the newest missed moment is at most `catchUpMinutes` (1..1440, default 60) old.
- Dedupe: `lastSlot` (the newest moment handled, UTC) is written to schedules.json **before** a run is dispatched, so
  a reload, a restart or a clock step back never runs the same moment twice (at-most-once). A new, edited or re-enabled
  schedule starts from "now": it never runs moments before it was saved.
- Authority: a schedule runs as its `actor` (the account that last saved or enabled it). Nothing of a session or a
  password is stored. At every run the panel re-resolves the account and its roles **as they are then** (gone,
  disabled, restricted, lost `schedules.manage`, lost the op's permission, the module switched off, a command now
  refused by its roles: refused, recorded, audited) and checks the specific op with `KapGate.CheckOp` (no recent proof
  at run time: the proof was required when it was saved). Command schedules run the pinned `line`; a saved command
  edited or deleted since the schedule was saved is refused (`saved_command_changed` / `saved_command_gone`) until the
  schedule is saved again.
- Recent proof (handler, `reauth_required`): creating, editing or enabling a `restart` or `command` schedule, and
  "run now" of one. Disabling, deleting and skipping never need it.

| Method and path | Permission | Body / answer |
|---|---|---|
| GET `/api/schedules` | schedules.view | `{schedules: [...], restart: {supported, via, code} \| null, now, timeZones: ["UTC","local"]}` |
| POST `/api/schedules` | schedules.manage | a schedule's editable fields `{name, enabled, kind, when, timeZone, skipWhenEmpty, missed, catchUpMinutes, params}` -> 201 the schedule |
| PUT `/api/schedules/{id}` | schedules.manage | the same -> the schedule (actor becomes the editor) |
| DELETE `/api/schedules/{id}` | schedules.manage | 204 |
| POST `/api/schedules/{id}/enable` | schedules.manage | `{enabled: bool}` -> the schedule |
| POST `/api/schedules/{id}/skip-next` | schedules.manage | `{skip: bool}` -> the schedule (`skipSlot`) |
| POST `/api/schedules/{id}/run` | schedules.manage | runs once now **as the caller** (live session, its own gate) -> `{run}` |
| GET `/api/schedules/{id}/runs` | schedules.view | the newest 100 run records `[{at, schedule, slot, kind, outcome: ok\|failed\|refused\|skipped\|missed, code, message, by}]` |
| POST `/api/schedules/restart/cancel` | schedules.manage | `server_restart_cancel` as the caller (KapGate: `server.server_restart_cancel`, recent proof) |

Limits: 100 schedules; name 1..64 characters.

## Saved commands (`/api/saved-commands`)

A saved command: `{id, name, description, line, command, form, confirm, shared, owner: {id, name}, revision, created,
updated, pinned}` (`pinned` is per user).
- `line`: the filled form's line (1..1000 characters, one line, no control characters); `command` is its first word.
  `form`: opaque JSON the web app needs to reopen the form (at most 8 KiB; never interpreted by the server).
- Saving checks that **you** may run it now (`KapGate` `command_run` without the proof, and your roles' command rules);
  the line still runs only through `command_run` with your level cap at run time.
- Running is always as the **runner** (never the author): `KapGate.CheckOp(command_run)` (Console-level users need a
  recent proof, as on the console), the command rules, then `command_run`. A saved command with `confirm` needs
  `{"confirmed": true}` in the run body (409 `confirm_required` otherwise) so a stray request cannot skip the dialog.
  Audit: `saved-commands.run` with the gateway's command metadata (first word, argument count, level, outcome).
- Visibility: your own, plus shared ones. Shared ones are listed only to users with `commands.command_run`, and
  `runnable: false` when your roles' command rules refuse their command.
- `revision` grows with every edit; schedules pin it.

| Method and path | Permission | Body / answer |
|---|---|---|
| GET `/api/saved-commands` | saved-commands.view | `[saved]` with `runnable`, `mine`, `pinned` |
| POST `/api/saved-commands` | saved-commands.manage | `{name, description?, line, form?, confirm?, shared?}` -> 201 saved |
| PUT `/api/saved-commands/{id}` | saved-commands.manage (own) or .manage_all (shared of others) | the same -> saved (revision + 1) |
| DELETE `/api/saved-commands/{id}` | as PUT | 204 |
| POST `/api/saved-commands/{id}/run` | saved-commands.view | `{confirmed?}` -> `{ok, result}` (the command_run answer) |
| PUT `/api/saved-commands/{id}/pin` | saved-commands.view | `{pinned: bool}` -> `{pinned}` |
| GET `/api/saved-commands/pinned` | saved-commands.view | your pinned ones (the dashboard widget's data) |

Limits: 500 saved commands in total, 50 per user, 50 pins per user.

## Mod updates (`/api/updates`)

Staging only: nothing is replaced in a running server. Kiln.Admin's `update_stage` copies a prepared folder that sits
under `<server>/kiln/staged/incoming` and Kiln applies it at the next start (keeping the old version for rollback).
The panel's part: get the bytes, verify them, read the package metadata from the verified bytes, extract safely,
and hand Kiln.Admin the exact file list (`files: [{path, sha256}]`).

Flow: **upload** (chunked) or **fetch** (allowlisted HTTPS URL) -> **plan** (verify the SHA-256, inspect the zip, read
kiln.mod.json or kiln-runtime.json) -> **stage** (owner, recent proof: re-hash the scratch zip against the plan,
extract to `incoming/kilnwarden-<plan>`, call `update_stage`, remove the folder) -> next start applies it.
`GET /api/updates` shows the next-start state (`staged`, `rollback`, `probation`, `previous`, `open`) from
`update_list`.

- Checksums: a URL source needs the expected SHA-256 up front (422 without it). An upload may give it (then it must
  match) or not (the plan shows the digest the server computed, the owner compares it). Stage requires the plan's
  digest echoed back (`{sha256}`) and re-hashes the scratch file before extracting.
- Signatures (0.1.0): minisign detached signatures (Ed25519 over the BLAKE2b-512 of the file, or over the file for the
  legacy format), checked against the publisher keys in `updates.json` `trustedKeys`. A signature that does not match
  refuses the package (422 `bad_signature`); with `requireSignature` only packages a trusted key signed can be staged
  (409 `signature_required`). Plans carry `signature: {state: verified|unsigned|unknown_key, key, keyId,
  trustedComment}`. Uploads take `signature` (the .minisig text), URL fetches `signature` or `signatureUrl` (same
  allowlist). Kiln.Admin itself checks a Kiln bundle's bytes against its kiln-runtime.json.
- Archive rules (zip only): at most 10,000 entries, 512 MiB uncompressed, 256 MiB per file, a compression ratio of at
  most 200:1 on files over 1 MiB, counted on the bytes actually inflated (headers are not trusted); refused: encrypted
  entries, links and other non-regular files (unix mode, Windows reparse attribute), absolute paths, drive letters,
  backslashes, `.`/`..`/empty segments, `: * ? " < > |`, control characters, a segment ending in `.` or a space,
  Windows device names, 8.3 short-name-shaped segments (`ABCDEF~1`), paths over 240 UTF-8 bytes, and two names that
  differ only in case. One wrapping top folder is removed. Extraction streams each entry into a new file under the
  target with the counted limits, and checks every full path stays inside it.
- Metadata: `kiln.mod.json` (mod: `id`, `version`, `name`) or `kiln-runtime.json` (Kiln: `version`) from the
  verified archive (at most 64 KiB, JSON depth 32). A given `id` must match the package's.
- URLs: `https` only, no credentials in the URL, host and port in `updates.json` `allowedOrigins` (default
  `https://github.com`, `https://objects.githubusercontent.com`, `https://release-assets.githubusercontent.com`). Redirects
  are followed by the panel (at most 5), each hop checked against the allowlist; the connection is refused when the
  host resolves to a loopback, private, link-local, multicast, CGNAT or unspecified address; no proxy, no
  decompression; at most `maxDownloadBytes` (default 256 MiB); 120 s in total. Logs and audit keep origin + path.
- Uploads: `POST /uploads` opens one (size declared, at most `maxUploadBytes`, default 256 MiB); chunks are sent with
  `PUT /uploads/{id}?offset=N` (raw bytes, at most `chunkBytes` each, which is below the panel's request-body cap);
  `POST /uploads/{id}/finish` makes the plan. Uploads and plans expire after 30 minutes; at most 4 open per server.
- Mod lifecycle (this session only): `load` (folder), `unload`, `reload` (id) through `mod_load`/`mod_unload`/
  `mod_reload`.
- Persistent switches (0.1.0): `POST /api/updates/mods/switch {folder, enabled, now?}` (`updates.switch`, owner,
  sensitive) writes or removes `kiln/mods/<folder>/.disabled`, which Kiln's loader skips at the next start; `now`
  also unloads or loads it in this session. The panel's own folder and Kiln.Admin's cannot be switched off (409
  `needed_by_the_panel`). `GET /api/updates` lists `folders: [{folder, id, name, version, disabled, locked}]`.

| Method and path | Permission | Body / answer |
|---|---|---|
| GET `/api/updates` | updates.view | `{mods: <mods data>, updates: <update_list data>, plans: [...], restart, limits: {maxUploadBytes, chunkBytes, maxDownloadBytes}, allowedOrigins}` (a KAP failure is reported per part) |
| POST `/api/updates/uploads` | updates.prepare | `{name, size, sha256?}` -> 201 `{id, chunkBytes, expiresAt}` |
| PUT `/api/updates/uploads/{id}?offset=N` | updates.prepare | raw bytes -> `{received}` |
| POST `/api/updates/uploads/{id}/finish` | updates.prepare | `{expectId?}` -> 201 the plan |
| POST `/api/updates/fetch` | updates.prepare | `{url, sha256, expectId?}` -> 201 the plan |
| GET `/api/updates/plans/{id}` | updates.view | the plan |
| DELETE `/api/updates/plans/{id}` | updates.prepare | 204 (scratch removed) |
| POST `/api/updates/plans/{id}/stage` | updates.stage | `{sha256, label?}` -> `{unit, staged, note}` (the `update_stage` answer) |
| POST `/api/updates/units/{unit}/cancel` | updates.stage | `update_cancel` answer |
| POST `/api/updates/units/{unit}/rollback` | updates.stage | `update_rollback` answer |
| POST `/api/updates/mods/load` \| `unload` \| `reload` | updates.view (+ the op's own permission and proof via KapGate) | `{folder}` / `{id}` -> the op's answer |
| GET `/api/updates/sources` | updates.view | updates.json |
| PUT `/api/updates/sources` | updates.sources | updates.json -> saved |

A plan: `{id, kind: "mod"|"kiln", id (mod), name, version, current (the installed version or null), folder, files,
bytes, sha256, source: {type: "upload"|"url", name | url (origin + path)}, warnings, created, expiresAt}`.

## Files (under kiln/data/kilnwarden)
- `schedules/schedules.json` (atomic), `schedules/runs.jsonl` (rotated at 1 MiB, 3 kept)
- `saved-commands/saved.json`, `saved-commands/pins.json`
- `updates/updates.json` (settings, hot-reloaded), `updates/scratch/<id>/` (uploads and plans, removed on expiry and
  at start)
- `<server>/kiln/staged/incoming/kilnwarden-<plan>/` exists only while `update_stage` runs (left in place after a
  timeout, removed an hour later)
