// The setup wizard's logic (0.1.2; 0.1.3 adds the owner's Discord sign-in, mods and the dashboard), apart from React so
// the unit tests can run it: the pages of each route, Back and Next, the panel name's rules (the same as the server's
// PanelNames and the claim pages'), the checklist and the Discord link's answers in plain words, and the end's list.
import type { Api } from "../api/http";

export type Route = "hosted" | "launcher" | "own";
export type StepId = "welcome" | "owner" | "route" | "name" | "claim" | "progress" | "own" | "own-progress" | "launcher" | "discord" | "helpers" | "mods"
  | "dashboard" | "done";
export type StepState = "done" | "working" | "waiting" | "failed";

export interface SetupProblem { title: string; fix: string; detail: string | null }
export interface SetupStep { id: string; state: StepState; note: string | null; problem: SetupProblem | null }
export interface SetupState {
  done: boolean;
  route: Route | "";
  step: string;
  suggested: Route;
  reason: "hosted" | "container" | "gameHost" | "ownAddress" | "publicAddress" | "default";
  client: { thisMachine: boolean; hosted: boolean };
  zone: string;
  address: string | null;
  launcher: string;
  hosted?: { host: string; name: string | null; url?: string; steps?: SetupStep[]; ready?: boolean; off?: boolean };
  own?: { host?: string; port: number; challenge?: string; url?: string; steps?: SetupStep[]; ready?: boolean };
  discord?: { enabled: boolean; guildId: string; available: boolean; ownerOnly?: boolean; kilnGuild?: boolean; linked?: boolean };
}
export interface ClaimLink { claimUrl: string; host: string; expires: string | null }

export function setupApi(api: Api) {
  return {
    state: () => api.get<SetupState>("/api/setup/state"),
    progress: (p: { route?: Route | ""; step?: StepId }) => api.put<{ ok: true }>("/api/setup/progress", p),
    hosted: (name: string) => api.post<ClaimLink>("/api/setup/hosted", { name }),
    claim: () => api.post<ClaimLink>("/api/setup/hosted/claim"),
    own: (host: string, port: number, challenge: "http-01" | "tls-alpn-01") => api.post<{ ok: true }>("/api/setup/own", { host, port, challenge }),
    check: () => api.post<{ ok: true }>("/api/setup/check"),
    discord: (guildId: string) => api.post<{ ok: true }>("/api/setup/discord", { guildId }),
    discordMe: (anyway = false) => api.post<{ ok: true; guildId: string; ownerOnly: boolean }>("/api/setup/discord/me", anyway ? { anyway: true } : {}),
    done: () => api.post<{ ok: true }>("/api/setup/done"),
    again: () => api.post<{ ok: true }>("/api/setup/again"),
  };
}

/** Which optional pages this panel has: the Discord pages need a build with the broker's keys. */
export interface PageOptions { discord?: boolean }

/** The pages every route ends with: your Discord sign-in, helpers' (both optional), mods, the dashboard, the end. */
function tail(o: PageOptions): StepId[] {
  return [...(o.discord === false ? [] : (["discord", "helpers"] as StepId[])), "mods", "dashboard", "done"];
}

/** The pages of a route, in order (the welcome and the account come before any route). */
export function pagesOf(route: Route | "", o: PageOptions = {}): StepId[] {
  const start: StepId[] = ["welcome", "owner", "route"];
  switch (route) {
    case "hosted": return [...start, "name", "claim", "progress", ...tail(o)];
    case "own": return [...start, "own", "own-progress", ...tail(o)];
    case "launcher": return [...start, "launcher", ...tail(o)];
    default: return [...start, "done"];
  }
}

/** "Step 3 of 7" for the progress dots: where this page is in its route. */
export function position(route: Route | "", step: StepId, o: PageOptions = {}): { at: number; of: number } {
  const pages = pagesOf(route === "" ? "hosted" : route, o);
  const i = pages.indexOf(step);
  return { at: i < 0 ? pages.length : i + 1, of: pages.length };
}

/** The page after `step` on its route (null at the end). The route's choice picks its own next page. */
export function nextPage(route: Route | "", step: StepId, o: PageOptions = {}): StepId | null {
  const pages = pagesOf(route, o);
  const i = pages.indexOf(step);
  return i >= 0 && i + 1 < pages.length ? pages[i + 1] : null;
}

