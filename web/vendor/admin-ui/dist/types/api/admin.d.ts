import { type QueryClient, type UseMutationOptions, type UseQueryOptions } from "@tanstack/react-query";
import { type ReactNode } from "react";
import { type Json, type KilnError, type ServerId } from "./shared";
import type { AdminInfo, LoginMintResult, LoginRedeemResult, RestartRequest, ServerRestartArgs, UpdateCancelResult, UpdateList, UpdateRollbackResult, UpdateStageArgs, UpdateStageResult } from "../admin/types";
export declare const KAP_PROTOCOL = "kap/1";
export type KapErrorCode = "bad_frame" | "too_large" | "auth_required" | "auth_failed" | "version" | "unknown_op" | "op_failed" | "busy" | "unknown_topic" | "timeout" | "shutting_down" | "internal";
export interface KapLimits {
    maxFrame: number;
    maxClientFrame: number;
    inflight: number;
    pingS: number;
    idleS: number;
}
export type KapFrame = {
    t: "hello";
    proto: string;
    nonce: string;
    kiln?: string;
    admin?: string;
    server: {
        id?: string | null;
        name?: string | null;
    };
    auth: string[];
} | {
    t: "auth";
    proto: string;
    cnonce: string;
    mac: string;
    client?: string;
} | {
    t: "ready";
    session: string;
    mac: string;
    ops: string[];
    topics: string[];
    limits: KapLimits;
    restart?: KapRestart;
} | {
    t: "req";
    id: number;
    op: string;
    args: Record<string, Json>;
} | {
    t: "res";
    id: number;
    ok: boolean;
    op: string;
    ms?: number;
    data?: Json;
    error?: string;
    code?: KapErrorCode;
} | {
    t: "sub";
    id: number;
    topic: string;
    everyMs?: number;
} | {
    t: "unsub";
    id: number;
    topic: string;
} | {
    t: "push";
    topic: string;
    seq: number;
    data: Json;
} | {
    t: "ping";
    id: number;
    ts?: number;
} | {
    t: "pong";
    id: number;
    ts?: number;
} | {
    t: "error";
    code: KapErrorCode;
    message: string;
    id?: number;
} | {
    t: "bye";
    reason: string;
};
/** ready.restart / admin_info.restart (0.2.0-dev.30.9): does a stop asked for with server_restart come back? */
export interface KapRestart {
    supported: "supported" | "unknown";
    /** "kilnctl" (its systemd unit restarts the server) or null */
    via: string | null;
    /** kilnctl run's exit code after such a stop (75), or null */
    code: number | null;
}
/** Topics Kiln.Admin offers (ready.topics lists the server's). "metrics": AdminMetrics every 2 s (since 30.9). */
export type AdminTopic = "state" | "log" | "metrics";
export interface AdminPos {
    x: number;
    y: number;
    z: number;
}
/** A player in the world (Ops.Players.cs PlayerList): name/steamId/admin/permissions, pos, health, god. */
export interface AdminPlayer {
    entity: number;
    name: string;
    steamId?: string;
    admin: boolean;
    permissions: string;
    pos?: AdminPos;
    health?: number;
    healthMax?: number;
    god?: boolean;
    [key: string]: unknown;
}
/** A player death from kiln.combat (state.combatDeaths): victim, killer, origin, where, unix seconds. */
export interface AdminCombatDeath {
    entity: number;
    killer: number;
    origin: string;
    pos: AdminPos;
    time: number;
    victimName?: string;
    killerKind?: "none" | "player" | "creature";
    killerName?: string;
    killerGuid?: string;
}
/** The `state` topic / state.json (AdminMod.State + Ops.AddState); fields grow with services. */
export interface AdminState {
    clock: number;
    kiln: string;
    ready: boolean;
    players?: AdminPlayer[];
    /** health-polling graves (health hit 0): clock = server seconds since boot */
    deaths?: {
        entity: number;
        pos: AdminPos;
        clock: number;
    }[];
    combatDeaths?: AdminCombatDeath[];
    /** enemies spawned by Kiln.Admin that are still alive */
    spawned?: number;
    mods?: {
        loaded: number;
        failed: number;
        disabled: number;
        phase: string;
    };
    [key: string]: unknown;
}
/** Ops every page can rely on (Ops*.cs); check others with useHasOp. */
export type KnownOp = "ping" | "status" | "players" | "enemies" | "teleport" | "teleport_to_player" | "inventory" | "give_item" | "item_search" | "spawn" | "spawn_item" | "despawn_spawned" | "settings_get" | "settings_set" | "settings_save" | "broadcast" | "tell" | "command" | "command_run" | "commands" | "time_get" | "time_set" | "weather_reset" | "health" | "heal" | "god" | "kill" | "damage" | "save_now" | "save_status" | "backup_list" | "backup_create" | "backup_restore" | "backup_cancel_restore" | "gameplay_status" | "zones_list" | "zones_add" | "zones_remove" | "content_status" | "content_get" | "content_set" | "content_reset" | "content_fields" | "content_search" | "content_entry" | "content_recipe" | "content_definitions" | "content_report" | "content_dump" | "content_reload" | "sync_status" | "config_list" | "config_get" | "config_set" | "config_reload" | "permissions_get" | "permissions_check" | "permissions_player_node" | "permissions_player_group" | "permissions_group" | "permissions_group_remove" | "permissions_map_user_group" | "permissions_reload" | "commands_describe" | "command_complete" | "events_stats" | "mods" | "mod_load" | "mod_reload" | "mod_unload" | "mod_test" | "crash_reports" | "admin_info" | "network_status" | "network_reload" | "native_status" | "log_tail" | "admin_token_rotate" | "server_restart" | "server_restart_cancel" | "login_mint" | "login_redeem" | "update_list" | "update_stage" | "update_cancel" | "update_rollback";
/** What the launcher may ask its backend for (Rust `Transport`): automatic, Kiln.Admin's TCP listener, the file bridge,
 * an SSH tunnel to a remote server. A transport lists the ones it offers in `kinds`. */
