// The operations routes (docs/step2-lane-b-ops.md): schedules, saved commands, mod updates. Typed, through the same
// request helper as every other route (same-origin JSON, ApiError, the reauth signal). Package uploads go in raw chunks
// below the panel's request-body cap; the SHA-256 is computed in the browser so the server can check it.
import { seg, type Api } from "./http";

export type ScheduleKind = "restart" | "backup" | "announce" | "command";
export type Weekday = "sun" | "mon" | "tue" | "wed" | "thu" | "fri" | "sat";
export const WEEKDAYS: Weekday[] = ["mon", "tue", "wed", "thu", "fri", "sat", "sun"];

export type ScheduleWhen =
  | { type: "daily"; times: string[]; days?: Weekday[] | null }
  | { type: "interval"; everyMinutes: number; anchor?: string | null }
  | { type: "once"; at: string };

export interface ScheduleRun {
  at: string;
  schedule: string;
  slot: string | null;
  kind: ScheduleKind;
  outcome: "ok" | "failed" | "refused" | "skipped" | "missed";
  code: string | null;
  message: string | null;
  by: string | null;
}

export interface ScheduleInput {
  name: string;
  enabled: boolean;
  kind: ScheduleKind;
  when: ScheduleWhen;
  timeZone: string;
  skipWhenEmpty: boolean;
  missed: "skip" | "catchUp";
  catchUpMinutes: number;
  params: Record<string, unknown>;
}

export interface Schedule extends ScheduleInput {
  id: string;
  actor: { id: string; name: string };
  created: string;
  updated: string;
  updatedBy: string | null;
  lastSlot: string | null;
  skipSlot: string | null;
  lastRun: ScheduleRun | null;
  nextRun: string | null;
  nextFire: string | null;
  savedCommand?: { id: string; name: string; command: string; changed: boolean } | null;
}

export interface RestartSupport {
  supported: "supported" | "unknown" | string;
  via: string | null;
  code: number | null;
}

export interface ScheduleList {
  schedules: Schedule[];
  restart: RestartSupport | null;
  now: string;
  timeZones: string[];
  localZone: string;
}

export interface SavedCommandInput {
  name: string;
  description?: string | null;
  line: string;
  form?: unknown;
  confirm?: boolean;
  shared?: boolean;
}

export interface SavedCommand extends Required<Omit<SavedCommandInput, "form">> {
  id: string;
  command: string;
  form: unknown;
  owner: { id: string; name: string };
  revision: number;
  created: string;
  updated: string;
  mine: boolean;
  pinned: boolean;
  runnable: boolean;
}

export interface PackagePlan {
  id: string;
  kind: "mod" | "kiln";
  modId: string | null;
  name: string | null;
  version: string;
  current: string | null;
  folder: string | null;
  files: number;
  bytes: number;
  sha256: string;
  source: { type: "upload"; name: string } | { type: "url"; url: string };
  warnings: string[];
  /** minisign: verified (a trusted key, `key` its name), unsigned, unknown_key (`keyId`), invalid (never a plan: 422 bad_signature) */
  signature?: { state: "verified" | "unsigned" | "unknown_key" | "invalid"; key: string | null; keyId: string | null; trustedComment: string | null };
  created: string;
  expiresAt: string;
}

/** update_list's unit (admin-protocol.md section 8). */
export interface UpdateUnit {
  unit: string;
  kind: "kiln" | "mod";
  folder: string | null;
  id: string | null;
  current: { version: string } | null;
  staged: { version: string; from: string | null; staged: string; by: string; label: string | null; files: number; bytes: number } | null;
  previous: { version: string; replacedBy: string; kept: string; reason: string } | null;
  rollback: { requested: string; by: string } | null;
  probation: { state: string; version: string; error: string | null } | null;
  open: boolean;
}

export type Part<T> = T | { error: { code: string; message: string } };
export const isPartError = <T,>(p: Part<T> | undefined | null): p is { error: { code: string; message: string } } =>
  !!p && typeof p === "object" && "error" in (p as object) && !Array.isArray(p);

export interface ModInfo {
  id: string;
  name?: string;
  version?: string;
  state?: string;
  folder?: string;
  [k: string]: unknown;
}

