import { describe, expect, test } from "bun:test";
import { ApiError } from "../src/api/http";
import type { Endpoints } from "../src/api/endpoints";
import type { AuthStatus, Me } from "../src/api/types";
import { can, createSession } from "../src/state/session";

const me = (over: Partial<Me> = {}): Me => ({
  id: "a1", name: "alice", roles: ["admin"], owner: false, permissions: ["users.view"], commandLevel: "admin", twoFactor: false,
  recoveryCodesLeft: 0, restriction: null, session: { handle: "h1", via: "password", expires: "2026-11-24T00:00:00Z" }, ...over,
});

function fakeApi(over: Partial<Record<keyof Endpoints, unknown>> = {}) {
  const state = { status: { ownerExists: true, signedIn: false, launcherLogin: true, providers: ["local"] } as AuthStatus, me: me() as Me | null, calls: [] as string[] };
  const api = {
    branding: async () => ({ name: "Embervale", accent: "#2a9df4", logo: null, favicon: null }),
    status: async () => ({ ...state.status }),
    me: async () => {
      state.calls.push("me");
      if (!state.me) throw new ApiError(401, "unauthenticated", "sign in");
      return state.me;
    },
    signIn: async () => ({ ok: true, user: state.me }),
    twoFactor: async () => ({ ok: true, user: state.me }),
    signOut: async () => {
      state.calls.push("logout");
    },
    ...over,
  } as unknown as Endpoints;
  return { api, state };
}

describe("session", () => {
  test("boot: signed out, with branding and status", async () => {
    const { api } = fakeApi();
    const s = createSession(api);
    expect(s.getState().phase).toBe("loading");
    await s.getState().boot();
    expect(s.getState().phase).toBe("signedOut");
    expect(s.getState().branding.name).toBe("Embervale");
    expect(s.getState().status?.ownerExists).toBe(true);
  });

  test("boot: signed in loads me", async () => {
    const { api, state } = fakeApi();
    state.status.signedIn = true;
    const s = createSession(api);
    await s.getState().boot();
    expect(s.getState().phase).toBe("signedIn");
    expect(s.getState().me?.name).toBe("alice");
  });

  test("sign-in with two-factor: twoFactor, then signed in (and the admin pages may reconnect)", async () => {
    const { api } = fakeApi({ signIn: async () => ({ ok: false, next: "2fa" }) });
    const s = createSession(api);
    await s.getState().boot();
    await s.getState().signIn("alice", "pw");
    expect(s.getState().phase).toBe("twoFactor");
    expect(s.getState().signIns).toBe(0);
    await s.getState().twoFactor("123456", null);
    expect(s.getState().phase).toBe("signedIn");
    expect(s.getState().signIns).toBe(1);
  });

  test("a failed sign-in stays signed out and throws", async () => {
    const { api } = fakeApi({ signIn: async () => { throw new ApiError(401, "invalid_credentials", "wrong name or password"); } });
    const s = createSession(api);
    await s.getState().boot();
    await expect(s.getState().signIn("alice", "nope")).rejects.toThrow("wrong name or password");
    expect(s.getState().phase).toBe("signedOut");
  });

  test("4401 from the socket: signed out with a notice, unless the status still says signed in", async () => {
    const { api, state } = fakeApi();
    state.status.signedIn = true;
    const s = createSession(api);
    await s.getState().boot();
    await s.getState().socketClosed(4401, "the sign-in ended");
    expect(s.getState().phase).toBe("signedIn");
    state.status.signedIn = false;
    await s.getState().socketClosed(4401, "the sign-in ended");
    expect(s.getState().phase).toBe("signedOut");
    expect(s.getState().me).toBeNull();
    expect(s.getState().notice).toContain("sign-in ended");
  });

  test("4403 with a restriction: me shows it, no not-allowed view", async () => {
    const { api, state } = fakeApi();
    state.status.signedIn = true;
    const s = createSession(api);
    await s.getState().boot();
    state.me = me({ restriction: "2fa_setup_required" });
    await s.getState().socketClosed(4403, "set it up first");
    expect(s.getState().me?.restriction).toBe("2fa_setup_required");
    expect(s.getState().notAllowed).toBeNull();
  });

  test("4403 without one: the not-allowed view with the reason", async () => {
    const { api, state } = fakeApi();
    state.status.signedIn = true;
    const s = createSession(api);
    await s.getState().boot();
    await s.getState().socketClosed(4403, "not for you");
    expect(s.getState().notAllowed).toBe("not for you");
    s.getState().clearNotAllowed();
    expect(s.getState().notAllowed).toBeNull();
  });

  test("an API 401 signs out; a restricted 403 re-reads me", async () => {
    const { api, state } = fakeApi();
    state.status.signedIn = true;
    const s = createSession(api);
    await s.getState().boot();
    state.me = me({ restriction: "password_change_required" });
    await s.getState().authSignal("restricted");
    expect(s.getState().me?.restriction).toBe("password_change_required");
    state.status.signedIn = false;
    await s.getState().authSignal("unauthenticated");
    expect(s.getState().phase).toBe("signedOut");
  });

  test("sign out calls logout and says so", async () => {
    const { api, state } = fakeApi();
    state.status.signedIn = true;
    const s = createSession(api);
    await s.getState().boot();
    state.status.signedIn = false;
    await s.getState().signOut();
    expect(state.calls).toContain("logout");
    expect(s.getState().phase).toBe("signedOut");
    expect(s.getState().notice).toBe("You signed out.");
  });

  test("can(): owner has everything, others their list", () => {
    expect(can(me(), "users.view")).toBe(true);
    expect(can(me(), "users.manage")).toBe(false);
    expect(can(me({ owner: true, permissions: [] }), "users.manage")).toBe(true);
    expect(can(null, "users.view")).toBe(false);
  });
});
