# Extension API, metrics, webhooks, public page: the contract

The contracts the code and the UI implement.

## Trust model (all four parts)
- Extension mods run in the server process: they are trusted code for **their own module**, not a sandbox. The panel
  gives them no admin secrets (no KAP token, no session store, no settings.json, no other module's settings), no
  unfiltered private feed (no raw KAP connection, no audit log, no other module's events) and no way around the gate:
  every extension route is a registry route, checked by the panel's gate (permission + recent proof for sensitive
  permissions) before the mod's handler runs.
- Custom page JavaScript is **less** trusted than its mod: it runs in a sandboxed iframe (opaque origin, no cookies,
  no network, no same-origin access) and can only ask the host page, by postMessage, for its own module's actions,
  queries and topics. The server checks each call again.
- Public routes are anonymous: they read one cached snapshot built from allow-listed fields only. Nothing on them
  reaches KAP, the stores or an admin response.

## C1. Public extension API (`Kilnwarden.Api.dll`, API 1.0.0)
Shipped as the mod's export (manifest `exports: ["Kilnwarden.Api.dll"]`). BCL-only (System.Text.Json.Nodes), no
Kiln types, its own semver (`WebPanel.ApiVersion`), public surface pinned by `src/Kilnwarden.Api/PublicAPI.txt`
(an offline test fails on any change that is not in the baseline: additions need the file updated and a minor bump,
removals a major bump).

Entry: `WebPanel.WhenAvailable(Action<IWebAdmin>)` (call in OnLoad, dispose in OnUnload; called on every panel start),
`WebPanel.TryGet(out IWebAdmin)`. `IWebAdmin.Attach(modId)` gives the mod's `IWebAdminExtension` (one per mod id;
disposing it, the mod's assembly context unloading, or the panel stopping removes everything it registered).

`IWebAdminExtension.RegisterModule(ModuleDefinition)` -> `IWebAdminModule`:
| Call | Server side |
|---|---|
| `AddAction(ActionDefinition, handler)` | `POST /api/<m>/actions/<name>`, gated on the action's permission (sensitive permission = recent proof), audited as `<m>.<name>` with `Redact` args masked, runs on the game thread or a pool thread |
| `AddQuery(QueryDefinition, handler)` | `GET /api/<m>/data/<name>?k=v` (read only, not audited, result <= 1 MiB) |
| `AddPage(DeclarativePage)` | `GET /api/<m>/pages/<id>`: the layout, blocks bound to actions/queries the user lacks are left out |
| `AddPage(CustomPage)` | `GET /api/<m>/frame/<id>`: an HTML document with the mod's script inlined, served with `Content-Security-Policy: sandbox allow-scripts; default-src 'none'; script-src 'nonce-..'; style-src 'nonce-..'; img-src data: blob:; connect-src 'none'; form-action 'none'; base-uri 'none'; frame-ancestors 'self'` and `X-Frame-Options: SAMEORIGIN`. Script <= 512 KiB |
| `AddWidget(WidgetDefinition)` | dashboard: listed in `GET /api/core/modules` (`widgets[].data` = its query path); public: a provider the public page calls (owner turns each on; off by default) |
| (always) | `GET /api/<m>/describe` (signed in): the actions, queries and topics this user may name, and whether the settings form is theirs (the custom-page host's allow-list) |
| `Publish(topic, payload)` | a declared topic only; `GET /api/<m>/events?after=<seq>` returns events of topics whose permission the user has (ring of 200 per module, payload <= 16 KiB); topics with `Webhooks = true` go to webhooks with only `WebhookFields` copied |
| `Settings<T>()` | `ext/<m>.json` in the data folder, hot reloaded (2 s poll), validated against `SettingsSchema`; `GET/PUT /api/<m>/settings` with the auto-declared permission `<m>.settings` (owner, **sensitive**: recent proof on PUT) |

Ids: module `[a-z0-9][a-z0-9_-]{0,63}` not taken by a first-party module; permissions `<m>.<name>`; actions,
queries, pages, widgets and topics `[a-z0-9][a-z0-9_-]{0,63}`. Handlers refuse with `WebAdminRefusal(status, code,
message)` (400/403/404/409/422): queries answer with that code, actions with the status and message (the core's action
route names the code after the status); other exceptions answer 500 `action_failed` / `query_failed`. Action arguments
are checked against `ArgsSchema` first (422). Settings types: public get/set properties of string, bool, numbers, enums,
string lists, converted by the panel itself (no serializer cache keeps a mod's types alive after it unloads). Every action call is in the audit log
whatever its outcome.

Panel routes of the component (module `extensions`, cannot be switched off):
| Route | Access |
|---|---|
| `GET /api/extensions` | `extensions.view` (admin): mods attached, their modules, API version |

Custom-page host protocol (`kwa: 1`, iframe <-> host page, both directions validated by `web/src/ext/bridge.ts`):
`{kwa:1, id, type:"call", action, args}`, `{kwa:1, id, type:"query", query, params}`, `{kwa:1, type:"subscribe"|"unsubscribe", topic}`,
`{kwa:1, type:"resize", height}`; answers `{kwa:1, id, type:"result", ok, result|error}`, `{kwa:1, type:"event", topic, seq, payload}`,
`{kwa:1, type:"theme", tokens}`. The host accepts messages only from that iframe's window with origin `"null"`, at most 64 KiB
and 20 per second, only for names the module declared and the user's permissions allow.

## C2. Metrics (module `metrics`)
Source: Kiln.Admin's `metrics` topic (every 2 s while subscribed) + the KAP connection state. Fields:
`players, stepsPerS, frameAvgMs, frameP95Ms, frameMaxMs, tickAvgMs, tickP95Ms, workingSetBytes, managedHeapBytes,
cpuPercent, gcPauseMs`, plus `modFaults` and `saveAgeS` (since the last world save) from the panel's server watch.
`saveMs` is reported `available: false` with a reason (Kiln.Admin tells only whoever asked for that save).
History in fixed rings, nothing unbounded: live 2 s x 300 (10 min, memory), 10 s x 8640 (24 h) and 5 min x 8640 (30 d)
persisted in `metrics/history.bin` (fixed-size binary, about 0.8 MB, atomic write every 5 min and at stop).
| Route | Access |
|---|---|
| `GET /api/metrics/fields` | `metrics.view` (moderator) |
| `GET /api/metrics/live` | `metrics.view` |
| `GET /api/metrics/history?range=10m|1h|6h|24h|7d|30d` | `metrics.view` (columnar, at most 600 points) |
| `GET /api/metrics/alerts` | `metrics.view` |
| `PUT /api/metrics/alerts` | `metrics.alerts.edit` (admin, **sensitive**) |
Alerts: `{id, metric, op: ">"|"<", threshold, forS (0-3600), cooldownS (>= 300), enabled}`, at most 32 rules; a
rule fires when the condition held for `forS`, then not again before `cooldownS`; at most `maxAlertsPerHour`
(default 6) in total. A firing is the panel event `metrics.alert`.

## C3. Webhooks (module `webhooks`)
Opt-in: no hook exists until the owner adds one. Kinds `discord` (host discord.com, discordapp.com, ptb./canary.,
path `/api/webhooks/<id>/<token>`) and `json` (any public https URL). URLs: https only, no user info, no fragment,
<= 512 chars, no IP literal or name that is local/private. Every send resolves the name and connects only to a public
address (checked again at connect time, so DNS rebinding cannot reach the LAN), no proxy, no redirects (3xx = failed),
5 s connect, 10 s total, body <= 16 KiB, response read <= 4 KiB. Per hook 20 sends a minute; one queue of 256 (oldest
dropped). Payloads copy only each event's allow-listed fields (strings <= 256 chars); Discord gets
`allowed_mentions: {parse: []}`. URLs are secrets: answers show `https://discord.com/api/webhooks/123.../***`.
| Route | Access |
|---|---|
| `GET /api/webhooks/hooks` | `webhooks.view` (admin) |
| `GET /api/webhooks/events` | `webhooks.view`: the event catalog |
| `GET /api/webhooks/deliveries` | `webhooks.view`: last 100 sends (no URL, no body) |
| `POST /api/webhooks/hooks` | `webhooks.edit` (owner, **sensitive**) |
| `PUT /api/webhooks/hooks/{id}` | `webhooks.edit` (sensitive; omitting `url` keeps it) |
| `DELETE /api/webhooks/hooks/{id}` | `webhooks.edit` (sensitive) |
| `POST /api/webhooks/hooks/{id}/test` | `webhooks.edit` (sensitive) |
Events: `player.join`, `player.leave` (from the `state` topic), `server.online`, `server.offline` (KAP connection),
`metrics.alert`, `webhook.test`, `<module>.<topic>` of extension topics with `Webhooks = true`. Admin actions
(`admin.action`) need the audit tap (`WebhookAuditTap`) wired by the integrator.

## C4. Public page (module `public`)
Off by default (`public.json` `enabled: false`): while off, the public routes answer 404.
| Route | Access |
|---|---|
| `GET /api/public/status` | Public (JSON snapshot; `Cache-Control: public, max-age=15`) |
| `GET /api/public/page` | Public (server-rendered HTML, no script: `default-src 'none'; style-src 'nonce-..'`) |
| `GET /api/public/settings` | `public.edit` (admin) |
| `PUT /api/public/settings` | `public.edit` (admin, **sensitive**) |
| `GET /api/public/preview` | `public.edit`: the snapshot even while off |
Parts (each a switch): status (on), player count (on), player names (**off**), map dots (**off**; `mapDotsGrid` 25 to
1000 m: positions are rounded to the middle of their grid cell, sorted, without names, drawn as an SVG without
script), rules (markdown subset, <= 4000 chars: headings, lists, emphasis, code, https links; everything else
escaped), mod list (off; names and versions only), join info (markdown), extension public widgets (each off until
listed). The snapshot is rebuilt at most
every 15 s from: KAP connected?, `state` topic's players (names only), the `mods` op as the panel (id, name,
version), the branding name.

## Permissions added
| Permission | Default role | Sensitive |
|---|---|---|
| `extensions.view` | admin | no |
| `metrics.view` | moderator | no |
| `metrics.alerts.edit` | admin | yes |
| `webhooks.view` | admin | no |
| `webhooks.edit` | owner | yes |
| `public.edit` | admin | yes |
| `<m>.settings` (per extension module with a settings schema) | owner | yes |
| extension-declared `<m>.*` | as declared | as declared |
