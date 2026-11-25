// Every panel route the web app uses, typed. Only routes the server has (the auth lane's AuthRoutes/UserRoutes/
// AuditRoutes, the core's CoreComponent, the branding component, the server module's status).
import { seg, type Api } from "./http";
import type {
  Account, AuditEntry, AuthStatus, Branding, Me, NavModule, PanelInfo, PermissionModule, Role, RoleInput, ServerStatus, SessionInfo,
  SignInAnswer, TwoFactorSetup,
} from "./types";

export interface AuditQuery {
  limit?: number;
  before?: string;
  user?: string;
  op?: string;
}

export function endpoints(api: Api) {
  return {
    // public
    branding: () => api.get<Branding>("/api/core/branding"),
    status: () => api.get<AuthStatus>("/api/auth/status"),
    signIn: (name: string, password: string) => api.post<SignInAnswer>("/api/auth/login", { name, password }),
    twoFactor: (code: string | null, recoveryCode: string | null) => api.post<SignInAnswer>("/api/auth/login/2fa", { code, recoveryCode }),
    setup: (token: string, name: string, password: string) => api.post<SignInAnswer>("/api/auth/setup", { token, name, password }),
    launcher: (secret: string) => api.post<SignInAnswer>("/api/auth/launcher", { secret }),
    // the signed-in user
    me: () => api.get<Me>("/api/auth/me"),
    reauth: (proof: { password: string; code?: never } | { code: string; password?: never }) => api.post<{ ok: true; reauthenticatedUntil: string }>("/api/auth/reauth", proof),
    signOut: () => api.post<void>("/api/auth/logout"),
    changePassword: (current: string, password: string) => api.post<SignInAnswer>("/api/auth/password", { current, password }),
    twoFactorBegin: (password: string) => api.post<TwoFactorSetup>("/api/auth/2fa/setup", { password }),
    twoFactorEnable: (code: string) => api.post<{ ok: true; recoveryCodes: string[] }>("/api/auth/2fa/enable", { code }),
    twoFactorDisable: (password: string, code: string) => api.post<SignInAnswer>("/api/auth/2fa/disable", { password, code }),
    recoveryCodes: (password: string) => api.post<{ recoveryCodes: string[] }>("/api/auth/2fa/recovery-codes", { password }),
    sessions: () => api.get<SessionInfo[]>("/api/auth/sessions"),
    revokeSession: (handle: string) => api.del<void>(`/api/auth/sessions/${seg(handle)}`),
    revokeOtherSessions: () => api.post<{ revoked: number }>("/api/auth/sessions/revoke-others"),
    // users and roles
    accounts: () => api.get<Account[]>("/api/users"),
    createAccount: (name: string, password: string, roles: string[]) => api.post<Account>("/api/users", { name, password, roles }),
    updateAccount: (id: string, patch: { roles?: string[]; disabled?: boolean }) => api.patch<Account>(`/api/users/${seg(id)}`, patch),
    resetPassword: (id: string, password: string) => api.post<void>(`/api/users/${seg(id)}/password`, { password }),
    resetTwoFactor: (id: string) => api.post<void>(`/api/users/${seg(id)}/2fa/reset`),
    deleteAccount: (id: string) => api.del<void>(`/api/users/${seg(id)}`),
    accountSessions: (id: string) => api.get<SessionInfo[]>(`/api/users/${seg(id)}/sessions`),
    revokeAccountSessions: (id: string) => api.del<{ revoked: number }>(`/api/users/${seg(id)}/sessions`),
    roles: () => api.get<Role[]>("/api/users/roles"),
    createRole: (role: RoleInput & { id: string }) => api.post<Role>("/api/users/roles", role),
    updateRole: (id: string, role: RoleInput) => api.put<Role>(`/api/users/roles/${seg(id)}`, role),
    deleteRole: (id: string) => api.del<void>(`/api/users/roles/${seg(id)}`),
    permissions: () => api.get<PermissionModule[]>("/api/users/permissions"),
    // audit
    audit: (q: AuditQuery = {}) => {
      const p = new URLSearchParams();
      if (q.limit) p.set("limit", String(q.limit));
      if (q.before) p.set("before", q.before);
      if (q.user) p.set("user", q.user);
      if (q.op) p.set("op", q.op);
      const qs = p.toString();
      return api.get<AuditEntry[]>(`/api/audit${qs ? `?${qs}` : ""}`);
    },
    // the panel
    modules: () => api.get<NavModule[]>("/api/core/modules"),
    info: () => api.get<PanelInfo>("/api/core/info"),
    serverStatus: () => api.get<ServerStatus>("/api/server/status"),
  };
}

export type Endpoints = ReturnType<typeof endpoints>;