/** The page Back goes to (null: none; the account exists, so nothing comes before the choice). */
export function prevPage(route: Route | "", step: StepId, o: PageOptions = {}): StepId | null {
  const pages = pagesOf(route, o);
  const i = pages.indexOf(step);
  return i > 2 ? pages[i - 1] : null;
}

/** The route the pages follow: the chosen one, or hosted access when the server console already set it up. */
export function routeOf(s: Pick<SetupState, "route" | "hosted">): Route | "" {
  return s.route || (s.hosted?.ready ? "hosted" : "");
}

/** The page to come back to: the saved one if it belongs to the saved route (a page this build has not got: the next
 * one it has), else the route's choice. */
export function resumeAt(s: Pick<SetupState, "route" | "step" | "done" | "hosted"> & { client?: SetupState["client"]; discord?: SetupState["discord"] }): StepId {
  if (s.done) return "done";
  const o: PageOptions = { discord: s.discord?.available !== false };
  const route = routeOf(s);
  const pages = pagesOf(route, o);
  const all = pagesOf(route);
  const step = s.step as StepId;
  if (pages.includes(step) && step !== "welcome" && step !== "owner" && (route === s.route || step !== "done")) return step;
  if (all.includes(step) && step !== "welcome" && step !== "owner") return all.slice(all.indexOf(step)).find((p) => pages.includes(p)) ?? "done";
  // set up from the server console: the name already works, only the optional pages and the end are left (or, opened
  // at the name itself before its last check, the checklist)
  if (s.hosted?.ready && (s.route === "" || s.route === "hosted")) return pages[pages.indexOf("progress") + 1];
  if (s.client?.hosted && s.hosted?.steps) return "progress";
  return "route";
}

/** The owner's Discord link (and the broker check before it) when it did not work, in one or two plain sentences. */
export function discordProblem(code: string, kilnGuild = true): string {
  switch (code) {
    case "ext_canceled": return "You canceled on Discord. Nothing changed.";
    case "linked_elsewhere": return "That Discord account is already linked to another account here. Unlink it there first, or use another Discord account.";
    case "not_member": return kilnGuild
      ? "Discord says you aren't in the Kiln Discord. Join it, then try again."
      : "Discord says you aren't in your Discord server. Check you used the right Discord account, then try again.";
    case "broker_unreachable": return "Discord sign-in isn't answering right now. Your password still works. Try again in a few minutes, or skip this.";
    case "ext_expired": case "expired": case "malformed": return "That took too long, or was started in another browser. Try again.";
    case "reauth_required": return "Confirm it's you in the box that opened, then press the button again.";
    case "owner_only": return "Only the owner can do this.";
    default: return "Discord didn't confirm it. Try again, or skip this.";
  }
}

/** One line of the end page: what it is, whether it was set up, and where to change it later. */
export interface DoneRow { id: "address" | "discord" | "helpers" | "mods" | "dashboard"; label: string; done: boolean; where: string; to: string }

/** The end page's list: what was set up and what was skipped (`extra`: mods waiting for the next start, and whether
 * the dashboard has a layout of its own). */
export function doneRows(s: SetupState, extra: { mods: number; customized: boolean }): DoneRow[] {
  const route = routeOf(s);
  const address = route === "launcher" || (route === "hosted" && !!s.hosted?.ready) || (route === "own" && !!s.own?.ready);
  const rows: DoneRow[] = [{ id: "address", label: "Address", done: address, where: "Panel settings", to: "/settings" }];
  if (s.discord?.available !== false && s.discord) {
    rows.push({ id: "discord", label: "Discord sign-in", done: !!s.discord.linked, where: "My account", to: "/account" });
    rows.push({ id: "helpers", label: "Helpers", done: s.discord.enabled && !s.discord.ownerOnly, where: "Panel settings", to: "/settings" });
  }
  rows.push({ id: "mods", label: "Mods", done: extra.mods > 0, where: "Mods", to: "/mods" });
  rows.push({ id: "dashboard", label: "Dashboard", done: extra.customized, where: "Overview", to: "/" });
  return rows;
}