export type AdminTransportKind = "auto" | "tcp" | "file" | "ssh";
/** One server's admin session as the transport hands it out (Rust kiln_manage::admin::client::AdminSession). `session`
 * is the transport's id for it, stable across its own reconnects; `transport` says what carries it ("ws": a
 * WebSocket). */
export interface AdminSession {
    session: string;
    serverId: ServerId;
    transport: AdminTransportKind | "ws";
    ops: string[];
    topics: string[];
    kiln: string | null;
    server: {
        id?: string | null;
        name?: string | null;
    };
    /** ready.restart (0.2.0-dev.30.9) when the transport has it */
    restart?: KapRestart;
}
/** A session's events (Rust AdminEvent): pushes; `closed` (retry: the transport reconnects by itself and sends `ready`
 * again; false: final; absent: an older backend, the hub retries); `error` (a non-fatal KAP error frame); `ready`
 * after every reconnect. */
export type AdminEvent = {
    kind: "push";
    topic: string;
    seq: number;
    data: Json;
} | {
    kind: "closed";
    reason: string;
    retry?: boolean;
} | {
    kind: "error";
    message: string;
} | {
    kind: "ready";
    transport: AdminSession["transport"];
    ops: string[];
    topics: string[];
    restart?: KapRestart;
};
/** An event with its session (the launcher's `kiln://admin` payload). */
export interface AdminHubEvent {
    session: string;
    serverId: ServerId;
    event: AdminEvent;
}
/** Carries KAP/1 for the hub. Every method rejects with a KilnError: `not_found` for a session it does not know,
 * `admin_unavailable` while it reconnects, the op's error as `op_failed` (busy, timeout for those KAP codes). It keeps
 * one session per server (connect again returns it), re-subscribes after its own reconnects, and reports them as
 * `closed {retry: true}` then `ready`. */