/** A folder under kiln/mods and its persistent switch (a .disabled file Kiln skips at the next start). */
export interface ModFolder { folder: string; id: string | null; name: string | null; version: string | null; disabled: boolean; locked: string | null }

export interface UpdatesStatus {
  mods: Part<ModInfo[] | { mods: ModInfo[] }>;
  folders?: ModFolder[];
  updates: Part<{ units: UpdateUnit[]; results: unknown[]; incoming: string }>;
  plans: PackagePlan[];
  restart: RestartSupport | null;
  limits: { maxUploadBytes: number; maxDownloadBytes: number; chunkBytes: number };
  allowedOrigins: string[];
  owner: boolean;
}

export interface TrustedKey { name: string; publicKey: string }

export interface UpdateSources {
  allowedOrigins: string[];
  maxUploadBytes: number;
  maxDownloadBytes: number;
  maxUnpackedBytes: number;
  maxEntries: number;
  /** minisign public keys (the "RW..." line of a .pub file) the owner trusts */
  trustedKeys?: TrustedKey[];
  /** stage only packages a trusted key signed */
  requireSignature?: boolean;
}

export interface SwitchAnswer { folder: string; id: string | null; enabled: boolean; changed: boolean; now: { ok: boolean; status?: number } | null }

export type ChunkPut = (path: string, bytes: Uint8Array) => Promise<{ received: number }>;

/** PUT raw bytes (an upload chunk), with the same error shape as the JSON helper. */
export function chunkPut(fetchImpl: (i: string, init: RequestInit) => Promise<Response> = (i, init) => fetch(i, init)): ChunkPut {
  return async (path, bytes) => {
    const res = await fetchImpl(path, {
      method: "PUT", credentials: "same-origin", cache: "no-store",
      headers: { "Content-Type": "application/octet-stream", Accept: "application/json" }, body: bytes as unknown as BodyInit,
    });
    const json = await res.json().catch(() => null) as { received?: number; error?: { message?: string } } | null;
    if (!res.ok) throw new Error(json?.error?.message ?? `upload failed (${res.status})`);
    return { received: json?.received ?? 0 };
  };
}

/** Lower-case hex SHA-256 of the bytes (WebCrypto). */
export async function sha256Hex(bytes: ArrayBuffer | Uint8Array): Promise<string> {
  const d = await crypto.subtle.digest("SHA-256", bytes as BufferSource);
  return [...new Uint8Array(d)].map((b) => b.toString(16).padStart(2, "0")).join("");
}

