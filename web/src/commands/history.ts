// The console's line history (security review S5): per account and per browser tab session (sessionStorage), never
// in localStorage where the next person on the browser would see it, and cleared at sign-out. The old browser-wide
// key ("kwa.console.history", before 0.1.0) is removed on sight.
export interface HistoryStore {
  readonly length: number;
  key(i: number): string | null;
  getItem(k: string): string | null;
  setItem(k: string, v: string): void;
  removeItem(k: string): void;
}

const PREFIX = "kwa.console.history.";
const LEGACY = "kwa.console.history";
const MAX = 100;

function stores(session?: HistoryStore, legacy?: HistoryStore): [HistoryStore | null, HistoryStore | null] {
  try {
    return [session ?? globalThis.sessionStorage ?? null, legacy ?? globalThis.localStorage ?? null];
  } catch {
    return [null, null]; // storage switched off
  }
}

function dropLegacy(legacy: HistoryStore | null) {
  try {
    legacy?.removeItem(LEGACY);
  } catch {
    // ignore
  }
}

export function loadHistory(account: string, session?: HistoryStore, legacy?: HistoryStore): string[] {
  const [s, l] = stores(session, legacy);
  dropLegacy(l);
  try {
    const v = JSON.parse(s?.getItem(PREFIX + account) ?? "[]");
    return Array.isArray(v) ? v.filter((x): x is string => typeof x === "string").slice(-MAX) : [];
  } catch {
    return [];
  }
}

export function saveHistory(account: string, lines: string[], session?: HistoryStore, legacy?: HistoryStore) {
  const [s, l] = stores(session, legacy);
  dropLegacy(l);
  try {
    s?.setItem(PREFIX + account, JSON.stringify(lines.slice(-MAX)));
  } catch {
    // private mode or full
  }
}

/** Every account's history in this tab (sign-out, a different account signing in). */
export function clearHistories(session?: HistoryStore, legacy?: HistoryStore) {
  const [s, l] = stores(session, legacy);
  dropLegacy(l);
  if (!s) return;
  try {
    const keys: string[] = [];
    for (let i = 0; i < s.length; i++) {
      const k = s.key(i);
      if (k?.startsWith(PREFIX)) keys.push(k);
    }
    for (const k of keys) s.removeItem(k);
  } catch {
    // ignore
  }
}
