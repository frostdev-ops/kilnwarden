// Steam and Discord sign-in and account links (step 2 lane A; contract: docs/lanes/identity.md). The flow:
//   POST /api/identity/start -> the provider's page -> its callback on the panel -> back here with #ext=<state>
//   -> POST /api/identity/complete {state} (the HttpOnly binder cookie proves it is the same browser).
// The state arrives in the fragment (never sent to a server, never in a Referer) and is wiped from the address bar.
import { seg, type Api } from "../api/http";
import type { SignInAnswer } from "../api/types";

export type ProviderId = "steam" | "discord";
export type Intent = "login" | "link";

export interface Link {
  provider: string;
  subject: string;
  enabled: boolean;
}

export interface Links {
  providers: { id: string; title: string }[];
  links: Link[];
}

export type LinkAnswer = { ok: true; linked: { provider: string; subject: string } };

export function identityEndpoints(api: Api) {
  return {
    /** `back` "setup" (0.1.3, a link from the setup wizard): Discord's answer returns to the wizard, not My account. */
    start: (provider: string, intent: Intent, back?: "setup") => api.post<{ url: string }>("/api/identity/start", back ? { provider, intent, back } : { provider, intent }),
    complete: <T = SignInAnswer | LinkAnswer>(state: string) => api.post<T>("/api/identity/complete", { state }),
    links: () => api.get<Links>("/api/identity/links"),
    unlink: (provider: string) => api.del<void>(`/api/identity/links/${seg(provider)}`),
  };
}

/** What the callback put in the fragment: a state to complete, an error, or nothing; the fragment is wiped. */
export function takeExternalFragment(loc: { hash: string; pathname: string; search: string } = location, h: Pick<History, "replaceState" | "state"> = history):
  { state: string } | { error: string } | null {
  const hash = loc.hash.replace(/^#/, "");
  if (!hash) return null;
  const p = new URLSearchParams(hash);
  const state = p.get("ext");
  const error = p.get("ext-error");
  if (state === null && error === null) return null;
  h.replaceState(h.state, "", loc.pathname + loc.search);
  if (state !== null) return /^[A-Za-z0-9_-]{43}$/.test(state) ? { state } : { error: "malformed" };
  return { error: error || "failed" };
}

/** Only provider pages the panel's own answer names: Steam's endpoint or an https broker (never a script URL). */
export function safeProviderUrl(url: string): string | null {
  try {
    const u = new URL(url);
    return u.protocol === "https:" ? u.href : null;
  } catch {
    return null;
  }
}

export const PROVIDER_TITLES: Record<string, string> = { steam: "Steam", discord: "Discord" };

/** The user-facing text for the server's error codes. */
export function externalErrorText(code: string, provider?: string): string {
  const p = provider ? PROVIDER_TITLES[provider] ?? provider : "That";
  switch (code) {
    case "not_linked": return `No panel account is linked to this ${p} account. Sign in with your password, then link it under My account.`;
    case "not_allowed": return "Your Discord roles do not allow signing in to this panel.";
    case "ext_canceled": return "The sign-in was canceled.";
    case "ext_expired": case "expired": case "malformed": return "That sign-in expired or was started in another browser. Start again.";
    case "ext_pending": return "The provider has not answered yet. Start again.";
    case "linked_elsewhere": return `That ${p} account is linked to another panel account.`;
    case "not_member": return "That Discord account is not a member of this panel's Discord server.";
    case "owner_only": return "On this panel, only the owner signs in with Discord.";
    case "unknown_provider": return "That sign-in method is switched off.";
    default: return "The sign-in could not be verified. Start again.";
  }
}
