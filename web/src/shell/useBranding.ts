// The server's branding on the page: the title, the favicon, and the accent as the design system's ember tokens
// (set through the CSSOM, which the CSP allows; no style element).
import { useEffect } from "react";
import type { Branding } from "../api/types";

/** #rrggbb mixed with white (t > 0) or black (t < 0). */
export function shade(hex: string, t: number): string {
  const n = parseInt(hex.slice(1), 16);
  const ch = [(n >> 16) & 255, (n >> 8) & 255, n & 255].map((c) => Math.round(t >= 0 ? c + (255 - c) * t : c * (1 + t)));
  return "#" + ch.map((c) => Math.min(255, Math.max(0, c)).toString(16).padStart(2, "0")).join("");
}

export const ACCENT_VARS = ["--k-ember-300", "--k-ember-400", "--k-ember-500", "--k-ember-600", "--k-ember-700"] as const;

export function accentVars(accent: string): Record<(typeof ACCENT_VARS)[number], string> {
  return { "--k-ember-300": shade(accent, 0.45), "--k-ember-400": shade(accent, 0.2), "--k-ember-500": accent, "--k-ember-600": shade(accent, -0.15), "--k-ember-700": shade(accent, -0.35) };
}

export function useBranding(b: Branding, section?: string) {
  useEffect(() => {
    document.title = section ? `${section} · ${b.name}` : b.name;
  }, [b.name, section]);
  useEffect(() => {
    const link = document.querySelector<HTMLLinkElement>('link[rel="icon"]');
    if (link) link.href = b.favicon ?? "/favicon.svg";
  }, [b.favicon]);
  useEffect(() => {
    const root = document.documentElement.style;
    if (b.accent && /^#[0-9a-f]{6}$/i.test(b.accent)) for (const [k, v] of Object.entries(accentVars(b.accent))) root.setProperty(k, v);
    else for (const k of ACCENT_VARS) root.removeProperty(k);
  }, [b.accent]);
}
