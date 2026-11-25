// The web app's build: dist/ = index.html + assets/<name>-<hash>.{js,css,woff2,...} (the mod's StaticFiles serves
// assets/ as immutable and revalidates the rest), then scripts/postbuild.ts checks it against the panel's CSP and writes
// the brotli files. @kiln/admin-ui is the vendored build in vendor/admin-ui (scripts/refresh-admin-ui.sh).
import { fileURLToPath } from "node:url";
import react from "@vitejs/plugin-react";
import { defineConfig } from "vite";
import { inlineFonts } from "./build/inlineFonts.ts";

const here = fileURLToPath(new URL(".", import.meta.url));

export default defineConfig({
  root: here,
  base: "/",
  plugins: [react(), inlineFonts()],
  resolve: {
    alias: [
      { find: /^@kiln\/admin-ui\/style\.css$/, replacement: `${here}vendor/admin-ui/dist/admin-ui.css` },
      { find: /^@kiln\/admin-ui$/, replacement: `${here}vendor/admin-ui/dist/admin-ui.js` },
    ],
    // one React, one router, one query client for the app and the vendored pages
    dedupe: ["react", "react-dom", "react-router-dom", "@tanstack/react-query", "zustand"],
  },
  build: {
    outDir: "dist",
    emptyOutDir: true,
    assetsDir: "assets",
    target: "es2022",
    sourcemap: false,
    // never inline assets as data: URLs (fonts must be files under the CSP; images may be, but files cache better)
    assetsInlineLimit: 0,
    cssCodeSplit: true,
    chunkSizeWarningLimit: 1500,
    rollupOptions: {
      output: {
        manualChunks(id) {
          if (id.includes("vendor/admin-ui") || id.includes("node_modules/leaflet")) return "admin-ui";
          if (id.includes("node_modules/react") || id.includes("node_modules/scheduler") || id.includes("node_modules/@tanstack") || id.includes("node_modules/react-router")) return "react";
          return undefined;
        },
      },
    },
  },
  server: {
    port: 5191,
    strictPort: true,
    // dev only: the API and the KAP socket from a running host (tools/devhost) on KWA_DEV_API
    proxy: process.env.KWA_DEV_API ? { "/api": { target: process.env.KWA_DEV_API, ws: true, changeOrigin: false } } : undefined,
  },
});
