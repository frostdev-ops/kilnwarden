// The panel's own routes the Panel settings, Player activity and Overview pages use: the core's settings and settings
// files, the certificate (ACME), hosted access (relay), sign-in providers, branding images, module switches, map art,
// the per-account dashboard layout, and player activity. Typed, through the app's one request helper.
import { seg, type Api } from "./http";
import { duration } from "../ui/format";

// ---------------------------------------------------------------- player activity
export interface OnlinePlayer { key: string; name: string; steamId: string | null; since: string; seconds: number; you: boolean }
export interface ActivityOnline { connected: boolean; live: boolean; players: OnlinePlayer[] }
export type ActivityType = "join" | "leave" | "death";
export interface ActivityEvent { type: ActivityType; at: string; name: string; steamId: string | null; key: string; seconds: number | null; detail: string | null }
export interface ActivityPlayer {
  key: string; name: string; steamId: string | null; online: boolean; you: boolean; sessions: number; seconds7: number; seconds30: number; deaths: number;
  firstSeen: string; lastSeen: string;
}
export interface ActivityQuery { limit?: number; before?: string; type?: ActivityType | ""; player?: string }

// ---------------------------------------------------------------- server settings (settings.json)
/** TLS modes by name. GET /api/core/settings sends the enum as its number (0..3) today; both are read. */
export const TLS_MODES = ["self-signed", "file", "off", "acme"] as const;
export type TlsMode = (typeof TLS_MODES)[number];
export const TLS_ENGINES = ["auto", "kwtls", "ssl-stream"] as const;
export type TlsEngine = (typeof TLS_ENGINES)[number];

export interface ServerSettings {
  format: number;
  bind: string;
  port: number;
  tls: { mode: TlsMode | number; engine: TlsEngine | number; certFile: string; keyFile: string; hostnames: string[] };
  reverseProxy: { enabled: boolean; trustedProxies: string[] };
  allowedHosts: string[];
  ipAllowlist: string[];
  limits: Record<string, number>;
  hsts: boolean;
}

/** The enum as a name, whether the server sent a number or a name. */
export function enumName<T extends string>(names: readonly T[], v: T | number | string | null | undefined, fallback: T): T {
  if (typeof v === "number") return names[v] ?? fallback;
  if (typeof v === "string") {
    const k = v.replace(/[^a-z]/gi, "").toLowerCase();
    return names.find((n) => n.replace(/-/g, "") === k) ?? fallback;
  }
  return fallback;
}

/** The enum in the same form the server sent it (a number stays a number), so a save round-trips. */
export function enumValue<T extends string>(names: readonly T[], name: T, like: unknown): T | number {
  return typeof like === "number" ? Math.max(0, names.indexOf(name)) : name;
}

// ---------------------------------------------------------------- settings files
export interface AuthFile {
  format: number; sessionIdleMinutes: number; sessionAbsoluteHours: number; maxSessionsPerAccount: number; passwordMinLength: number;
  pbkdf2Iterations: number; maxConcurrentHashes: number; launcherLogin: boolean; maxSocketsPerAccount: number;
  signIn: { attemptsPerMinutePerIp: number; attemptsPerMinutePerAccount: number; accountFreeFailures: number; ipFreeFailures: number; lockoutBaseSeconds: number; lockoutMaxSeconds: number };
}
/** A provider's group (a Discord role id, a Kiln permission group) that gives a panel role at each sign-in. */
export interface RoleMapping { from: string; role: string }
export interface IdentityFile {
  format: number;
  steam: { enabled: boolean; roleMap?: RoleMapping[] };
  discord: { enabled: boolean; broker: string; guildId: string; allowedRoles: string[]; roleMap?: RoleMapping[] };
}
export type AcmeChallenge = "http-01" | "tls-alpn-01" | "dns-01";
export interface AcmeFile {
  format: number; directory: string; email: string; acceptTerms: boolean; names: string[]; challenge: AcmeChallenge; dns01PropagationSeconds: number;
  http01Bind: string; http01Port: number; renewDaysBefore: number; minRetryMinutes: number; maxRetryHours: number; maxOrdersPerWeek: number;
}
export interface RelayFile { enabled: boolean; origin: string; host: string }
export interface BrandingFile { format: number; name: string | null; accent: string | null; logo: string | null; favicon: string | null }
export interface SettingsFiles { auth: AuthFile; identity: IdentityFile; acme: AcmeFile; relay: RelayFile; branding: BrandingFile }
export type SettingsFileId = keyof SettingsFiles;
export interface SettingsFile<K extends SettingsFileId = SettingsFileId> { id: K; title: string; file: string; value: SettingsFiles[K]; problems: string[] }

