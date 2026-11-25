// The panel's own API shapes (the auth lane's /api/auth, /api/users, /api/audit; the core's /api/core; the server
// module's /api/server/status). camelCase JSON, errors as {error: {code, message}}.

export type CommandLevelName = "none" | "everyone" | "admin" | "console";
export type Restriction = "password_change_required" | "2fa_setup_required" | "2fa_signin_required";

export interface AuthStatus {
  ownerExists: boolean;
  signedIn: boolean;
  launcherLogin: boolean;
  /** "local" (the password), then other sign-in providers (step 2: Steam, Discord) as {id, title} */
  providers: (string | { id: string; title: string })[];
}

export interface Me {
  id: string;
  name: string;
  roles: string[];
  owner: boolean;
  permissions: string[];
  commandLevel: CommandLevelName;
  twoFactor: boolean;
  recoveryCodesLeft: number;
  restriction: Restriction | null;
  session: { handle: string; via: string; expires: string; reauthenticatedUntil?: string | null };
}

/** A sign-in step's answer: signed in (with the user), two-factor next, or setup next (launcher login, no owner). */
export type SignInAnswer =
  | { ok: true; user: Me | null }
  | { ok: false; next: "2fa" }
  | { ok: false; next: "setup"; setupToken: string };

export interface SessionInfo {
  handle: string;
  created: string;
  lastSeen: string;
  expires: string;
  ip: string;
  userAgent: string;
  via: string;
  twoFactor: boolean;
  current: boolean;
}

export interface TwoFactorSetup {
  secret: string;
  uri: string;
}

export interface Account {
  id: string;
  name: string;
  roles: string[];
  owner: boolean;
  primary: boolean;
  disabled: boolean;
  twoFactor: boolean;
  mustChangePassword: boolean;
  created: string;
  lastSignIn: string | null;
  createdBy: string | null;
  sessions: number;
  links: string[];
  /** roles given by a linked Steam or Discord sign-in (identity.json roleMap), apart from `roles`; read-only here */
  mappedRoles?: string[];
}

export interface Role {
  id: string;
  name: string;
  builtin: boolean;
  grants: string[];
  revokes: string[];
  commandLevel: CommandLevelName;
  maxCommandLevel: CommandLevelName;
  commandDeny: string[];
  commandOnly: string[] | null;
  require2fa: boolean;
  effective: string[];
  members: number;
}

export interface RoleInput {
  id?: string;
  name?: string;
  grants?: string[];
  revokes?: string[];
  commandLevel?: CommandLevelName;
  commandDeny?: string[];
  commandOnly?: string[] | null;
  require2fa?: boolean;
}

export interface PermissionInfo {
  name: string;
  title: string;
  description: string | null;
  defaultRole: "viewer" | "moderator" | "admin" | "owner";
  sensitive: boolean;
}

export interface PermissionModule {
  module: string;
  title: string;
  enabled: boolean;
  permissions: PermissionInfo[];
}

export interface AuditEntry {
  at: string;
  user: string;
  userName: string;
  op: string;
  ok: boolean;
  error?: string;
  ms: number;
  ip: string | null;
  args?: unknown;
  lost?: number;
}

export interface NavPage {
  id: string;
  title: string;
  kind: "builtin" | "declarative" | "custom";
  entry: string;
  icon: string | null;
}

export interface NavModule {
  id: string;
  title: string;
  icon: string | null;
  pages: NavPage[];
  widgets: unknown[];
  permissions: string[];
}

export interface Branding {
  name: string;
  accent: string | null;
  logo: string | null;
  favicon: string | null;
}

export interface ServerStatus {
  connected: boolean;
  kiln: string | null;
  admin: string | null;
  server: { id?: string | null; name?: string | null } | null;
  restart: { supported?: "supported" | "unknown" | string; via?: string | null; code?: number | null; [k: string]: unknown } | null;
  ops: string[] | null;
  topics: string[] | null;
}

export interface PanelInfo {
  product: string;
  version: string;
  kiln: string;
  endpoints: string[];
  tls: { engine: string; certificate: { sha256: string; subject: string; names: string[]; notAfter: string; keyType: string; source: string; chain: number } | null };
  connection: { protocol: string | null; cipherSuite: string | null; alpn: string | null; client: string };
}
