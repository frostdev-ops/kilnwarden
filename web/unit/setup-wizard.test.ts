// The setup wizard's logic (src/setup/wizard.ts): each route's pages, where a restart comes back to, the panel name's
// rules (the same sentences as the server's PanelNames), and the checklist's plain words.
import { describe, expect, test } from "bun:test";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import {
  RESERVED, allDone, checkLabel, firstProblem, nameProblem, normalizeName, pagesOf, position, reasonText, resumeAt, suggestName, type SetupStep,
} from "../src/setup/wizard";

const step = (id: string, state: SetupStep["state"], note: string | null = null, problem: SetupStep["problem"] = null): SetupStep => ({ id, state, note, problem });

describe("the pages of each route", () => {
  test("hosted, launcher and own address", () => {
    expect(pagesOf("hosted")).toEqual(["welcome", "owner", "route", "name", "claim", "progress", "discord", "done"]);
    expect(pagesOf("launcher")).toEqual(["welcome", "owner", "route", "launcher", "discord", "done"]);
    expect(pagesOf("own")).toEqual(["welcome", "owner", "route", "own", "own-progress", "discord", "done"]);
    expect(pagesOf("")).toEqual(["welcome", "owner", "route", "done"]);
  });
  test("the dots say where the page is", () => {
    expect(position("hosted", "welcome")).toEqual({ at: 1, of: 8 });
    expect(position("hosted", "claim")).toEqual({ at: 5, of: 8 });
    expect(position("launcher", "done")).toEqual({ at: 6, of: 6 });
    expect(position("", "route")).toEqual({ at: 3, of: 8 }); // before a choice: counted as the default route
  });
});

describe("coming back after a restart", () => {
  const base = { done: false, route: "" as const, step: "", hosted: undefined };
  test("a finished setup shows its end", () => expect(resumeAt({ ...base, done: true })).toBe("done"));
  test("nothing chosen yet: the choice", () => expect(resumeAt(base)).toBe("route"));
  test("the saved page of the saved route", () => {
    expect(resumeAt({ ...base, route: "hosted", step: "claim" })).toBe("claim");
    expect(resumeAt({ ...base, route: "own", step: "own-progress" })).toBe("own-progress");
  });
  test("a page that is not the route's, or the account, goes back to the choice", () => {
    expect(resumeAt({ ...base, route: "launcher", step: "claim" })).toBe("route");
    expect(resumeAt({ ...base, route: "hosted", step: "owner" })).toBe("route");
    expect(resumeAt({ ...base, route: "hosted", step: "nonsense" })).toBe("route");
  });
  test("set up from the server console: only the optional page and the end are left", () => {
    expect(resumeAt({ ...base, hosted: { host: "a.kiln.nexus", name: "a", ready: true } })).toBe("discord");
    expect(resumeAt({ ...base, route: "hosted", hosted: { host: "a.kiln.nexus", name: "a", ready: true } })).toBe("discord");
    expect(resumeAt({ ...base, route: "hosted", step: "progress", hosted: { host: "a.kiln.nexus", name: "a", ready: true } })).toBe("progress");
    const steps = [{ id: "ready", state: "working" as const, note: null, problem: null }];
    expect(resumeAt({ ...base, route: "hosted", client: { thisMachine: false, hosted: true }, hosted: { host: "a.kiln.nexus", name: "a", ready: false, steps } })).toBe("progress");
    expect(resumeAt({ ...base, route: "hosted", client: { thisMachine: true, hosted: false }, hosted: { host: "a.kiln.nexus", name: "a", ready: false, steps } })).toBe("route");
    expect(resumeAt({ ...base, route: "hosted", step: "done", hosted: { host: "a.kiln.nexus", name: "a", ready: true } })).toBe("done");
  });
});

