// The command forms' pickers as line text (unit/units.test.ts): a duration from a number and a unit ("90s", "5m",
// "1h"), a time of day from an hour:minute input ("07:30"), both as Kiln's argument parser reads them
// (docs/kiln/commands.md: Duration takes 90s, 5m, 1h30m, 250ms, 00:05:00 or seconds; TimeOfDay takes 7, 07:30, 7pm,
// noon, dusk), and a click on the world plane as world x and z (0..10240 m, x to the right, z up = north).

export type DurationUnit = "s" | "m" | "h";
export const DURATION_UNITS: { value: DurationUnit; label: string; seconds: number }[] = [
  { value: "s", label: "seconds", seconds: 1 }, { value: "m", label: "minutes", seconds: 60 }, { value: "h", label: "hours", seconds: 3600 },
];

/** "90" + "s" -> "90s"; an empty or invalid amount -> "". */
export function durationText(amount: string, unit: DurationUnit): string {
  const a = amount.trim();
  if (!/^\d+(\.\d+)?$/.test(a)) return "";
  return `${a}${unit}`;
}

/** "90s" -> {amount: "90", unit: "s"}; anything else (1h30m, 00:05:00) -> null. */
export function splitDuration(text: string): { amount: string; unit: DurationUnit } | null {
  const m = /^(\d+(?:\.\d+)?)(s|m|h)$/.exec(text.trim());
  return m ? { amount: m[1], unit: m[2] as DurationUnit } : null;
}

/** Seconds in a duration as Kiln reads it, or null when it cannot tell: 90s, 5m, 1h30m, 250ms, 00:05:00, 90. */
export function durationSeconds(text: string): number | null {
  const t = text.trim().toLowerCase();
  if (!t) return null;
  if (/^\d+(\.\d+)?$/.test(t)) return Number(t);
  const clock = /^(\d+):(\d{2})(?::(\d{2}))?$/.exec(t);
  if (clock) return clock[3] !== undefined ? Number(clock[1]) * 3600 + Number(clock[2]) * 60 + Number(clock[3]) : Number(clock[1]) * 60 + Number(clock[2]);
  const parts = t.match(/\d+(?:\.\d+)?(?:ms|h|m|s)/g);
  if (!parts || parts.join("") !== t) return null;
  let s = 0;
  for (const p of parts) {
    const n = parseFloat(p);
    s += p.endsWith("ms") ? n / 1000 : p.endsWith("h") ? n * 3600 : p.endsWith("m") ? n * 60 : n;
  }
  return s;
}

/** An hour:minute input's value ("07:30") as the line's text (the same); "" stays "". */
export function timeText(hhmm: string): string {
  const m = /^(\d{1,2}):(\d{2})$/.exec(hhmm.trim());
  if (!m) return "";
  const h = Number(m[1]), min = Number(m[2]);
  if (h > 24 || min > 59 || (h === 24 && min > 0)) return "";
  return `${String(h).padStart(2, "0")}:${m[2]}`;
}

/** A time of day as hours (7.5 for 07:30, 19 for 7pm, 12 for noon), or null when it is a name Kiln resolves itself. */
export function timeHours(text: string): number | null {
  const t = text.trim().toLowerCase();
  if (/^\d+(\.\d+)?$/.test(t)) return Number(t);
  const clock = /^(\d{1,2}):(\d{2})$/.exec(t);
  if (clock) return Number(clock[1]) + Number(clock[2]) / 60;
  const ampm = /^(\d{1,2})(?::(\d{2}))?\s*(am|pm)$/.exec(t);
  if (ampm) return (Number(ampm[1]) % 12) + (ampm[3] === "pm" ? 12 : 0) + (ampm[2] ? Number(ampm[2]) / 60 : 0);
  if (t === "noon") return 12;
  if (t === "midnight") return 0;
  return null;
}

/** The world plane's size in metres (the Enshrouded map is 10240 m square). */
export const WORLD = 10240;

/** A click at (px, py) on a plane drawn w by h pixels (y down) as world x and z (z up), rounded to the metre. */
export function planeToWorld(px: number, py: number, w: number, h: number): { x: number; z: number } {
  const clamp = (v: number) => Math.min(WORLD, Math.max(0, v));
  return { x: Math.round(clamp((px / w) * WORLD)), z: Math.round(clamp((1 - py / h) * WORLD)) };
}

/** World x and z as a point on a plane of w by h pixels (y down). */
export function worldToPlane(x: number, z: number, w: number, h: number): { px: number; py: number } {
  return { px: (x / WORLD) * w, py: (1 - z / WORLD) * h };
}

/** Metres between two points on the ground (x and z). */
export function groundDistance(a: { x: number; z: number }, b: { x: number; z: number }): number {
  return Math.hypot(a.x - b.x, a.z - b.z);
}
