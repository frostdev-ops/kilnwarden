// 0.1.1 ships once hosted access is live (owner, 2026-11-25): the texts describe hosted access and Discord sign-in as
// working, "Open web panel" as needing Kiln 0.2.0-dev.31.14 (out now), and carry the owner's privacy wording.
import { describe, expect, test } from "bun:test";
import { readdirSync, readFileSync } from "node:fs";
import { join, relative, resolve } from "node:path";

const root = resolve(import.meta.dir, "../..");
const read = (p: string) => readFileSync(join(root, p), "utf8");
const flat = (s: string) => s.replace(/\s+/g, " ");

function files(dir: string, ext: RegExp): string[] {
  return readdirSync(join(root, dir), { withFileTypes: true }).flatMap((e) => {
    const p = join(dir, e.name);
    if (e.isDirectory()) return files(p, ext);
    return ext.test(e.name) ? [p] : [];
  });
}

// what players and server owners read: the docs, the Nexus texts, the panel's own text and the console's help
const TEXTS = [
  "README.md", "CHANGELOG.md", "SECURITY.md", "DESIGN.md",
  ...files("docs", /\.md$/), ...files("release", /\.(md|bbcode|txt)$/), ...files("web/src", /\.(ts|tsx)$/),
  "src/Kilnwarden/WebAdminMod.cs", "src/Kilnwarden.Core/Hosting/ConsoleCommands.cs",
].map((p) => relative(root, join(root, p)));

const NOT_LIVE = /coming soon|go(es)? live soon|live soon|not live yet|will be on the (kiln )?privacy page|privacy page will say|before (hosted access|they) opens?|once hosted access opens|until hosted access opens/i;

describe("hosted access is live", () => {
  test.each(TEXTS)("%s never says hosted access or Discord sign-in is still to come", (p) => {
    const hits = flat(read(p)).match(new RegExp(`.{0,60}(${NOT_LIVE.source}).{0,40}`, "gi")) ?? [];
    expect(hits).toEqual([]);
  });

  test("the privacy commitment reads as the owner wrote it, with the link", () => {
    for (const p of ["README.md", "docs/hosting-routes.md"]) {
      expect(flat(read(p).replace(/\[([^\]]+)\]\([^)]+\)/g, "$1"))).toContain("We commit not to, and the Kiln privacy page says so.");
      expect(read(p)).toContain("https://frostdev.io/kiln/privacy");
    }
    expect(read("release/nexus-description.bbcode")).toContain(
      "We commit not to, and the [url=https://frostdev.io/kiln/privacy]Kiln privacy page[/url] says so.");
  });

  test("counts, not addresses, says what nginx keeps; and the CAA record is named", () => {
    for (const p of ["README.md", "docs/hosting-routes.md", "release/nexus-description.bbcode"]) {
      const s = flat(read(p));
      expect(s).not.toContain("It keeps counts, not addresses");
      expect(s).not.toContain("does not log addresses");
      expect(s).toContain("The relay keeps counts, not addresses. nginx in front of it records a visitor's address only when a connection fails, and keeps it 14 days.");
      expect(s).toMatch(/kiln\.nexus`? CAA record forbids wildcard certificates/);
    }
  });

  test("the 0.1.1 changelog gives players what changed for them", () => {
    const log = read("CHANGELOG.md");
    const v011 = flat(log.slice(log.indexOf("## 0.1.1"), log.indexOf("## 0.1.0")));
    expect(v011).toMatch(/^## 0\.1\.1 \(2026-11-2[56]\)/);
    for (const fact of ["Hosted access is live", "Discord sign-in works", "https://claim.kiln.nexus", "**Undo**", "24 hours",
      "Kiln 0.2.0-dev.31.14 or newer, which is out now"]) expect(v011).toContain(fact);
  });
});