export interface AdminTransport {
    /** For diagnostics: "tauri", "websocket", ... */
    readonly name: string;
    /** The kinds a user may pick (the Reconnect menu); empty: nothing to pick. */
    readonly kinds: readonly AdminTransportKind[];
    connect(serverId: ServerId, want: AdminTransportKind): Promise<AdminSession>;
    disconnect(session: string): Promise<void>;
    /** Runs an op; resolves to its `data`. */
    call<T = Json>(session: string, op: string, args: Record<string, Json>): Promise<T>;
    subscribe(session: string, topic: string): Promise<void>;
    unsubscribe(session: string, topic: string): Promise<void>;
    /** Every session's events; resolves to the unsubscribe function. */
    listen(handler: (e: AdminHubEvent) => void): Promise<() => void>;
}
/** The map art's state where the host makes it from the game (the launcher's Settings, Game art). */
export interface AdminArtStatus {
    map?: boolean;
    extracted?: string | null;
    canExtract?: boolean;
}
/** A crash report file the host found itself (logs_crash_reports). */
export interface AdminCrashFile {
    name: string;
    created: string;
    size: number;
}
/** What the pages need from the app around them, beyond KAP. A launcher action here is never an op: a KAP op of a
 * similar name (server_restart) goes through the transport (kapOps), these through the host. */
export interface AdminHost {
    /** Base URL of the local game art (map/..., data/...); undefined: /assets-local/ (catalog.ts assetsRoot). */
    assetsRoot?: () => string | undefined;
    /** A hook: the map art's state, to probe again when the host made it. */
    useArtStatus: () => AdminArtStatus | undefined;
    /** The launcher reads a stopped server's crash reports from its folder. */
    crashReportsFromDisk?: (serverId: ServerId) => Promise<AdminCrashFile[]>;
    /** The launcher restarts a server it manages (its own server_restart command, not Kiln.Admin's op). */
    restartManagedServer?: (serverId: ServerId) => Promise<unknown>;
}
/** Sets what the hub talks through and the app around the pages. Sessions of a previous transport are dropped (their
 * timers stopped; nothing is sent to it). The launcher calls this once (./adminTauri.ts); a web page before it renders
 * the pages. */
