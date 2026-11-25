import { describe, expect, test } from "bun:test";
import { extractInlineFonts } from "../build/inlineFonts";
import { checkDist } from "../scripts/distRules";

describe("inline fonts", () => {
  test("data: fonts become hashed files next to the stylesheet", () => {
    const b64 = Buffer.from("wOF2-fake-font").toString("base64");
    const css = `@font-face{font-family:A;src:url(data:font/woff2;base64,${b64}) format("woff2")}@font-face{font-family:B;src:url("data:font/woff2;base64,${b64}")}.x{background:url(data:image/png;base64,iVBOR)}`;
    const { css: out, fonts } = extractInlineFonts(css);
    expect(fonts.length).toBe(1);
    expect(fonts[0].fileName).toMatch(/^font-[0-9a-f]{12}\.woff2$/);
    expect(Buffer.from(fonts[0].bytes).toString()).toBe("wOF2-fake-font");
    expect(out).not.toContain("data:font");
    expect(out).toContain(`url(${fonts[0].fileName}) format("woff2")`);
    expect(out).toContain("data:image/png");
  });
});

describe("dist rules", () => {
  const ok = [
    { path: "index.html", text: '<!doctype html><html><head><script type="module" crossorigin src="/assets/index-AbCdEf12.js"></script><link rel="stylesheet" href="/assets/index-AbCdEf12.css"></head><body><div id="root"></div></body></html>' },
    { path: "assets/index-AbCdEf12.js", text: "console.log(1)" },
    { path: "assets/index-AbCdEf12.css", text: "@font-face{src:url(font-0123456789ab.woff2)} .a{background:url(\"data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg'/%3E\")}" },
    { path: "assets/font-0123456789ab.woff2", text: null },
    { path: "favicon.svg", text: "<svg/>" },
  ];

  test("a good build passes", () => expect(checkDist(ok)).toEqual([]));

  test("inline scripts, styles, handlers, CDNs, data fonts and unhashed assets fail", () => {
    const bad = [
      { path: "index.html", text: '<script>alert(1)</script><style>a{}</style><div style="x" onclick="y"></div><script src="https://cdn.example/x.js"></script>' },
      { path: "assets/app.js", text: "" },
      { path: "assets/x-AbCdEf12.css", text: "@import url(https://fonts.example/css);@font-face{src:url(data:font/woff2;base64,AAAA)} .b{background:url(//cdn.example/i.png)}" },
    ];
    const p = checkDist(bad).join("\n");
    for (const s of ["inline script", "a style element", "a style attribute", "event handler", "loads https://cdn.example/x.js", "content hash", "data: font", "imports https://fonts.example/css", "loads //cdn.example/i.png"])
      expect(p).toContain(s);
  });
});

test('vendored built admin-ui matches its recorded upstream SHA and file hashes', async () => {
  const { createHash } = await import('node:crypto');
  const { readFileSync } = await import('node:fs');
  const { resolve } = await import('node:path');
  const dir = resolve(import.meta.dir, '../vendor/admin-ui');
  const manifest = JSON.parse(readFileSync(resolve(dir, 'VENDOR.json'), 'utf8')) as { kilnCommit: string; built: string; files: Record<string, string> };
  expect(manifest.kilnCommit).toMatch(/^[a-f0-9]{40}$/);
  expect(manifest.built).toBe('bun run build:admin-ui (launcher/)');
  for (const [path, hash] of Object.entries(manifest.files)) {
    expect(path).not.toMatch(/\.map$/);
    expect(createHash('sha256').update(readFileSync(resolve(dir, path))).digest('hex')).toBe(hash);
  }
});
