import { describe, expect, test } from "bun:test";
import { createApi } from "../src/api/http";
import { groupEvents } from "../src/api/laneC";
import {
  TLS_ENGINES, TLS_MODES, arrange, enumName, enumValue, eventText, imageType, isClaimPages, isClaimUrl, isLoopback, move, panelEndpoints, scheduledText,
  shortFingerprint,
} from "../src/api/panel";
import { durationShort, formatValue } from "../src/ui/chartMath";
import { ago, duration, sentence } from "../src/ui/format";

describe("settings.json enums", () => {
  test("a number (what the server sends today) or a name reads as the name", () => {
    expect(enumName(TLS_MODES, 2, "self-signed")).toBe("off");
    expect(enumName(TLS_MODES, 3, "self-signed")).toBe("acme");
    expect(enumName(TLS_MODES, "self-signed", "off")).toBe("self-signed");
    expect(enumName(TLS_MODES, "SelfSigned", "off")).toBe("self-signed");
    expect(enumName(TLS_ENGINES, "SslStream", "auto")).toBe("ssl-stream");
    expect(enumName(TLS_MODES, 9, "self-signed")).toBe("self-signed");
    expect(enumName(TLS_MODES, null, "file")).toBe("file");
  });

  test("a save sends the form the server sent", () => {
    expect(enumValue(TLS_MODES, "acme", 0)).toBe(3);
    expect(enumValue(TLS_MODES, "acme", "off")).toBe("acme");
    expect(enumValue(TLS_ENGINES, "kwtls", 0)).toBe(1);
  });
});

describe("dashboard layout", () => {
  const all = ["panel/server", "panel/links", "panel/info", "metrics/players"];
  test("saved order first, new cards after, hidden apart", () => {
    expect(arrange(all, undefined)).toEqual({ visible: all, hidden: [] });
    expect(arrange(all, { order: ["metrics/players", "panel/links", "gone/widget"], hidden: ["panel/info"] }))
      .toEqual({ visible: ["metrics/players", "panel/links", "panel/server"], hidden: ["panel/info"] });
  });

  test("up and down stay in bounds", () => {
    expect(move(["a", "b", "c"], "b", -1)).toEqual(["b", "a", "c"]);
    expect(move(["a", "b", "c"], "c", 1)).toEqual(["a", "b", "c"]);
    expect(move(["a", "b", "c"], "a", -1)).toEqual(["a", "b", "c"]);
  });

  test("only valid keys are saved", async () => {
    const sent: unknown[] = [];
    const p = panelEndpoints(createApi(async (_url, init) => { sent.push(JSON.parse(String(init.body))); return Response.json({ ok: true }); }));
    await p.saveDashboard({ order: ["panel/server", "Bad Key", "my-mod/stats"], hidden: ["x"] });
    expect(sent[0]).toEqual({ order: ["panel/server", "my-mod/stats"], hidden: [] });
  });
});

describe("branding images and addresses", () => {
  test("image types: png, webp, ico only", () => {
    expect(imageType({ name: "logo.png", type: "image/png" })).toBe("image/png");
    expect(imageType({ name: "favicon.ico", type: "" })).toBe("image/x-icon");
    expect(imageType({ name: "favicon.ico", type: "image/vnd.microsoft.icon" })).toBe("image/x-icon");
    expect(imageType({ name: "logo.svg", type: "image/svg+xml" })).toBeNull();
    expect(imageType({ name: "logo.jpg", type: "image/jpeg" })).toBeNull();
  });

  test("raw image bodies keep their content type", async () => {
    const calls: { url: string; type: string }[] = [];
    const p = panelEndpoints(createApi(async (url, init) => { calls.push({ url, type: (init.headers as Record<string, string>)["Content-Type"] }); return Response.json({ ok: true, file: "logo.png" }); }));
    await p.uploadBrandImage("logo", new Blob([new Uint8Array([137, 80, 78, 71])]), "image/png");
    expect(calls).toEqual([{ url: "/api/core/branding/logo", type: "image/png" }]);
  });

  test("loopback binds", () => {
    expect(isLoopback("127.0.0.1")).toBe(true);
    expect(isLoopback("::1")).toBe(true);
    expect(isLoopback("0.0.0.0")).toBe(false);
    expect(isLoopback("::")).toBe(false);
    expect(isLoopback("192.168.1.20")).toBe(false);
  });
});

