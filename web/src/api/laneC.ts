// The metrics, webhooks, public page and extension module routes (docs/step2-lane-c.md). Separate from endpoints.ts,
// on the same request helper.
import { seg, type Api } from "./http";

export interface MetricField { key: string; title: string; unit: string | null; available: boolean; reason: string | null }
export interface MetricsLive { connected: boolean; at: number | null; values: Record<string, number | null> }
export interface MetricsSeries { range: string; stepS: number; t: number[]; series: Record<string, (number | null)[]>; fields: MetricField[] }
export interface AlertRule { id: string; metric: string; op: ">" | "<"; threshold: number; forS: number; cooldownS: number; enabled: boolean }
export interface AlertsDoc { maxAlertsPerHour: number; alerts: AlertRule[]; history: boolean }

export interface Webhook { id: string; name: string; kind: "discord" | "json"; url: string; enabled: boolean; events: string[]; template: string | null }
export interface WebhookInput { name?: string; kind?: "discord" | "json"; url?: string; enabled?: boolean; events?: string[]; template?: string | null }
export interface EventKind { topic: string; title: string; fields: string[] }
/** The owner's private-target opt-in (a security trade-off: off by default; the warning is shown while it is on). */
export interface WebhookSettings { allowPrivateTargets: boolean; canChange: boolean; warning: string | null }
export interface Delivery { at: string; hook: string; name: string; event: string; ok: boolean; status: number | null; error: string | null; ms: number }

export interface PublicSettings {
  format?: number;
  enabled: boolean; showStatus: boolean; showPlayerCount: boolean; showPlayerNames: boolean; showMapDots: boolean; showMods: boolean;
  /** metres, 25..1000 (a dot is the middle of the player's grid square) */
  mapDotsGrid: number;
  rules: string; joinInfo: string; widgets: string[];
}
export interface PublicSettingsDoc { settings: PublicSettings; widgets: { key: string; title: string }[]; mapDots: { available: boolean; worldMetres?: number; reason?: string } }

export type Block =
  | { type: "stat"; title: string; path: string; field: string; unit?: string | null }
  | { type: "table"; title?: string | null; path: string; rows?: string | null; columns: { field: string; title: string }[] }
  | { type: "form"; title: string; path: string; submit: string; schema?: JsonSchema | null; confirm?: string | null }
  | { type: "button"; label: string; path: string; args?: Record<string, unknown> | null; confirm?: string | null }
  | { type: "text"; text: string };
export interface Layout { module: string; id: string; title: string; blocks: Block[] }
export interface JsonSchema {
  type?: string; title?: string; description?: string; properties?: Record<string, JsonSchema>; required?: string[]; enum?: unknown[];
  minimum?: number; maximum?: number; minLength?: number; maxLength?: number; default?: unknown; items?: JsonSchema;
}
export interface ModuleDescription {
  module: string; actions: { name: string; title: string | null; confirm: string | null; schema: JsonSchema | null }[]; queries: string[]; topics: string[]; settings: boolean;
}
export interface ExtEvent { seq: number; topic: string; at: string; payload: unknown }

/** A path the server handed us must stay an API path of the same module (never another origin or another module). */
export function moduleApiPath(module: string, path: string): string {
  const prefix = `/api/${module}/`;
  if (!path.startsWith(prefix) || path.includes("..") || path.includes("//") || /[?#\\]/.test(path.slice(prefix.length))) throw new Error(`not a path of module ${module}: ${path}`);
  return path;
}

export function laneC(api: Api) {
  return {
    metricsFields: () => api.get<{ fields: MetricField[] }>("/api/metrics/fields"),
    metricsLive: () => api.get<MetricsLive>("/api/metrics/live"),
    metricsHistory: (range: string) => api.get<MetricsSeries>(`/api/metrics/history?range=${seg(range)}`),
    alerts: () => api.get<AlertsDoc>("/api/metrics/alerts"),
    saveAlerts: (doc: Partial<AlertsDoc>) => api.put<{ ok: true }>("/api/metrics/alerts", doc),

    webhooks: () => api.get<{ hooks: Webhook[] }>("/api/webhooks/hooks"),
    webhookEvents: () => api.get<{ events: EventKind[] }>("/api/webhooks/events"),
    deliveries: () => api.get<{ deliveries: Delivery[] }>("/api/webhooks/deliveries"),
    webhookSettings: () => api.get<WebhookSettings>("/api/webhooks/settings"),
    setPrivateTargets: (on: boolean) => api.put<{ ok: true; allowPrivateTargets: boolean; warning: string | null }>("/api/webhooks/settings", { allowPrivateTargets: on }),
    addWebhook: (h: WebhookInput) => api.post<{ ok: true; hook: Webhook }>("/api/webhooks/hooks", h),
    updateWebhook: (id: string, h: WebhookInput) => api.put<{ ok: true; hook: Webhook }>(`/api/webhooks/hooks/${seg(id)}`, h),
    deleteWebhook: (id: string) => api.del<{ ok: true }>(`/api/webhooks/hooks/${seg(id)}`),
    testWebhook: (id: string) => api.post<{ ok: true; queued: true }>(`/api/webhooks/hooks/${seg(id)}/test`),

    publicSettings: () => api.get<PublicSettingsDoc>("/api/public/settings"),
    savePublicSettings: (s: PublicSettings) => api.put<{ ok: true }>("/api/public/settings", s),
    publicPreview: () => api.get<Record<string, unknown>>("/api/public/preview"),

    layout: (module: string, page: string) => api.get<Layout>(`/api/${seg(module)}/pages/${seg(page)}`),
    moduleGet: <T>(module: string, path: string) => api.get<T>(moduleApiPath(module, path)),
    query: <T>(module: string, name: string, params: Record<string, string> = {}) => {
      const qs = new URLSearchParams(params).toString();
      return api.get<T>(`/api/${seg(module)}/data/${seg(name)}${qs ? `?${qs}` : ""}`);
    },
    modulePost: <T>(module: string, path: string, args: unknown) => api.post<T>(moduleApiPath(module, path), args ?? {}),
    describe: (module: string) => api.get<ModuleDescription>(`/api/${seg(module)}/describe`),
    events: (module: string, after: number) => api.get<{ seq: number; events: ExtEvent[] }>(`/api/${seg(module)}/events?after=${after}`),
    extSettings: (module: string) => api.get<{ schema: JsonSchema; value: Record<string, unknown> | null }>(`/api/${seg(module)}/settings`),
    saveExtSettings: (module: string, value: Record<string, unknown>) => api.put<{ ok: true }>(`/api/${seg(module)}/settings`, value),
  };
}

export type LaneC = ReturnType<typeof laneC>;

/** Event groups by topic prefix ("player.join" -> Players). */
const GROUPS: Record<string, string> = {
  player: "Players", server: "Server", schedule: "Schedules", mod: "Mods", crash: "Crashes", save: "World saves", metrics: "Metrics",
  admin: "Panel", webhook: "Tests",
};

export function groupEvents(events: EventKind[]): { group: string; events: EventKind[] }[] {
  const out = new Map<string, EventKind[]>();
  for (const e of events) {
    const prefix = e.topic.split(".")[0];
    const g = GROUPS[prefix] ?? prefix.charAt(0).toUpperCase() + prefix.slice(1);
    out.set(g, [...(out.get(g) ?? []), e]);
  }
  const order = Object.values(GROUPS);
  return [...out.entries()].sort(([a], [b]) => (order.indexOf(a) + 1 || 99) - (order.indexOf(b) + 1 || 99) || a.localeCompare(b)).map(([group, events]) => ({ group, events }));
}