export const MIN_NAME = 3;
export const MAX_NAME = 40;
// The relay's reserved names (the server's PanelNames.Reserved): names nobody may claim
export const RESERVED = new Set([
  "www", "api", "app", "admin", "administrator", "root", "relay", "panel", "panels", "mail", "smtp", "imap", "pop", "ftp", "ns", "ns1", "ns2",
  "dns", "kiln", "frostdev", "official", "staff", "team", "support", "help", "security", "abuse", "status", "docs", "test", "testing",
  "localhost", "local", "example", "invalid", "acme", "cdn", "static", "assets", "auth", "login", "signin", "account", "billing",
  "discord", "steam", "nexus", "enshrouded", "keen", "keengames", "moderator", "owner", "wiki", "dev", "beta", "blog", "forum", "news",
  "shop", "store", "download", "downloads", "launcher", "webadmin", "broker", "mods", "claim",
]);

/** What was typed, as a name: lower case, and the zone or "https://" taken off when the whole address was pasted. */
export function normalizeName(typed: string, zone = "kiln.nexus"): string {
  let s = typed.trim().toLowerCase();
  if (s.startsWith("https://")) s = s.slice(8);
  else if (s.startsWith("http://")) s = s.slice(7);
  s = s.replace(/\/+$/, "");
  if (s.endsWith("." + zone)) s = s.slice(0, -(zone.length + 1));
  return s;
}

/** Null when the name can be claimed, else what is wrong, in one plain sentence (the server says the same). */
export function nameProblem(name: string): string | null {
  if (name.length === 0) return "Type a name.";
  if (name.length < MIN_NAME) return `Use at least ${MIN_NAME} characters.`;
  if (name.length > MAX_NAME) return `Use at most ${MAX_NAME} characters.`;
  if (!/^[a-z0-9-]+$/.test(name)) return "Use only letters, digits and dashes.";
  if (name.startsWith("-") || name.endsWith("-")) return "Start and end with a letter or a digit.";
  if (name.length >= 4 && name[2] === "-" && name[3] === "-") return "Don't put two dashes in the third and fourth places.";
  if (RESERVED.has(name)) return `"${name}" is kept for Kiln itself. Pick another name.`;
  return null;
}

/** A first name to offer: the server's name from the branding, made into a valid label. */
export function suggestName(serverName: string): string {
  const s = serverName.toLowerCase().normalize("NFKD").replace(/[^a-z0-9]+/g, "-").replace(/^-+|-+$/g, "").slice(0, MAX_NAME).replace(/-+$/, "");
  return s === "kilnwarden" || nameProblem(s) ? "" : s;
}

/** The checklist's plain words: what each line says while it happens and once it is done. */
export const CHECK_LABELS: Record<string, { doing: string; done: string; waiting?: string; failed: string }> = {
  connected: { doing: "Connecting…", done: "Connected", failed: "Can't connect" },
  claimed: { doing: "Waiting for your claim…", done: "Claimed", waiting: "Claimed", failed: "Not claimed" },
  secure: { doing: "Securing…", done: "Secure", waiting: "Secure", failed: "Not secure yet" },
  ready: { doing: "Checking…", done: "Ready", waiting: "Ready", failed: "Not answering yet" },
  found: { doing: "Looking up your address…", done: "Address found", failed: "Address not found" },
  reachable: { doing: "Reaching your panel…", done: "Reachable", waiting: "Reachable", failed: "Can't reach it" },
};

export function checkLabel(s: SetupStep): string {
  const l = CHECK_LABELS[s.id] ?? { doing: s.id, done: s.id, failed: s.id };
  if (s.state === "done") return l.done;
  if (s.state === "failed") return l.failed;
  if (s.state === "working") return l.doing;
  if (s.state === "waiting" && s.id === "claimed" && s.note === "claim") return l.doing;
  if (s.state === "waiting" && s.note === "moving") return "Moving to this server…";
  return l.waiting ?? l.doing;
}

/** Every step done: the route works. */
export const allDone = (steps: SetupStep[] | undefined) => !!steps && steps.length > 0 && steps.every((s) => s.state === "done");

/** The first step that failed, if any (the screen shows its fix). */
export const firstProblem = (steps: SetupStep[] | undefined) => steps?.find((s) => s.state === "failed" && s.problem) ?? null;

/** Why the wizard suggests its route, in one line (or nothing for the plain default). */
export function reasonText(reason: SetupState["reason"]): string | null {
  switch (reason) {
    case "container": return "Your server runs in a container, so this needs no open port.";
    case "gameHost": return "Your server is on a game host, so this needs no open port.";
    case "ownAddress": return "Your server already has its own address set up.";
    default: return null;
  }
}
