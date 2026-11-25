import { describe, expect, test } from "bun:test";
import type { NavModule } from "../src/api/types";
import { buildNav, isActive } from "../src/state/nav";

const mod = (id: string, pages: [string, string, string?][], icon = "x"): NavModule => ({
  id, title: id, icon, widgets: [], permissions: [],
  pages: pages.map(([pid, entry, kind]) => ({ id: pid, title: pid[0].toUpperCase() + pid.slice(1), kind: (kind ?? "builtin") as never, entry, icon: null })),
});

describe("navigation", () => {
  test("entries become app paths, ordered like the server's pages", () => {
    const nav = buildNav([
      mod("users", [["users", "users"]]),
      mod("players", [["players", "players"], ["map", "map"]]),
      mod("commands", [["console", "console"]]),
      mod("configs", [["configs", "configs"]]),
      mod("auth", [["account", "auth/account"]]),
    ]);
    expect(nav.map((n) => n.path)).toEqual(["/servers/this/admin/players", "/servers/this/admin/map", "/servers/this/configs", "/commands", "/users"]);
    expect(nav.find((n) => n.path === "/commands")?.group).toBe("server");
  });

  test("extension pages and unknown entries go to the module page", () => {
    const nav = buildNav([mod("mymod", [["stats", "mymod/stats"], ["cfg", "panel.json", "declarative"]])]);
    expect(nav.map((n) => n.path).sort()).toEqual(["/m/mymod/cfg", "/m/mymod/stats"]);
  });

  test("no modules, no pages (nothing is a placeholder)", () => {
    expect(buildNav([])).toEqual([]);
  });

  test("web-only module pages are real pages in their groups", () => {
    const nav = buildNav([mod("metrics", [["metrics", "metrics"]]), mod("webhooks", [["webhooks", "webhooks"]]), mod("public", [["public-page", "public-page"]]),
      mod("activity", [["activity", "activity"]]), mod("core", [["settings", "core/settings"]])]);
    expect(nav.map((n) => [n.path, n.group])).toEqual([["/activity", "server"], ["/metrics", "server"], ["/settings", "panel"], ["/webhooks", "panel"], ["/public-page", "panel"]]);
  });

  test("the same path once", () => {
    const nav = buildNav([mod("commands", [["console", "console"], ["commands", "commands"]])]);
    expect(nav.length).toBe(1);
  });

  test("active by prefix", () => {
    const [n] = buildNav([mod("users", [["users", "users"]])]);
    expect(isActive(n, "/users")).toBe(true);
    expect(isActive(n, "/users/roles")).toBe(true);
    expect(isActive(n, "/usersx")).toBe(false);
  });
});

test('legacy Console page identity is preserved but displayed as Commands', () => {
  const [n] = buildNav([mod('server', [['console', 'console']])]);
  expect(n.key).toBe('server:console');
  expect(n.path).toBe('/commands');
  expect(n.title).toBe('Commands');
});
