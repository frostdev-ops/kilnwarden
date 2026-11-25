// The navigation (the module switcher) from GET /api/core/modules: every enabled module's pages the user may see, as
// app paths. A page's `entry` names what the app shows: an admin-ui page (players, map, ...), a merged server page
// (configs) or a web-only page (users, audit, console = Commands, activity, settings...). Extension pages (declarative,
// custom) and unknown entries go to /m/<module>/<page>, which renders them or says why it cannot.
import type { NavModule } from "../api/types";
import { SERVER_ID } from "../api/kapSocket";

export interface NavItem {
  key: string;
  title: string;
  path: string;
  icon: string;
  group: "server" | "panel";
  order: number;
}

const admin = (page: string) => `/servers/${SERVER_ID}/admin/${page}`;

/** Known entries: path, group, order (the server's PageSpec.Order, which /api/core/modules does not carry). */
export const ENTRIES: Record<string, { path: string; group: NavItem["group"]; order: number }> = {
  players: { path: admin("players"), group: "server", order: 10 },
  map: { path: admin("map"), group: "server", order: 11 },
  zones: { path: admin("zones"), group: "server", order: 12 },
  activity: { path: "/activity", group: "server", order: 13 },
  items: { path: admin("items"), group: "server", order: 20 },
  spawn: { path: admin("spawn"), group: "server", order: 21 },
  world: { path: admin("world"), group: "server", order: 30 },
  combat: { path: admin("combat"), group: "server", order: 31 },
  creatures: { path: admin("creatures"), group: "server", order: 32 },
  content: { path: admin("content"), group: "server", order: 40 },
  gamedata: { path: admin("gamedata"), group: "server", order: 41 },
  saves: { path: admin("saves"), group: "server", order: 50 },
  configs: { path: `/servers/${SERVER_ID}/configs`, group: "server", order: 60 },
  permissions: { path: admin("permissions"), group: "server", order: 61 },
  console: { path: "/commands", group: "server", order: 70 },
  commands: { path: "/commands", group: "server", order: 70 },
  mods: { path: "/mods", group: "server", order: 80 },
  updates: { path: "/mods", group: "server", order: 80 },
  schedules: { path: "/schedules", group: "server", order: 85 },
  "saved-commands": { path: "/saved-commands", group: "server", order: 71 },
  metrics: { path: "/metrics", group: "server", order: 86 },
  webhooks: { path: "/webhooks", group: "panel", order: 930 },
  "public-page": { path: "/public-page", group: "panel", order: 940 },
  crashes: { path: admin("crashes"), group: "server", order: 81 },
  "core/settings": { path: "/settings", group: "panel", order: 900 },
  users: { path: "/users", group: "panel", order: 910 },
  audit: { path: "/audit", group: "panel", order: 920 },
};

/** The account page lives in the account menu, not the list. */
const HIDDEN = new Set(["auth/account"]);

export function buildNav(modules: NavModule[]): NavItem[] {
  const items: NavItem[] = [];
  const seen = new Set<string>();
  for (const m of modules) {
    for (const p of m.pages) {
      if (HIDDEN.has(p.entry)) continue;
      const known = p.kind === "builtin" ? ENTRIES[p.entry] : undefined;
      const path = known?.path ?? `/m/${encodeURIComponent(m.id)}/${encodeURIComponent(p.id)}`;
      if (seen.has(path)) continue;
      seen.add(path);
      items.push({
        key: `${m.id}:${p.id}`, title: p.kind === "builtin" && p.entry === "console" ? "Commands" : p.title, path, icon: p.icon ?? m.icon ?? "circle",
        group: known?.group ?? "panel", order: known?.order ?? 500,
      });
    }
  }
  return items.sort((a, b) => a.order - b.order || a.title.localeCompare(b.title));
}

/** Whether `path` (the location) is under `item`'s path. */
export function isActive(item: NavItem, path: string): boolean {
  return path === item.path || path.startsWith(item.path + "/");
}
