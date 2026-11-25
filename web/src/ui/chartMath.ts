// Pure helpers of the line charts (unit tested): a nice axis, value formatting by unit, the nearest point to the pointer.

/** A round axis top at or above max (1, 2, 2.5, 5 x 10^n); 1 for an empty or zero series. */
export function niceMax(max: number): number {
  if (!Number.isFinite(max) || max <= 0) return 1;
  const p = 10 ** Math.floor(Math.log10(max));
  for (const m of [1, 2, 2.5, 5, 10]) if (m * p >= max) return m * p;
  return 10 * p;
}

export function ticks(top: number, n = 4): number[] {
  return Array.from({ length: n + 1 }, (_, i) => (top * i) / n);
}

export function formatValue(v: number | null | undefined, unit: string | null): string {
  if (v === null || v === undefined || !Number.isFinite(v)) return "–";
  switch (unit) {
    case "bytes": {
      const u = ["B", "KB", "MB", "GB", "TB"];
      let x = v;
      let i = 0;
      while (Math.abs(x) >= 1024 && i < u.length - 1) {
        x /= 1024;
        i++;
      }
      return `${x >= 100 || i === 0 ? Math.round(x) : x.toFixed(1)} ${u[i]}`;
    }
    case "ms": return `${v >= 100 ? Math.round(v) : v.toFixed(1)} ms`;
    case "%": return `${Math.round(v)}%`;
    case "count": return String(Math.round(v));
    case "1/s": return `${v.toFixed(1)}/s`;
    case "s": return durationShort(v);
    default: return Number.isInteger(v) ? String(v) : v.toFixed(2);
  }
}

/** The index of the time closest to x (t sorted ascending); -1 when empty. */
export function nearestIndex(t: number[], x: number): number {
  if (!t.length) return -1;
  let lo = 0;
  let hi = t.length - 1;
  while (hi - lo > 1) {
    const mid = (lo + hi) >> 1;
    if (t[mid] <= x) lo = mid;
    else hi = mid;
  }
  return Math.abs(t[lo] - x) <= Math.abs(t[hi] - x) ? lo : hi;
}

/** SVG path of a series with gaps where values are null. */
export function linePath(t: number[], v: (number | null)[], x: (t: number) => number, y: (v: number) => number): string {
  let d = "";
  let pen = false;
  for (let i = 0; i < t.length; i++) {
    const val = v[i];
    if (val === null || val === undefined || !Number.isFinite(val)) {
      pen = false;
      continue;
    }
    d += `${pen ? "L" : "M"}${x(t[i]).toFixed(1)},${y(val).toFixed(1)}`;
    pen = true;
  }
  return d;
}

/** Seconds, short: "45 s", "12 min", "3.5 h", "2.1 d" (an axis label or a chart's latest value). */
export function durationShort(s: number): string {
  const a = Math.abs(s);
  if (a < 90) return `${Math.round(s)} s`;
  if (a < 90 * 60) return `${Math.round(s / 60)} min`;
  if (a < 36 * 3600) return `${(s / 3600).toFixed(s / 3600 >= 10 ? 0 : 1)} h`;
  return `${(s / 86400).toFixed(1)} d`;
}