// ---------------------------------------------------------------- certificate, relay, providers, map art, modules
export interface AcmeStatus {
  active: boolean; phase: string; blocked: string | null; directory?: string; challenge?: string; acceptTerms?: boolean; names?: string[];
  certificate: { sha256: string; names: string[]; notAfter: string; keyType: string; chainLength: number } | null;
  lastAttempt: string | null; lastSuccess: string | null; nextAttempt: string | null; lastError: string | null; failures: number;
  ordersThisWeek: number; maxOrdersPerWeek: number;
}
export interface RelayStatus {
  enabled: boolean; origin: string; host: string; state: "off" | "waiting" | "connected" | "error"; lastError: string | null; since: string | null;
  waitingFor: string[]; publicKey: string | null;
  /** SHA-256 hex of the public key (what the relay and its claim pages show); the claim pages' "Your panel name"
   * (https://claim.<the name's zone>/, 0.1.1). */
  fingerprint?: string | null; namesPage?: string | null;
  /** 0.1.1: the move or release the relay has scheduled for this panel's name (it waits 24 hours, and the owner can undo
   * it here): the new key's fingerprint (null: a release), when it was asked for, when it happens. */
  scheduled?: RelayScheduled | null;
  /** 0.1.1: this server's key takes the name over then (it is moving here from another server). */
  movingHere?: string | null;
}
export interface RelayScheduled { to: string | null; requested: string; at: string }
/** A claim link for the relay's Discord claim pages (POST /api/core/relay/claim): once, for 15 minutes. */
export interface RelayClaimLink { url: string; host: string; fingerprint: string; expires: string; warnings: string[] }

/** A key fingerprint as people compare it: the first 32 hex digits in groups of four (the claim pages show the same). */
export function shortFingerprint(fp: string | null | undefined): string {
  return (fp ?? "").slice(0, 32).match(/.{1,4}/g)?.join(" ") ?? "";
}

/** Owner decision 2026-11-25: said word for word wherever hosted access is described (Panel settings > Hosted access,
 * the README, the claim pages and kiln.nexus itself). */
export const KILN_NEXUS_NOTE = "kiln.nexus is frostdev's own domain for Kiln. It has no affiliation with Nexus Mods (nexusmods.com).";

/** Only links to the claim pages' own https origin (claim.<zone>, 0.1.1) become a link to click. */
export function isClaimUrl(url: string): boolean {
  return /^https:\/\/claim\.[a-z0-9-]+(\.[a-z0-9-]+)+(:\d+)?\/claim\?v=1&/.test(url);
}

/** The claim pages' "Your panel name" (https://claim.<zone>/), only that becomes a link. */
export function isClaimPages(url: string | null | undefined): url is string {
  return !!url && /^https:\/\/claim\.[a-z0-9-]+(\.[a-z0-9-]+)+(:\d+)?\/$/.test(url);
}

/** The banner's sentence for a scheduled move or release of this panel's name (lane L1b). */
export function scheduledText(host: string, s: RelayScheduled, at: string): string {
  return s.to === null
    ? `This panel's name ${host} is being released on ${at}: after that it no longer reaches this server, and anyone may claim it.`
    : `This panel's name ${host} is being moved to another server on ${at}.`;
}
export interface IdentityStatus {
  panelId: string;
  steam: { enabled: boolean; active: boolean };
  discord: { enabled: boolean; active: boolean; broker: string; keyPinned: boolean; guildId: string; allowedRoles: number | string[] };
}
export interface MapAssetsStatus { present: boolean; folder: string }
/** A module with its switch (GET /api/core/modules/switches: every module, on or off). */
export interface ModuleSwitch { id: string; title: string; enabled: boolean; canDisable: boolean | null; description?: string | null }

// ---------------------------------------------------------------- dashboard
export interface DashboardLayout { order: string[]; hidden: string[] }
export const LAYOUT_KEY = /^[a-z0-9][a-z0-9_-]{0,63}\/[a-z0-9][a-z0-9_-]{0,63}$/;

