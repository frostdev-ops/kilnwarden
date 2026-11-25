// Plain-language numbers and times for the pages (unit tested): a duration, how long ago, a server message as a sentence.

/** Seconds as "45 s", "12 min", "1 h 5 min", "3 d 4 h". */
export function duration(seconds: number | null | undefined): string {
  if (seconds === null || seconds === undefined || !Number.isFinite(seconds)) return "–";
  const s = Math.max(0, Math.round(seconds));
  if (s < 60) return `${s} s`;
  const m = Math.floor(s / 60);
  if (m < 60) return `${m} min`;
  const h = Math.floor(m / 60);
  if (h < 24) return m % 60 ? `${h} h ${m % 60} min` : `${h} h`;
  const d = Math.floor(h / 24);
  return h % 24 ? `${d} d ${h % 24} h` : `${d} d`;
}

/** An ISO time as "just now", "5 min ago", "3 h ago", "2 d ago" (or "in 5 min" for the future). */
export function ago(iso: string | null | undefined, now = Date.now()): string {
  if (!iso) return "never";
  const t = new Date(iso).getTime();
  if (Number.isNaN(t)) return iso;
  const diff = (now - t) / 1000;
  if (Math.abs(diff) < 45) return "just now";
  return diff > 0 ? `${duration(diff)} ago` : `in ${duration(-diff)}`;
}

/** A server message as a sentence: a capital first letter and a full stop. */
export function sentence(text: string): string {
  const t = text.trim();
  if (!t) return t;
  const first = t[0].toUpperCase() + t.slice(1);
  return /[.!?…)]$/.test(first) ? first : `${first}.`;
}
