// After `vite build`: checks dist/ against what the panel's server allows, then writes a brotli file next to every
// compressible one (the mod's StaticFiles serves <file>.br to browsers that accept br).
// The checks (CSP: default-src 'self', script-src 'self', style-src 'self', font-src 'self', img-src 'self' data: blob:):
//   - index.html has no inline script, no inline style element or attribute, no event handler attribute;
//   - no file loads anything from another origin (http://, https://, protocol-relative //) in CSS url() or HTML src/href;
//   - no CSS has a data: font (the inlineFonts plugin turns them into files);
//   - every asset under assets/ has a content hash in its name (they are served as immutable).
//   bun scripts/postbuild.ts [dist]
import { brotliCompressSync, constants } from "node:zlib";
import { readdirSync, readFileSync, statSync, writeFileSync } from "node:fs";
import { join, relative } from "node:path";
import { checkDist } from "./distRules";

const dist = process.argv[2] ?? join(import.meta.dir, "..", "dist");
const walk = (d: string): string[] => readdirSync(d).flatMap((n) => (statSync(join(d, n)).isDirectory() ? walk(join(d, n)) : [join(d, n)]));
const files = walk(dist).filter((f) => !f.endsWith(".br"));
const problems = checkDist(files.map((f) => ({ path: relative(dist, f).replaceAll("\\", "/"), text: /\.(html|css|js|svg|json|webmanifest)$/.test(f) ? readFileSync(f, "utf8") : null })));
if (problems.length) {
  console.error(`web/dist: ${problems.length} problem(s):`);
  for (const p of problems) console.error(`  ${p}`);
  process.exit(1);
}
let n = 0, raw = 0, packed = 0;
for (const f of files) {
  if (!/\.(html|css|js|mjs|svg|json|webmanifest|txt|ico)$/.test(f)) continue;
  const data = readFileSync(f);
  if (data.length < 1024) continue;
  const br = brotliCompressSync(data, { params: { [constants.BROTLI_PARAM_QUALITY]: 11, [constants.BROTLI_PARAM_SIZE_HINT]: data.length } });
  if (br.length >= data.length * 0.95) continue;
  writeFileSync(f + ".br", br);
  n++; raw += data.length; packed += br.length;
}
console.log(`web/dist: ok, ${files.length} files; ${n} brotli files (${(raw / 1024).toFixed(0)} KiB -> ${(packed / 1024).toFixed(0)} KiB)`);