describe("the panel's name", () => {
  test("good names", () => {
    for (const n of ["abc", "my-server", "a1b2", "x".repeat(40)]) expect(nameProblem(n)).toBeNull();
  });
  test("each problem in one plain sentence, as the server says it", () => {
    expect(nameProblem("")).toBe("Type a name.");
    expect(nameProblem("ab")).toBe("Use at least 3 characters.");
    expect(nameProblem("x".repeat(41))).toBe("Use at most 40 characters.");
    expect(nameProblem("my_server")).toBe("Use only letters, digits and dashes.");
    expect(nameProblem("My-Server")).toBe("Use only letters, digits and dashes."); // normalizeName lowers it first
    expect(nameProblem("-abc")).toBe("Start and end with a letter or a digit.");
    expect(nameProblem("abc-")).toBe("Start and end with a letter or a digit.");
    expect(nameProblem("xn--abc")).toBe("Don't put two dashes in the third and fourth places.");
    expect(nameProblem("admin")).toBe('"admin" is kept for Kiln itself. Pick another name.');
    expect(nameProblem("claim")).toContain("kept for Kiln itself");
  });
  test("what people paste is taken apart to the name", () => {
    expect(normalizeName("  https://My-Server.kiln.nexus/ ")).toBe("my-server");
    expect(normalizeName("my-server.kiln.nexus")).toBe("my-server");
    expect(normalizeName("e2e.panel.test", "panel.test")).toBe("e2e");
  });
  test("a first name from the server's name", () => {
    expect(suggestName("Frost's Keep")).toBe("frost-s-keep");
    expect(suggestName("Kilnwarden")).toBe(""); // the default name says nothing about the server
    expect(suggestName("!!")).toBe("");
    expect(suggestName("admin")).toBe("");
  });
  test("the reserved names are the server's and the claim pages'", () => {
    const cs = readFileSync(resolve(import.meta.dir, "../../src/Kilnwarden.Core/Setup/PanelNames.cs"), "utf8");
    const listed = [...cs.slice(cs.indexOf("Reserved ="), cs.indexOf("};", cs.indexOf("Reserved ="))).matchAll(/"([a-z0-9]+)"/g)].map((m) => m[1]);
    expect(new Set(listed)).toEqual(RESERVED);
  });
});

describe("the checklist in plain words", () => {
  test("each line while it happens and once done", () => {
    expect(checkLabel(step("connected", "working"))).toBe("Connecting…");
    expect(checkLabel(step("connected", "done"))).toBe("Connected");
    expect(checkLabel(step("claimed", "waiting", "claim"))).toBe("Waiting for your claim…");
    expect(checkLabel(step("claimed", "waiting", "moving"))).toBe("Moving to this server…");
    expect(checkLabel(step("claimed", "done"))).toBe("Claimed");
    expect(checkLabel(step("secure", "working"))).toBe("Securing…");
    expect(checkLabel(step("secure", "waiting"))).toBe("Secure");
    expect(checkLabel(step("secure", "done"))).toBe("Secure");
    expect(checkLabel(step("ready", "working"))).toBe("Checking…");
    expect(checkLabel(step("ready", "done"))).toBe("Ready");
    expect(checkLabel(step("connected", "failed"))).toBe("Can't connect");
    expect(checkLabel(step("claimed", "waiting"))).toBe("Claimed");
  });
  test("no technical words on the main path", () => {
    const words = ["connected", "claimed", "secure", "ready", "found", "reachable"].flatMap((id) =>
      (["done", "working", "waiting", "failed"] as const).map((s) => checkLabel(step(id, s))));
    for (const w of words) expect(w).not.toMatch(/relay|certificate|dns|acme|tls|port|key|fingerprint|json/i);
  });
  test("done and the first problem", () => {
    expect(allDone([step("a", "done"), step("b", "done")])).toBe(true);
    expect(allDone([step("a", "done"), step("b", "working")])).toBe(false);
    expect(allDone([])).toBe(false);
    const p = { title: "This server can't reach frostdev.io.", fix: "Ask your host.", detail: "x" };
    expect(firstProblem([step("a", "done"), step("b", "failed", null, p), step("c", "failed", null, p)])?.id).toBe("b");
    expect(firstProblem([step("a", "done")])).toBeNull();
  });
  test("why a route is suggested", () => {
    expect(reasonText("container")).toContain("container");
    expect(reasonText("default")).toBeNull();
  });
});