export declare function configureAdmin(c: {
    transport: AdminTransport | null;
    host?: Partial<AdminHost>;
}): void;
/** The configured transport (null before configureAdmin). */
export declare const adminTransport: () => AdminTransport | null;
/** The app around the pages (configureAdmin's host). */
export declare const adminHost: () => AdminHost;
export type AdminStatus = "idle" | "connecting" | "ready" | "reconnecting" | "error";
/** Everything the hub knows about one server's admin connection. */
export interface AdminConn {
    serverId: ServerId;
    status: AdminStatus;
    session: AdminSession | null;
    /** the transport kind asked for (connect); the session says which one it got */
    want: AdminTransportKind;
    error: KilnError | null;
    state: AdminState | null;
    stateSeq: number;
    /** Date.now() of the last state push */
    stateAt: number | null;
    connectedAt: number | null;
    /** failed connects in a row (backoff) */
    attempt: number;
    /** Date.now() of the next automatic try */
    retryAt: number | null;
    /** pushes that skipped a seq (slow client: Kiln.Admin dropped some) */
    gaps: number;
}
/** The contract's state shape (contracts.md section 13) plus the hub's details. */
export interface AdminHubState extends AdminConn {
    transport: AdminSession["transport"] | null;
    connecting: boolean;
    ready: boolean;
    ops: ReadonlySet<string>;
}
export type AdminCall = <T = Json>(op: KnownOp | string, args?: Record<string, Json>) => Promise<T>;
interface HubStore {
    conns: Record<ServerId, AdminConn>;
}
/** The hub's store (zustand). Pages read it through the hooks below. */
export declare const useAdminHub: import("zustand").UseBoundStore<import("zustand").StoreApi<HubStore>>;
/** no state push for this long: the connection bar says the state is stale */
export declare const ADMIN_STALE_MS = 6000;
type StateListener = (serverId: ServerId, state: AdminState) => void;
type TopicListener = (serverId: ServerId, topic: string, data: Json) => void;
/** Called with every state push of every server (src/admin/history.ts keeps deaths and seen players). */
export declare function onAdminState(l: StateListener): () => void;
/** Pushes of other topics (e.g. `log`). */
export declare function onAdminTopic(l: TopicListener): () => void;
/** A page started using the server's admin session. */
export declare function acquireAdmin(id: ServerId): void;
/** A page stopped using it; the last one closes the session after 30 s (page changes keep it). */
export declare function releaseAdmin(id: ServerId): void;
/** Reconnect now (the "Reconnect" button), optionally over another transport kind. */
export declare function reconnectAdmin(id: ServerId, kind?: AdminTransportKind): void;
/** Error for an op this server's Kiln.Admin does not serve. */
export declare const needsNewer: (op: string) => KilnError;
/** Runs an op on the server's shared session; data on ok, KilnError (op_failed with the op's message) otherwise. */
export declare function callAdmin<T = Json>(id: ServerId, op: string, args?: Record<string, Json>): Promise<T>;
/** Shares one admin session for `serverId` with everything below it. */
export declare function AdminProvider({ serverId, children }: {
    serverId: ServerId;
    children?: ReactNode;
}): import("react").FunctionComponentElement<import("react").ProviderProps<string | null>>;
/** The provider's server id (or the given one). */
export declare function useAdminServerId(explicit?: ServerId): ServerId;
/** True inside an <AdminProvider>. */
export declare const useInAdminProvider: () => boolean;
/** The shared session's state for a server: connection, transport, the latest `state` push. */
export declare function useAdminState(serverId?: ServerId): AdminHubState;
/** True when the server's Kiln.Admin serves `op` (ready.ops / status.ops). `useHasOp(op)` or `useHasOp(serverId, op)`. */
export declare function useHasOp(serverIdOrOp: ServerId | KnownOp | string, op?: KnownOp | string): boolean;
/** A function that runs an op on the server's shared session (rejects with KilnError, op_failed for ok=false). */
export declare function useAdminCaller(serverId?: ServerId): AdminCall;
export interface AdminMutationOptions<T, A> extends Omit<UseMutationOptions<T, KilnError, A>, "mutationFn"> {
    /** queries of these ops are refetched after a success */
    invalidates?: (KnownOp | string)[];
    serverId?: ServerId;
}
/** An op as a TanStack mutation: `const give = useAdminCall("give_item"); give.mutate({entity, item, count})`. */
export declare function useAdminCall<T = Json, A extends Record<string, Json> = Record<string, Json>>(op: KnownOp | string, options?: AdminMutationOptions<T, A>): import("@tanstack/react-query").UseMutationResult<T, KilnError, A, unknown>;
export interface AdminQueryOptions<T> extends Omit<UseQueryOptions<T, KilnError>, "queryKey" | "queryFn"> {
    serverId?: ServerId;
}
/** An op as a TanStack query, keyed per session (a reconnect refetches). Disabled until the op is available. */
export declare function useAdminQuery<T = Json>(op: KnownOp | string, args?: Record<string, Json>, options?: AdminQueryOptions<T>): import("@tanstack/react-query").UseQueryResult<T, KilnError>;
/** Refetches the queries of these ops on a server. */
export declare function invalidateAdmin(qc: QueryClient, serverId: ServerId, ops: (KnownOp | string)[]): Promise<void>;
/** Typed calls of Kiln.Admin's newer ops (0.2.0-dev.30.9; admin-protocol.md sections 8 and 11), each a KAP request on
 * the server's session through the hub's transport. They are ops, never launcher actions: `serverRestart` is Kiln.Admin
 * restarting the server it runs in (KAP `server_restart`), not the launcher's restart of a server it manages
 * (AdminHost.restartManagedServer). Check an op with useHasOp before offering it. */
export declare function kapOps(call: AdminCall): {
    adminInfo: () => Promise<AdminInfo>;
    serverRestart: (args?: ServerRestartArgs) => Promise<RestartRequest>;
    serverRestartCancel: (args?: {
        by?: string;
    }) => Promise<{
        canceled: true;
    }>;
    loginMint: (args: {
        consumer: string;
        label?: string;
    }) => Promise<LoginMintResult>;
    loginRedeem: (args: {
        consumer: string;
        secret: string;
    }) => Promise<LoginRedeemResult>;
    updateList: () => Promise<UpdateList>;
    updateStage: (args: UpdateStageArgs) => Promise<UpdateStageResult>;
    updateCancel: (args: {
        unit: string;
    }) => Promise<UpdateCancelResult>;
    updateRollback: (args: {
        unit: string;
    }) => Promise<UpdateRollbackResult>;
};
export type KapOps = ReturnType<typeof kapOps>;
/** kapOps on the server's shared session (the provider's, or the named server's). */
export declare function useKap(serverId?: ServerId): KapOps;
export {};
