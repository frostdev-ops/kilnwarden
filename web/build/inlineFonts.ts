// The vendored admin-ui stylesheet carries its fonts as data: URLs (vite's library mode inlines every asset). The
// panel's CSP allows fonts from 'self' only (font-src 'self', no data:), so this plugin turns each inlined font back into
// a hashed file next to the stylesheet (assets/font-<hash>.woff2) and points the url() at it. Pure function for the
// unit test; the plugin applies it to every CSS asset of the bundle.
import { createHash } from "node:crypto";
import type { Plugin } from "vite";

const FONT_DATA = /url\(\s*(["']?)data:(font\/(?:woff2|woff|ttf|otf)|application\/(?:font-woff2?|x-font-ttf|x-font-woff));base64,([A-Za-z0-9+/=]+)\1\s*\)/g;

const EXT: Record<string, string> = {
  "font/woff2": "woff2", "font/woff": "woff", "font/ttf": "ttf", "font/otf": "otf",
  "application/font-woff2": "woff2", "application/font-woff": "woff", "application/x-font-ttf": "ttf", "application/x-font-woff": "woff",
};

export interface ExtractedFont {
  fileName: string;
  bytes: Uint8Array;
}

/** Replaces every inlined font in `css` with url(<prefix><file>); returns the new CSS and the files to write. */
export function extractInlineFonts(css: string, prefix = ""): { css: string; fonts: ExtractedFont[] } {
  const fonts = new Map<string, ExtractedFont>();
  const out = css.replace(FONT_DATA, (_m, _q, mime: string, b64: string) => {
    const bytes = Uint8Array.from(Buffer.from(b64, "base64"));
    const hash = createHash("sha256").update(bytes).digest("hex").slice(0, 12);
    const fileName = `font-${hash}.${EXT[mime] ?? "bin"}`;
    fonts.set(fileName, { fileName, bytes });
    return `url(${prefix}${fileName})`;
  });
  return { css: out, fonts: [...fonts.values()] };
}

export function inlineFonts(): Plugin {
  return {
    name: "kwa-inline-fonts",
    apply: "build",
    generateBundle(_opts, bundle) {
      for (const asset of Object.values(bundle)) {
        if (asset.type !== "asset" || !asset.fileName.endsWith(".css")) continue;
        const text = typeof asset.source === "string" ? asset.source : new TextDecoder().decode(asset.source);
        const { css, fonts } = extractInlineFonts(text);
        if (fonts.length === 0) continue;
        const dir = asset.fileName.includes("/") ? asset.fileName.slice(0, asset.fileName.lastIndexOf("/") + 1) : "";
        for (const f of fonts) {
          if (!bundle[dir + f.fileName]) this.emitFile({ type: "asset", fileName: dir + f.fileName, source: f.bytes });
        }
        asset.source = css;
      }
    },
  };
}
