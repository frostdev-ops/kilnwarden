export type Json = null | boolean | number | string | Json[] | {
    [key: string]: Json;
};
export type ServerId = string;
export type ErrorCode = "not_implemented" | "invalid_argument" | "not_found" | "already_exists" | "io" | "parse" | "schema" | "conflict" | "game_running" | "steam_updating" | "permission" | "unsupported" | "network" | "hash_mismatch" | "consent_required" | "refused" | "timeout" | "canceled" | "busy" | "auth_failed" | "admin_unavailable" | "op_failed" | "internal"
/** The data folder cannot be used: its pointer names a folder that is missing, unreadable or not a Kiln data folder. */
 | "data_folder";
/** What every command rejects with. */
export interface KilnError {
    code: ErrorCode;
    message: string;
    detail?: unknown;
}
export declare function isKilnError(e: unknown): e is KilnError;
/** Any rejection as a KilnError (Tauri passes the serialized error through; anything else becomes "internal"). */
export declare function toKilnError(e: unknown): KilnError;
export interface SnapshotEntry {
    path: string;
    type: string;
    comment: string | null;
    default: Json;
    requiresRestart: boolean;
    synced: boolean;
    min?: number;
    max?: number;
    /** [Range(min, max, step)]: the slider moves from min in steps of this size (editor metadata only). */
    step?: number;
    options?: string[];
}
export interface SchemaSnapshot {
    format: number;
    source: "kiln" | "emf";
    file: string;
    valueFormat: "jsonc" | "ini";
    id: string;
    mod: string;
    modVersion: string | null;
    kind: string | null;
    written: string | null;
    kiln: string | null;
    sections: {
        path: string;
        comment: string | null;
        synced: boolean;
    }[];
    entries: SnapshotEntry[];
}
export interface ValidationError {
    path: string;
    message: string;
}
/** A placed mod folder Kiln left where it is (engine_mods.rs RetainedPlacement): `mayRun`, its own loader may still run
 * it whatever the profile says. */
export interface RetainedPlacement {
    path: string;
    reason: string;
    mayRun: boolean;
    /** The profile has the mod off, or no longer has it (absent: on, or an older backend). */
    off?: boolean;
}
export interface PlacementCleanup {
    removed: string[];
    retained: RetainedPlacement[];
    /** Files the mods wrote into removed copies, now in the game's kiln/kept-mod-files: one sentence per mod. Absent: none. */
    keptFiles?: string[];
}