describe("plain words", () => {
  test("durations and how long ago", () => {
    expect(duration(45)).toBe("45 s");
    expect(duration(12 * 60 + 5)).toBe("12 min");
    expect(duration(3900)).toBe("1 h 5 min");
    expect(duration(7200)).toBe("2 h");
    expect(duration(3 * 86400 + 4 * 3600)).toBe("3 d 4 h");
    expect(duration(null)).toBe("–");
    const now = Date.parse("2026-11-25T12:00:00Z");
    expect(ago("2026-11-25T11:59:50Z", now)).toBe("just now");
    expect(ago("2026-11-25T11:55:00Z", now)).toBe("5 min ago");
    expect(ago(null, now)).toBe("never");
  });

  test("server messages as sentences", () => {
    expect(sentence("email is not an address")).toBe("Email is not an address.");
    expect(sentence("Done.")).toBe("Done.");
  });

  test("activity lines", () => {
    expect(eventText({ type: "join", name: "Brannoc", seconds: null, detail: null })).toBe("Brannoc joined");
    expect(eventText({ type: "leave", name: "Brannoc", seconds: 3900, detail: null })).toBe("Brannoc left after 1 h 5 min");
    expect(eventText({ type: "leave", name: "Ysolde", seconds: 60, detail: "server stopped" })).toBe("Ysolde left after 1 min (server stopped)");
    expect(eventText({ type: "death", name: "Ysolde", seconds: null, detail: "a wolf" })).toBe("Ysolde died (killed by a wolf)");
    expect(eventText({ type: "death", name: "Ysolde", seconds: null, detail: "" })).toBe("Ysolde died");
  });

  test("seconds on a chart", () => {
    expect(formatValue(42, "s")).toBe("42 s");
    expect(formatValue(600, "s")).toBe("10 min");
    expect(durationShort(5400)).toBe("1.5 h");
    expect(durationShort(3 * 86400)).toBe("3.0 d");
  });

  test("webhook events grouped by prefix, players first", () => {
    const g = groupEvents([
      { topic: "server.restart", title: "r", fields: [] }, { topic: "player.join", title: "j", fields: ["player"] },
      { topic: "player.death", title: "d", fields: ["player", "killer"] }, { topic: "zzz.thing", title: "z", fields: [] },
    ]);
    expect(g.map((x) => [x.group, x.events.map((e) => e.topic)])).toEqual([["Players", ["player.join", "player.death"]], ["Server", ["server.restart"]], ["Zzz", ["zzz.thing"]]]);
  });
});

describe("claim links (hosted access, lane L1)", () => {
  test("the fingerprint people compare: 32 hex digits in fours, as the claim page shows", () => {
    expect(shortFingerprint("0123456789abcdef".repeat(4))).toBe("0123 4567 89ab cdef 0123 4567 89ab cdef");
    expect(shortFingerprint(null)).toBe("");
  });

  test("only the claim pages' own https origin (claim.<zone>, 0.1.1) becomes a link", () => {
    expect(isClaimUrl("https://claim.kiln.nexus/claim?v=1&host=a.kiln.nexus&key=x")).toBe(true);
    expect(isClaimUrl("https://claim.panel.test:8443/claim?v=1&host=a")).toBe(true);
    for (const bad of ["javascript:alert(1)//https://claim.kiln.nexus/claim?v=1&", "http://claim.kiln.nexus/claim?v=1&",
      "https://claim.kiln.nexus/other?v=1&", "https://evil.example@claim.kiln.nexus/claim?v=1&", " https://claim.kiln.nexus/claim?v=1&",
      // 0.1.0's links on frostdev.io, and names that are not the claim pages
      "https://frostdev.io/kiln/webadmin/names/claim?v=1&host=a.kiln.nexus", "https://claim/claim?v=1&", "https://myclaim.kiln.nexus/claim?v=1&",
      "https://claim.kiln.nexus/x/claim?v=1&", "https://claim.kiln.nexus/claim?v=2&"]) {
      expect(isClaimUrl(bad)).toBe(false);
    }
    expect(isClaimPages("https://claim.kiln.nexus/")).toBe(true);
    for (const bad of [null, undefined, "", "https://claim.kiln.nexus/x", "https://frostdev.io/kiln/webadmin/names", "javascript:alert(1)//https://claim.kiln.nexus/"]) {
      expect(isClaimPages(bad)).toBe(false);
    }
  });

  test("a scheduled move or release of the panel's name in a sentence (lane L1b)", () => {
    const at = "26 Nov 2026, 9:00 AM";
    expect(scheduledText("a.kiln.nexus", { to: "ab".repeat(32), requested: "x", at: "y" }, at)).toBe("This panel's name a.kiln.nexus is being moved to another server on 26 Nov 2026, 9:00 AM.");
    expect(scheduledText("a.kiln.nexus", { to: null, requested: "x", at: "y" }, at)).toContain("is being released on 26 Nov 2026, 9:00 AM");
  });

  test("Undo posts to the relay's undo route", async () => {
    const calls: { url: string; method: string }[] = [];
    const p = panelEndpoints(createApi(async (url, init) => {
      calls.push({ url: String(url), method: String(init.method) });
      return Response.json({ undone: true, host: "a.kiln.nexus" });
    }));
    expect((await p.relayUndo()).undone).toBe(true);
    expect(calls).toEqual([{ url: "/api/core/relay/move/undo", method: "POST" }]);
  });

  test("Make a claim link posts to the relay's claim route", async () => {
    const calls: { url: string; method: string }[] = [];
    const p = panelEndpoints(createApi(async (url, init) => {
      calls.push({ url: String(url), method: String(init.method) });
      return Response.json({ url: "https://claim.kiln.nexus/claim?v=1&", host: "a.kiln.nexus", fingerprint: "ab", expires: "2026-11-25T00:00:00Z", warnings: [] });
    }));
    const link = await p.relayClaim();
    expect(calls).toEqual([{ url: "/api/core/relay/claim", method: "POST" }]);
    expect(link.host).toBe("a.kiln.nexus");
  });
});
