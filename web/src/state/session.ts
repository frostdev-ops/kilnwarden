// Who is signed in, and what the shell shows for it. A small store (zustand/vanilla) the pages and the shell read:
//   loading     -> the first status/me requests are on their way
//   signedOut   -> the sign-in page (status says whether an owner exists; `notice` why we are here)
//   twoFactor   -> sign-in step two (a two-factor ticket cookie is set)
//   signedIn    -> the panel; `me.restriction` set: only the page that fixes it; `notAllowed`: the KAP socket was
//                  refused (4403) and the admin pages show why
// Transitions come from the sign-in calls, from the API client's auth signals (a 401 or a restricted 403 on any route)
// and from the KAP socket's close codes (4401 -> re-read the status, signed out unless it says otherwise; 4403 ->
// re-read me, then "not allowed" unless a restriction explains it).
import { clearHistories } from "../commands/history";
import { createStore, type StoreApi } from "zustand/vanilla";
import { isApiError } from "../api/http";
import type { Endpoints } from "../api/endpoints";
import type { AuthStatus, Branding, Me, SignInAnswer } from "../api/types";

export type Phase = "loading" | "signedOut" | "twoFactor" | "signedIn";

export interface SessionState {
  phase: Phase;
  status: AuthStatus | null;
  me: Me | null;
  branding: Branding;
  /** Why the user is on the sign-in page (their session ended, they signed out). */
  notice: string | null;
  /** The KAP socket was refused with 4403: the reason. */
  notAllowed: string | null;
  /** Bumped after every sign-in, so the admin pages reconnect. */
  signIns: number;
  reauthNeeded: boolean;
}

export const DEFAULT_BRANDING: Branding = { name: "Kilnwarden", accent: null, logo: null, favicon: null };

export interface SessionActions {
  boot(): Promise<void>;
  refresh(): Promise<void>;
  signIn(name: string, password: string): Promise<SignInAnswer>;
  twoFactor(code: string | null, recoveryCode: string | null): Promise<SignInAnswer>;
  /** After setup or the launcher login answered with a session (or any call that returns one). */
  accept(answer: SignInAnswer): Promise<void>;
  signOut(): Promise<void>;
  /** The API client saw a 401 or a restricted 403. */
  authSignal(kind: "unauthenticated" | "restricted" | "reauth"): Promise<void>;
  /** The KAP socket closed with 4401 or 4403. */
  socketClosed(code: number, reason: string): Promise<void>;
  clearNotAllowed(): void;
  dismissReauth(): void;
  /** Leave the two-factor step (start over with the password). */
  cancelTwoFactor(): void;
}

export type SessionStore = StoreApi<SessionState & SessionActions>;

export function createSession(api: Endpoints): SessionStore {
  return createStore<SessionState & SessionActions>()((set, get) => {
    const signedOut = (status: AuthStatus | null, notice: string | null) => {
      clearHistories(); // the console's lines never outlive the sign-in (security review S5)
      set({ phase: "signedOut", me: null, status, notice, notAllowed: null, reauthNeeded: false });
    };

    const loadMe = async () => {
      const me = await api.me();
      set((s) => ({ phase: "signedIn", me, notice: null, status: s.status ? { ...s.status, signedIn: true, ownerExists: true } : s.status }));
    };

    const statusOrNull = () => api.status().catch(() => null);

    return {
      phase: "loading",
      status: null,
      me: null,
      branding: DEFAULT_BRANDING,
      notice: null,
      notAllowed: null,
      signIns: 0,
      reauthNeeded: false,

      async boot() {
        const [branding, status] = await Promise.all([api.branding().catch(() => null), statusOrNull()]);
        if (branding) set({ branding });
        set({ status });
        if (!status?.signedIn) return signedOut(status, null);
        try {
          await loadMe();
        } catch {
          signedOut(await statusOrNull(), null);
        }
      },

      async refresh() {
        try {
          await loadMe();
        } catch (e) {
          if (isApiError(e) && e.status === 401) signedOut(await statusOrNull(), "Your sign-in ended. Sign in again.");
          else throw e;
        }
      },

      async signIn(name, password) {
        const r = await api.signIn(name, password);
        await get().accept(r);
        return r;
      },

      async twoFactor(code, recoveryCode) {
        const r = await api.twoFactor(code, recoveryCode);
        await get().accept(r);
        return r;
      },

      async accept(r) {
        if (r.ok) {
          set((s) => ({ signIns: s.signIns + 1, notAllowed: null }));
          if (r.user) set((s) => ({ phase: "signedIn", me: r.user, notice: null, status: s.status ? { ...s.status, signedIn: true, ownerExists: true } : s.status }));
          else await loadMe();
        } else if (r.next === "2fa") set({ phase: "twoFactor", notice: null });
      },

      async signOut() {
        try {
          await api.signOut();
        } catch {
          // signed out anyway (an expired session answers 401)
        }
        signedOut(await statusOrNull(), "You signed out.");
      },

      async authSignal(kind) {
        if (get().phase !== "signedIn") return;
        if (kind === "reauth") { set({ reauthNeeded: true }); return; }
        if (kind === "restricted") return get().refresh().catch(() => undefined);
        const status = await statusOrNull();
        if (status?.signedIn) return;
        signedOut(status, "Your sign-in ended. Sign in again.");
      },

      async socketClosed(code, reason) {
        if (get().phase !== "signedIn") return;
        if (code === 4401) {
          const status = await statusOrNull();
          if (status?.signedIn) return;
          signedOut(status, "Your sign-in ended. Sign in again.");
          return;
        }
        // 4403: a restriction explains it (the shell shows the page that fixes it); else the gate said no
        try {
          await loadMe();
        } catch {
          // keep what we have
        }
        if (!get().me?.restriction) set({ notAllowed: reason || "The server refused the admin connection for your account." });
      },

      dismissReauth() { set({ reauthNeeded: false }); },

      clearNotAllowed() {
        set({ notAllowed: null });
      },

      cancelTwoFactor() {
        if (get().phase === "twoFactor") set({ phase: "signedOut", notice: null });
      },
    };
  });
}

/** What a user may do, from me.permissions (the server checks again; the UI only hides). */
export function can(me: Me | null, permission: string): boolean {
  return !!me && (me.owner || me.permissions.includes(permission));
}
