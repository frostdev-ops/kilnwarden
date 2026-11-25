// The host side of a custom page's postMessage protocol (kwa: 1, docs/step2-lane-c.md C1). The iframe is sandboxed
// without same-origin, so its messages come from origin "null"; the host accepts them only from that iframe's window,
// at most MAX_BYTES each and RATE per second, and only for names the module declared and the user's permissions allow.
// The server checks every call again: this filter is defence in depth, not the gate.

export const MAX_BYTES = 64 * 1024;
export const RATE = 20;

export type FrameRequest =
  | { kwa: 1; type: "call"; id: number; action: string; args: Record<string, unknown> }
  | { kwa: 1; type: "query"; id: number; query: string; params: Record<string, string> }
  | { kwa: 1; type: "subscribe" | "unsubscribe"; topic: string }
  | { kwa: 1; type: "resize"; height: number }
  | { kwa: 1; type: "ready" };

const NAME = /^[a-z0-9][a-z0-9_-]{0,63}$/;
const plain = (v: unknown): v is Record<string, unknown> => !!v && typeof v === "object" && !Array.isArray(v) && Object.getPrototypeOf(v) === Object.prototype;

/** The message as a request, or a reason it is refused. */
export function parseFrameMessage(data: unknown): FrameRequest | { refused: string } {
  let size: number;
  try {
    size = JSON.stringify(data)?.length ?? 0;
  } catch {
    return { refused: "not JSON" };
  }
  if (size > MAX_BYTES) return { refused: "too large" };
  if (!plain(data) || data.kwa !== 1) return { refused: "not a kwa/1 message" };
  switch (data.type) {
    case "ready":
      return { kwa: 1, type: "ready" };
    case "resize":
      return typeof data.height === "number" && Number.isFinite(data.height) ? { kwa: 1, type: "resize", height: Math.max(0, Math.min(20000, Math.round(data.height))) } : { refused: "bad height" };
    case "subscribe":
    case "unsubscribe":
      return typeof data.topic === "string" && NAME.test(data.topic) ? { kwa: 1, type: data.type, topic: data.topic } : { refused: "bad topic" };
    case "call":
      if (!Number.isSafeInteger(data.id) || typeof data.action !== "string" || !NAME.test(data.action)) return { refused: "bad call" };
      if (data.args !== undefined && !plain(data.args)) return { refused: "args must be an object" };
      return { kwa: 1, type: "call", id: data.id as number, action: data.action, args: (data.args as Record<string, unknown>) ?? {} };
    case "query": {
      if (!Number.isSafeInteger(data.id) || typeof data.query !== "string" || !NAME.test(data.query)) return { refused: "bad query" };
      const params: Record<string, string> = {};
      if (data.params !== undefined) {
        if (!plain(data.params)) return { refused: "params must be an object" };
        for (const [k, v] of Object.entries(data.params)) {
          if (typeof v !== "string" && typeof v !== "number" && typeof v !== "boolean") return { refused: "params are strings" };
          params[k] = String(v);
        }
      }
      return { kwa: 1, type: "query", id: data.id as number, query: data.query, params };
    }
    default:
      return { refused: "unknown type" };
  }
}

/** Whether a message event comes from the frame we host: its window, and the opaque origin of a sandboxed document. */
export function fromOurFrame(e: { source: unknown; origin: string }, frame: unknown): boolean {
  return !!frame && e.source === frame && e.origin === "null";
}

/** At most RATE messages per rolling second. */
export function rateLimiter(rate = RATE, now: () => number = () => Date.now()) {
  const times: number[] = [];
  return () => {
    const t = now();
    while (times.length && t - times[0] >= 1000) times.shift();
    if (times.length >= rate) return false;
    times.push(t);
    return true;
  };
}

/** What the module lets this user name from the frame (from its declarative description or the nav). */
export interface FrameAllow { actions: Set<string>; queries: Set<string>; topics: Set<string> }

export function allowed(req: FrameRequest, allow: FrameAllow): boolean {
  switch (req.type) {
    case "call": return allow.actions.has(req.action);
    case "query": return allow.queries.has(req.query);
    case "subscribe": case "unsubscribe": return allow.topics.has(req.topic);
    default: return true;
  }
}

/** The frame's document must be the one the panel loaded (security review S6): a custom page that navigates its frame
 * (to another panel URL: the panel's frame-src 'self' blocks every other site) gets a second load event, and from then
 * on nothing is answered or forwarded until the user reloads the page. */
export function frameGuard() {
  let loads = 0;
  return {
    loaded() { loads++; },
    accepts() { return loads === 1; },
    navigated() { return loads > 1; },
  };
}