export function opsEndpoints(api: Api, put: ChunkPut = chunkPut()) {
  return {
    // schedules
    schedules: () => api.get<ScheduleList>("/api/schedules"),
    scheduleRuns: (id: string) => api.get<ScheduleRun[]>(`/api/schedules/${seg(id)}/runs`),
    createSchedule: (s: ScheduleInput) => api.post<Schedule>("/api/schedules", s),
    updateSchedule: (id: string, s: ScheduleInput) => api.put<Schedule>(`/api/schedules/${seg(id)}`, s),
    deleteSchedule: (id: string) => api.del<void>(`/api/schedules/${seg(id)}`),
    enableSchedule: (id: string, enabled: boolean) => api.post<Schedule>(`/api/schedules/${seg(id)}/enable`, { enabled }),
    skipNext: (id: string, skip: boolean) => api.post<Schedule>(`/api/schedules/${seg(id)}/skip-next`, { skip }),
    runSchedule: (id: string) => api.post<{ run: ScheduleRun }>(`/api/schedules/${seg(id)}/run`),
    cancelRestart: () => api.post<{ ok: true }>("/api/schedules/restart/cancel"),
    // saved commands
    savedCommands: () => api.get<SavedCommand[]>("/api/saved-commands"),
    pinnedCommands: () => api.get<SavedCommand[]>("/api/saved-commands/pinned"),
    saveCommand: (c: SavedCommandInput) => api.post<SavedCommand>("/api/saved-commands", c),
    updateCommand: (id: string, c: SavedCommandInput) => api.put<SavedCommand>(`/api/saved-commands/${seg(id)}`, c),
    deleteCommand: (id: string) => api.del<void>(`/api/saved-commands/${seg(id)}`),
    pinCommand: (id: string, pinned: boolean) => api.put<{ pinned: boolean }>(`/api/saved-commands/${seg(id)}/pin`, { pinned }),
    runCommand: (id: string, confirmed = false) =>
      api.post<{ ok: true; result: unknown }>(`/api/saved-commands/${seg(id)}/run`, confirmed ? { confirmed: true } : {}),
    // mod updates
    updates: () => api.get<UpdatesStatus>("/api/updates"),
    plan: (id: string) => api.get<PackagePlan>(`/api/updates/plans/${seg(id)}`),
    removePlan: (id: string) => api.del<void>(`/api/updates/plans/${seg(id)}`),
    stage: (id: string, sha256: string, label?: string) => api.post<{ unit: string; note?: string }>(`/api/updates/plans/${seg(id)}/stage`, { sha256, label }),
    cancelUnit: (unit: string) => api.post<unknown>(`/api/updates/units/${seg(unit)}/cancel`),
    rollbackUnit: (unit: string) => api.post<unknown>(`/api/updates/units/${seg(unit)}/rollback`),
    modLifecycle: (verb: "load" | "unload" | "reload", value: string) =>
      api.post<unknown>(`/api/updates/mods/${verb}`, verb === "load" ? { folder: value } : { id: value }),
    fetchUrl: (url: string, sha256: string, expectId?: string, sig: { signature?: string; signatureUrl?: string } = {}) =>
      api.post<PackagePlan>("/api/updates/fetch", { url, sha256, expectId: expectId || undefined, signature: sig.signature || undefined, signatureUrl: sig.signatureUrl || undefined }),
    switchMod: (folder: string, enabled: boolean, now = false) => api.post<SwitchAnswer>("/api/updates/mods/switch", { folder, enabled, now }),
    sources: () => api.get<{ settings: UpdateSources; problems: string[] }>("/api/updates/sources"),
    saveSources: (s: UpdateSources) => api.put<{ ok: true }>("/api/updates/sources", s),
    /** Opens an upload, sends the bytes in chunks (progress 0..1), finishes it: the plan. */
    async upload(file: { name: string; bytes: Uint8Array }, opts: { sha256?: string; expectId?: string; signature?: string; onProgress?: (f: number) => void } = {}) {
      const sha256 = opts.sha256 ?? (await sha256Hex(file.bytes));
      const open = await api.post<{ id: string; chunkBytes: number }>("/api/updates/uploads", { name: file.name, size: file.bytes.length, sha256, signature: opts.signature || undefined });
      for (let off = 0; off < file.bytes.length; off += open.chunkBytes) {
        const end = Math.min(file.bytes.length, off + open.chunkBytes);
        await put(`/api/updates/uploads/${seg(open.id)}?offset=${off}`, file.bytes.subarray(off, end));
        opts.onProgress?.(end / file.bytes.length);
      }
      return api.post<PackagePlan>(`/api/updates/uploads/${seg(open.id)}/finish`, { expectId: opts.expectId || undefined });
    },
  };
}

export type OpsEndpoints = ReturnType<typeof opsEndpoints>;

/** "06:00, 18:30" -> ["06:00", "18:30"] (sorted, unique, only valid HH:mm). */
export function parseTimes(text: string): string[] {
  const out = new Set<string>();
  for (const t of text.split(/[\s,;]+/)) {
    const m = /^(\d{1,2}):(\d{2})$/.exec(t.trim());
    if (!m) continue;
    const h = Number(m[1]), min = Number(m[2]);
    if (h < 24 && min < 60) out.add(`${String(h).padStart(2, "0")}:${m[2]}`);
  }
  return [...out].sort();
}

/** A plain-language line for a schedule's rule. */
export function describeWhen(w: ScheduleWhen, zone: string): string {
  switch (w.type) {
    case "daily": {
      const days = w.days?.length ? w.days.join(", ") : "every day";
      return `${days} at ${w.times.join(", ")} (${zone})`;
    }
    case "interval":
      return w.everyMinutes % 60 === 0 ? `every ${w.everyMinutes / 60} h` : `every ${w.everyMinutes} min`;
    case "once":
      return `once, ${new Date(w.at).toLocaleString()}`;
  }
}

/** Bytes as KiB/MiB. */
export function size(n: number): string {
  if (n < 1024) return `${n} B`;
  if (n < 1 << 20) return `${(n / 1024).toFixed(1)} KiB`;
  return `${(n / (1 << 20)).toFixed(1)} MiB`;
}
