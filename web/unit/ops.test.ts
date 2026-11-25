import { expect, test } from "bun:test";
import { createApi } from "../src/api/http";
import { describeWhen, opsEndpoints, parseTimes, sha256Hex } from "../src/api/ops";
import type { NavModule } from "../src/api/types";
import { buildNav } from "../src/state/nav";

test("uploads go in ordered raw chunks with the browser's SHA-256, then finish", async () => {
  const json: { url: string; method: string; body: unknown }[] = [];
  const chunks: { path: string; bytes: number[] }[] = [];
  const api = createApi(async (url, init) => {
    json.push({ url, method: init.method!, body: init.body ? JSON.parse(String(init.body)) : null });
    if (url === "/api/updates/uploads") return Response.json({ id: "p-1/x", chunkBytes: 4 }, { status: 201 });
    return Response.json({ id: "p-1/x", kind: "mod" }, { status: 201 });
  });
  const ops = opsEndpoints(api, async (path, bytes) => { chunks.push({ path, bytes: [...bytes] }); return { received: 0 }; });
  const bytes = new Uint8Array([1, 2, 3, 4, 5, 6, 7, 8, 9, 10]);
  const progress: number[] = [];
  await ops.upload({ name: "m.zip", bytes }, { expectId: "cool-mod", onProgress: (f) => progress.push(f) });
  expect(json[0]).toEqual({ url: "/api/updates/uploads", method: "POST", body: { name: "m.zip", size: 10, sha256: await sha256Hex(bytes) } });
  expect(chunks.map((c) => c.path)).toEqual([
    "/api/updates/uploads/p-1%2Fx?offset=0", "/api/updates/uploads/p-1%2Fx?offset=4", "/api/updates/uploads/p-1%2Fx?offset=8",
  ]);
  expect(chunks.flatMap((c) => c.bytes)).toEqual([...bytes]);
  expect(progress.at(-1)).toBe(1);
  expect(json[1]).toEqual({ url: "/api/updates/uploads/p-1%2Fx/finish", method: "POST", body: { expectId: "cool-mod" } });
});

test("ids are single path segments and runs send confirmed only when asked", async () => {
  const calls: { url: string; body: unknown }[] = [];
  const ops = opsEndpoints(createApi(async (url, init) => { calls.push({ url, body: init.body ? JSON.parse(String(init.body)) : null }); return Response.json({}); }));
  await ops.runCommand("sc-1/../x");
  await ops.runCommand("sc-2", true);
  await ops.skipNext("sch-1", true);
  await ops.cancelUnit("mod.cool-mod");
  await ops.modLifecycle("load", "cool-mod");
  await ops.modLifecycle("reload", "cool-mod");
  expect(calls).toEqual([
    { url: "/api/saved-commands/sc-1%2F..%2Fx/run", body: {} },
    { url: "/api/saved-commands/sc-2/run", body: { confirmed: true } },
    { url: "/api/schedules/sch-1/skip-next", body: { skip: true } },
    { url: "/api/updates/units/mod.cool-mod/cancel", body: null },
    { url: "/api/updates/mods/load", body: { folder: "cool-mod" } },
    { url: "/api/updates/mods/reload", body: { id: "cool-mod" } },
  ]);
});

test("times and rules in plain words", () => {
  expect(parseTimes("6:00, 18:30 18:30;25:00 x 07:5")).toEqual(["06:00", "18:30"]);
  expect(describeWhen({ type: "daily", times: ["06:00"], days: ["sat"] }, "UTC")).toBe("sat at 06:00 (UTC)");
  expect(describeWhen({ type: "interval", everyMinutes: 360 }, "UTC")).toBe("every 6 h");
  expect(describeWhen({ type: "interval", everyMinutes: 45 }, "UTC")).toBe("every 45 min");
});

test("the operations pages are real pages in the server group, Mods once", () => {
  const mod = (id: string, pages: [string, string][]): NavModule => ({
    id, title: id, icon: "x", widgets: [], permissions: [],
    pages: pages.map(([pid, entry]) => ({ id: pid, title: pid, kind: "builtin" as never, entry, icon: null })),
  });
  const nav = buildNav([mod("mods", [["mods", "mods"]]), mod("updates", [["updates", "updates"]]), mod("schedules", [["schedules", "schedules"]]),
    mod("saved-commands", [["saved-commands", "saved-commands"]])]);
  expect(nav.filter((n) => n.path === "/mods").length).toBe(1);
  for (const p of ["/mods", "/schedules", "/saved-commands"]) expect(nav.find((n) => n.path === p)?.group).toBe("server");
  expect(nav.filter((n) => n.path === "/schedules").length).toBe(1);
});
