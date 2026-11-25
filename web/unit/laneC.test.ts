import { describe, expect, test } from "bun:test";
import { moduleApiPath } from "../src/api/laneC";
import { allowed, fromOurFrame, parseFrameMessage, rateLimiter, MAX_BYTES } from "../src/ext/bridge";
import { formatValue, linePath, nearestIndex, niceMax } from "../src/ui/chartMath";
import { coerce, initialValues } from "../src/ext/SchemaForm";

describe("custom page bridge", () => {
  test("parses only well-formed kwa/1 messages", () => {
    expect(parseFrameMessage({ kwa: 1, type: "call", id: 1, action: "greet", args: { name: "a" } })).toEqual({ kwa: 1, type: "call", id: 1, action: "greet", args: { name: "a" } });
    expect(parseFrameMessage({ kwa: 1, type: "query", id: 2, query: "stats", params: { n: 3 } })).toEqual({ kwa: 1, type: "query", id: 2, query: "stats", params: { n: "3" } });
    expect(parseFrameMessage({ kwa: 1, type: "resize", height: 99999 })).toEqual({ kwa: 1, type: "resize", height: 20000 });
    for (const bad of [
      null, "x", { kwa: 2, type: "ready" }, { kwa: 1, type: "nav", to: "/users" }, { kwa: 1, type: "call", id: 1, action: "../users" },
      { kwa: 1, type: "call", id: 1.5, action: "a" }, { kwa: 1, type: "call", id: 1, action: "a", args: [1] }, { kwa: 1, type: "subscribe", topic: "A B" },
      { kwa: 1, type: "query", id: 1, query: "q", params: { x: { y: 1 } } }, { kwa: 1, type: "call", id: 1, action: "a", args: { big: "x".repeat(MAX_BYTES) } },
    ]) expect("refused" in parseFrameMessage(bad)).toBe(true);
  });

  test("only our frame's window, with the sandbox's opaque origin", () => {
    const w = {};
    expect(fromOurFrame({ source: w, origin: "null" }, w)).toBe(true);
    expect(fromOurFrame({ source: w, origin: "https://panel" }, w)).toBe(false);
    expect(fromOurFrame({ source: {}, origin: "null" }, w)).toBe(false);
    expect(fromOurFrame({ source: null, origin: "null" }, null)).toBe(false);
  });

  test("allow-list and rate", () => {
    const allow = { actions: new Set(["greet"]), queries: new Set(["stats"]), topics: new Set<string>() };
    expect(allowed({ kwa: 1, type: "call", id: 1, action: "greet", args: {} }, allow)).toBe(true);
    expect(allowed({ kwa: 1, type: "call", id: 1, action: "reset", args: {} }, allow)).toBe(false);
    expect(allowed({ kwa: 1, type: "subscribe", topic: "greeted" }, allow)).toBe(false);
    let t = 0;
    const ok = rateLimiter(3, () => t);
    expect([ok(), ok(), ok(), ok()]).toEqual([true, true, true, false]);
    t = 1000;
    expect(ok()).toBe(true);
  });

  test("server paths stay within the module", () => {
    expect(moduleApiPath("sample", "/api/sample/data/stats")).toBe("/api/sample/data/stats");
    for (const p of ["/api/users", "/api/sample/../users", "https://evil/api/sample/x", "/api/sample//x", "/api/sample/data/x?y=1", "/api/samplex/data"])
      expect(() => moduleApiPath("sample", p)).toThrow();
  });
});

describe("charts", () => {
  test("nice axis and formats", () => {
    expect(niceMax(0)).toBe(1);
    expect(niceMax(7)).toBe(10);
    expect(niceMax(42)).toBe(50);
    expect(niceMax(1.7)).toBe(2);
    expect(formatValue(1536, "bytes")).toBe("1.5 KB");
    expect(formatValue(1060000000, "bytes")).toBe("1011 MB");
    expect(formatValue(2.88, "ms")).toBe("2.9 ms");
    expect(formatValue(null, "ms")).toBe("–");
  });
  test("nearest point and gaps", () => {
    expect(nearestIndex([0, 10, 20], 14)).toBe(1);
    expect(nearestIndex([0, 10, 20], 16)).toBe(2);
    expect(nearestIndex([], 1)).toBe(-1);
    expect(linePath([0, 1, 2, 3], [1, null, 2, 3], (t) => t, (v) => v)).toBe("M0.0,1.0M2.0,2.0L3.0,3.0");
  });
});

describe("schema forms", () => {
  const schema = { type: "object", required: ["name"], properties: { name: { type: "string" }, n: { type: "integer", default: 2 }, on: { type: "boolean" } } };
  test("initial values and coercion", () => {
    expect(initialValues(schema, { name: "a" })).toEqual({ name: "a", n: 2, on: false });
    expect(coerce(schema, { name: "a", n: "5", on: true })).toEqual({ name: "a", n: 5, on: true });
    expect(coerce(schema, { name: "", n: "" })).toEqual({ name: "" });
  });
});

// Security review S6: a custom page that navigates its frame (a second load) loses the bridge.
import { frameGuard } from "../src/ext/bridge";
describe("frame guard", () => {
  test("answers only the frame's first document", () => {
    const g = frameGuard();
    expect(g.accepts()).toBe(false); // before it loaded
    g.loaded();
    expect(g.accepts()).toBe(true);
    g.loaded(); // navigated (or reloaded by itself)
    expect(g.accepts()).toBe(false);
    expect(g.navigated()).toBe(true);
  });
});
