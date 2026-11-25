// The admin pages' socket (admin-protocol.md section 12): the auth lane's KAP gateway at /api/server/kap, signed in by
// the session cookie (no ticket in the URL). The gateway stamps every command line with the user's source and level, so
// the page never sends them. @kiln/admin-ui's transport treats close codes 4401 and 4403 as final but does not say which
// one it was; the WebSocket class handed to it reports them here, so the app can send the user back to sign-in (4401)
// or show why the server refused (4403).
import type { WebSocketClass, WebSocketLike } from "@kiln/admin-ui";

export const KAP_PATH = "/api/server/kap";
/** The one "server" the web app administers (the admin pages' server id: their URLs are /servers/this/admin/...). */
export const SERVER_ID = "this";

export const NOT_SIGNED_IN = 4401;
export const NOT_ALLOWED = 4403;

/** ws(s)://<this host>/api/server/kap for the page at `loc`. */
export function kapUrl(loc: { protocol: string; host: string } = location): string {
  return `${loc.protocol === "https:" ? "wss" : "ws"}://${loc.host}${KAP_PATH}`;
}

type Listener = (ev: never) => void;

/** A WebSocket class that tells `onAuthClose` about closes with 4401 or 4403 (before the transport's own listener runs). */
export function trackingWebSocket(onAuthClose: (code: number, reason: string) => void, Base?: WebSocketClass, onReauth?: () => void): WebSocketClass {
  const Inner = Base ?? (globalThis as unknown as { WebSocket: WebSocketClass }).WebSocket;
  return class TrackingWebSocket implements WebSocketLike {
    private readonly ws: WebSocketLike;
    constructor(url: string) {
      this.ws = new Inner(url);
      if (onReauth) this.ws.addEventListener("message", ((ev: { data: unknown }) => {
        if (typeof ev.data !== "string") return;
        try {
          const frame = JSON.parse(ev.data);
          if (frame?.t === "res" && frame.ok === false && frame.code === "reauth_required") onReauth();
        } catch { /* The actual transport validates malformed frames. */ }
      }) as Listener);
      this.ws.addEventListener("close", ((ev: { code: number; reason: string }) => {
        if (ev.code === NOT_SIGNED_IN || ev.code === NOT_ALLOWED) onAuthClose(ev.code, ev.reason ?? "");
      }) as Listener);
    }
    get readyState() {
      return this.ws.readyState;
    }
    get binaryType() {
      return this.ws.binaryType;
    }
    set binaryType(v: string) {
      this.ws.binaryType = v;
    }
    send(data: string) {
      this.ws.send(data);
    }
    close(code?: number, reason?: string) {
      this.ws.close(code, reason);
    }
    addEventListener(type: "open" | "message" | "error" | "close", listener: Listener) {
      this.ws.addEventListener(type, listener);
    }
  };
}
