# Adding your mod to the web panel (Kilnwarden.Api 1.0)

Kilnwarden turns every Kiln command into a form on its Commands page, so a mod whose admin features are commands
gets a web UI without doing anything. This page is for mods that want more: their own page, buttons, a dashboard
widget, settings, live events or webhooks.

The API is the panel's export `Kilnwarden.Api.dll`. It uses only .NET types (System.Text.Json.Nodes), has its own
version (semver, `WebPanel.ApiVersion`), and is pinned by `src/Kilnwarden.Api/PublicAPI.txt`.

## Start from the template

```sh
dotnet new install <this repo>/templates/kilnwarden-module
dotnet new kilnwarden-module -n Lighthouse --moduleId lighthouse --modId lighthouse-panel
```

Then copy `Kilnwarden.Api.dll` from the panel's mod folder (`kiln/mods/kilnwarden/` on a server, or the
release zip) into the new project's `lib/`, and `dotnet build`. The reference is compile-only: at run time your mod
uses the panel's own copy, and never ships it.

The template makes a working mod: a page of blocks, an action that runs on the game thread, a query, typed settings
and a dashboard widget. `samples/Kilnwarden.Sample` shows everything else, including a custom page.

## The shape of it

```csharp
public override void OnLoad()
{
    if (!Mods.IsLoaded("kilnwarden")) return;     // the panel is optional
    _panel = Panel.Start(Context.Info.Id, Log.Info); // keep every Kilnwarden.Api type out of the Mod class
}
public override void OnUnload() => _panel?.Dispose();
```

- Declare the panel as an optional dependency: `<KilnDependency Include="kilnwarden" Version=">=0.1.0" Optional="true" />`.
  Kiln then loads your mod after the panel, and without it.
- `WebPanel.WhenAvailable(admin => ...)` calls you now if the panel runs, and again after every panel start (a panel
  reload drops what extensions registered). Dispose what it returns, and the extension, in `OnUnload`.
- `admin.Attach(modId)` gives your mod's `IWebAdminExtension`. Disposing it, your mod unloading, or the panel stopping
  removes everything you registered.

## Modules

`ext.RegisterModule(new ModuleDefinition(id, title) { ... })`:

| Field | Meaning |
|---|---|
| `id` | `[a-z0-9][a-z0-9_-]{0,63}`, not a first-party module or event prefix (`player`, `mod`, `server`, `schedule`...). Your routes are `/api/<id>/...` |
| `Icon`, `Description` | the navigation entry |
| `Permissions` | `PermissionDefinition("<id>.<name>", title) { DefaultRole, Sensitive }`. The owner can grant them to any role in Users and roles. `Sensitive = true`: every write needs the user to have confirmed their password or code in the last ten minutes |
| `Topics` | events you publish: `TopicDefinition(name, permission) { Webhooks, WebhookFields }` |
| `SettingsSchema` | a JSON Schema subset (object, string, integer, number, boolean, enum, string arrays, min/max, lengths) |

The module's on/off switch is in Panel settings, like every first-party module's.

## What a module can add

| Call | What users get | Server side |
|---|---|---|
| `AddAction(ActionDefinition(name, permission) { Title, ArgsSchema, Confirm, RunOn }, handler)` | a button or form | `POST /api/<id>/actions/<name>`; the permission is checked, args are checked against `ArgsSchema` (422), everything is audited (arguments whose names look secret are masked). `RunOn.GameThread` runs the handler in the server's tick, so it may use Kiln's game APIs |
| `AddQuery(QueryDefinition(name, permission), handler)` | data for pages and widgets | `GET /api/<id>/data/<name>?k=v`, read only, at most 1 MiB |
| `AddPage(DeclarativePage(id, title, blocks))` | a page of `TextBlock`, `StatBlock`, `TableBlock`, `FormBlock`, `ButtonBlock`: no JavaScript | blocks bound to actions or queries the user may not use are left out |
| `AddPage(CustomPage(id, title, script))` | your own ES module in a sandboxed frame | see below |
| `AddWidget(WidgetDefinition(id, title, Dashboard) { Query, Field, Kind })` | a card on the overview | |
| `AddWidget(WidgetDefinition(id, title, Public) { PublicData })` | a part of the public page, off until the owner lists it | only what `PublicData` returns; keep it public |
| `Publish(topic, payload)` | live updates in custom pages; webhooks when `Webhooks = true` | only `WebhookFields` leave the panel |
| `Settings<T>()` | a settings form (owner, sensitive) | `ext/<id>.json` (owner-only file), validated, hot-reloaded; public get/set properties of string, bool, numbers, enums and string lists |

A handler refuses with `throw new WebAdminRefusal(status, code, message)` (400, 403, 404, 409 or 422). Any other
exception is a 500 with a plain message; its details go to `kiln.log`.

## Custom pages

Your script runs in an iframe sandboxed without same-origin: no cookies, no storage, no network (fetch, XHR and
WebSocket are blocked by its CSP), no forms, popups or top navigation, and it cannot read the panel. It talks to the
panel through `window.kiln`:

```js
const stats = await kiln.query("stats", { since: "1h" });
await kiln.call("press", {});              // the user's permissions apply; a confirm step shows the panel's dialog
const stop = kiln.subscribe("greeted", payload => render(payload));
kiln.onTheme(tokens => {});                // --kwa-text, --kwa-bg, --kwa-accent... (also set as CSS variables)
kiln.resize(document.body.scrollHeight);
```

Only actions, queries and topics your module declared and the user may use are answered; the server checks each call
again. A page that navigates its frame away loses the bridge until the user loads it again. At most 20 messages a
second and 64 KiB each.

## Trust

Your mod runs in the server process: the panel trusts it for its own module, not more. It gets no admin token, no
sessions, no other module's settings and no raw KAP connection, and every route of your module passes the panel's
gate first. Your custom page's script is trusted less than your mod: it only sees what the bridge gives it.

## Versions

API 1.0.0. Additions bump the minor version and are listed in `PublicAPI.txt`; nothing is removed within 1.x. Check
`admin.ApiVersion` if you use something newer than 1.0.

## Testing without a game

The panel's engine runs without the game: the repo's offline tests register the sample and the template's module in
a real panel (`tests/Offline/LaneC`). `IWebAdmin` is an interface, so your own tests can also call your `Register`
against a fake.