export function panelEndpoints(api: Api) {
  return {
    activityOnline: () => api.get<ActivityOnline>("/api/activity/online"),
    activityEvents: (q: ActivityQuery = {}) => {
      const p = new URLSearchParams();
      if (q.limit) p.set("limit", String(q.limit));
      if (q.before) p.set("before", q.before);
      if (q.type) p.set("type", q.type);
      if (q.player) p.set("player", q.player);
      const qs = p.toString();
      return api.get<{ events: ActivityEvent[]; more: boolean }>(`/api/activity/events${qs ? `?${qs}` : ""}`);
    },
    activityPlayers: () => api.get<{ since: string | null; players: ActivityPlayer[] }>("/api/activity/players"),
    activityRecent: () => api.get<ActivityOnline & { recent: ActivityEvent[] }>("/api/activity/recent"),

    settings: () => api.get<{ settings: ServerSettings; problems: string[] }>("/api/core/settings"),
    saveSettings: (s: ServerSettings) => api.put<{ ok: true }>("/api/core/settings", s),
    settingsFiles: () => api.get<{ files: SettingsFile[] }>("/api/core/settings/files"),
    saveSettingsFile: <K extends SettingsFileId>(id: K, value: SettingsFiles[K]) => api.put<{ ok: true }>(`/api/core/settings/files/${seg(id)}`, value),

    acmeStatus: () => api.get<AcmeStatus>("/api/acme/status"),
    acmeRenew: () => api.post<{ ok: true; started: true }>("/api/acme/renew"),
    relay: () => api.get<RelayStatus>("/api/core/relay"),
    relayClaim: () => api.post<RelayClaimLink>("/api/core/relay/claim"),
    relayUndo: () => api.post<{ undone: true; host: string }>("/api/core/relay/move/undo"),
    identityStatus: () => api.get<IdentityStatus>("/api/identity/status"),
    mapAssets: () => api.get<MapAssetsStatus>("/api/core/map-assets-status"),

    uploadBrandImage: (kind: "logo" | "favicon", file: Blob, type: string) => api.raw<{ ok: true; file: string }>("POST", `/api/core/branding/${kind}`, file, type),
    removeBrandImage: (kind: "logo" | "favicon") => api.del<void>(`/api/core/branding/${kind}`),
    setModule: (id: string, enabled: boolean) => api.put<{ ok: true }>(`/api/core/modules/${seg(id)}`, { enabled }),
    moduleSwitches: () => api.get<{ modules: ModuleSwitch[] }>("/api/core/modules/switches"),

    dashboard: () => api.get<DashboardLayout>("/api/core/dashboard"),
    saveDashboard: (l: DashboardLayout) => api.put<{ ok: true }>("/api/core/dashboard", { order: l.order.filter((k) => LAYOUT_KEY.test(k)), hidden: l.hidden.filter((k) => LAYOUT_KEY.test(k)) }),
  };
}

export type PanelEndpoints = ReturnType<typeof panelEndpoints>;

/** Modules that refuse to be switched off (the server answers 409); used until GET /api/core/modules says canDisable. */
export const ALWAYS_ON = new Set(["core", "auth", "users", "acme", "identity", "extensions"]);

/** The image types the branding routes take (sniffed again on the server; SVG never). */
export function imageType(file: { name: string; type: string }): string | null {
  const t = file.type.toLowerCase();
  if (t === "image/png" || t === "image/webp" || t === "image/x-icon") return t;
  if (t === "image/vnd.microsoft.icon") return "image/x-icon";
  const ext = file.name.toLowerCase().split(".").pop();
  return ext === "png" ? "image/png" : ext === "webp" ? "image/webp" : ext === "ico" ? "image/x-icon" : null;
}

/** Whether an address is this machine only (127.0.0.0/8, ::1). */
export function isLoopback(bind: string): boolean {
  const b = bind.trim().toLowerCase();
  return b === "::1" || b === "localhost" || /^127\.\d{1,3}\.\d{1,3}\.\d{1,3}$/.test(b);
}

/** Moves `key` one step in `order` (dir -1 up, +1 down). */
export function move(order: string[], key: string, dir: -1 | 1): string[] {
  const i = order.indexOf(key);
  const j = i + dir;
  if (i < 0 || j < 0 || j >= order.length) return order;
  const next = [...order];
  [next[i], next[j]] = [next[j], next[i]];
  return next;
}

/** The dashboard's cards in the saved order: saved keys first (those still there), then the rest in their default order. */
export function arrange(available: string[], saved: DashboardLayout | undefined): { visible: string[]; hidden: string[] } {
  const hidden = new Set(saved?.hidden ?? []);
  const seen = new Set<string>();
  const order: string[] = [];
  for (const k of [...(saved?.order ?? []), ...available]) {
    if (!available.includes(k) || seen.has(k)) continue;
    seen.add(k);
    order.push(k);
  }
  return { visible: order.filter((k) => !hidden.has(k)), hidden: order.filter((k) => hidden.has(k)) };
}

/** "Brannoc joined", "Brannoc left after 1 h 5 min", "Brannoc died (killed by a wolf)". */
export function eventText(e: Pick<ActivityEvent, "type" | "name" | "seconds" | "detail">): string {
  switch (e.type) {
    case "join": return `${e.name} joined`;
    case "leave": {
      const after = e.seconds !== null && e.seconds !== undefined ? ` after ${duration(e.seconds)}` : "";
      return `${e.name} left${after}${e.detail ? ` (${e.detail})` : ""}`;
    }
    case "death": return `${e.name} died${e.detail ? ` (killed by ${e.detail})` : ""}`;
  }
}
