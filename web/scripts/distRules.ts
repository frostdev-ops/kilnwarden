// The rules scripts/postbuild.ts holds the built app to (and unit/dist-rules.test.ts tests): what the panel's CSP and
// the static file server expect. Each file: its path under dist/ and its text (null for binary files).
export interface DistFile {
  path: string;
  text: string | null;
}

const HASHED = /-[A-Za-z0-9_-]{8,}\.[a-z0-9]+$/;

export function checkDist(files: DistFile[]): string[] {
  const p: string[] = [];
  if (!files.some((f) => f.path === "index.html")) p.push("no index.html");
  for (const f of files) {
    if (f.path.startsWith("assets/") && !HASHED.test(f.path)) p.push(`${f.path}: assets are served as immutable, so their names need a content hash`);
    if (f.text === null) continue;
    if (f.path.endsWith(".html")) {
      for (const m of f.text.matchAll(/<script\b([^>]*)>([\s\S]*?)<\/script>/gi)) {
        if (m[2].trim()) p.push(`${f.path}: inline script (CSP script-src 'self')`);
        if (!/\bsrc=/.test(m[1])) p.push(`${f.path}: a script without src`);
      }
      if (/<style\b/i.test(f.text)) p.push(`${f.path}: a style element (CSP style-src 'self')`);
      if (/\sstyle\s*=/i.test(f.text)) p.push(`${f.path}: a style attribute (CSP style-src 'self')`);
      if (/\son[a-z]+\s*=/i.test(f.text)) p.push(`${f.path}: an event handler attribute (CSP script-src 'self')`);
      for (const m of f.text.matchAll(/\s(?:src|href)\s*=\s*["']?((?:https?:)?\/\/[^"'\s>]+)/gi)) p.push(`${f.path}: loads ${m[1]} (no CDN: everything is local)`);
    }
    if (f.path.endsWith(".css")) {
      if (/url\(\s*["']?data:(?:font\/|application\/(?:font|x-font))/i.test(f.text)) p.push(`${f.path}: a data: font (CSP font-src 'self')`);
      for (const m of f.text.matchAll(/url\(\s*["']?((?:https?:)?\/\/[^"')\s]+)/gi)) p.push(`${f.path}: loads ${m[1]} (no CDN: everything is local)`);
      for (const m of f.text.matchAll(/@import\s+(?:url\()?\s*["']?((?:https?:)?\/\/[^"')\s;]+)/gi)) p.push(`${f.path}: imports ${m[1]}`);
    }
  }
  return p;
}
