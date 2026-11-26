// Owner decision 2026-11-25 (9:40 pm): wherever hosted access is described, kiln.nexus says it is not Nexus Mods, word
// for word: Panel settings > Hosted access, the README and the 0.1.1 changelog (the claim pages and kiln.nexus's own
// page are checked with the hosted services, which are not in this repository). The tour (e2e/tour.spec.ts) checks it
// is visible on the Hosted access tab.
import { describe, expect, test } from "bun:test";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { KILN_NEXUS_NOTE } from "../src/api/panel";

const SENTENCE = "kiln.nexus is frostdev's own domain for Kiln. It has no affiliation with Nexus Mods (nexusmods.com).";
const read = (p: string) => readFileSync(resolve(import.meta.dir, p), "utf8");

describe("kiln.nexus is not Nexus Mods", () => {
  test("the panel's sentence is the owner's, word for word", () => {
    expect(KILN_NEXUS_NOTE).toBe(SENTENCE);
  });

  test("Panel settings > Hosted access shows it first, in its own paragraph", () => {
    const settings = read("../src/pages/Settings.tsx");
    const tab = settings.slice(settings.indexOf("function RelayTab("), settings.indexOf("function claimRefused("));
    const at = tab.indexOf("{KILN_NEXUS_NOTE}");
    expect(at).toBeGreaterThan(0);
    expect(at).toBeLessThan(tab.indexOf("The panel keeps one connection open to the relay"));
  });

  test("the README's hosted access section says it under its heading, and so does the 0.1.1 changelog", () => {
    const readme = read("../../README.md");
    const section = readme.slice(readme.indexOf("## Hosted access on kiln.nexus"));
    expect(section.indexOf(SENTENCE)).toBeGreaterThan(0);
    expect(section.indexOf(SENTENCE)).toBeLessThan(200);
    const changelog = read("../../CHANGELOG.md");
    const v011 = changelog.slice(changelog.indexOf("## 0.1.1"), changelog.indexOf("## 0.1.0"));
    expect(v011.replace(/\s+/g, " ")).toContain(SENTENCE);
  });
});
