import type { AdminTransport } from "./admin";
import { type ServerId } from "./shared";
/** The WebSocket class (the browser's by default; tests and Node may pass their own). */
export interface WebSocketLike {
    readonly readyState: number;
    binaryType: string;
    send(data: string): void;
    close(code?: number, reason?: string): void;
    addEventListener(type: "open" | "message" | "error" | "close", listener: (ev: never) => void): void;
}
export type WebSocketClass = new (url: string) => WebSocketLike;
export interface WebSocketTransportOptions {
    /** The signed-in socket's URL for a server: a string, or a function called at every (re)connect (so the host can hand
     * out a fresh ticket). A function that throws a KilnError with code `auth_failed` ends the session (signed out). */
    url: string | ((serverId: ServerId) => string | Promise<string>);
    WebSocket?: WebSocketClass;
    /** Reconnect backoff, first and largest delay (default 1 s, 30 s). */
    backoffMs?: readonly [number, number];
    /** How long a connect waits for `ready` (default 10 s). */
    handshakeMs?: number;
    /** How long a request may take (default: the op's budget + 10 s, as the launcher). */
    callTimeoutMs?: (op: string) => number;
}
/** The transport, plus closing every session (page unload, tests). */
export interface WebSocketAdminTransport extends AdminTransport {
    disconnectAll(): Promise<void>;
}
/** An op's budget in seconds (admin-protocol.md section 4; Rust protocol::op_budget). */
export declare function opBudgetS(op: string): number;
/** An AdminTransport over KAP/1-over-WebSocket. */
export declare function createWebSocketTransport(opts: WebSocketTransportOptions): WebSocketAdminTransport;
