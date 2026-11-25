// Users and roles (DESIGN §3): accounts (create with a temporary password, roles, disable, reset password or
// two-factor, sessions, delete) and roles (built-in and custom: permissions per module, the command level, command
// rules, required two-factor). The server enforces who may hand out what (nobody gives more than they have); this
// page shows its refusals as they come.
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { ep, useSession } from "../app/context";
import type { Account, CommandLevelName, PermissionModule, Role, RoleInput } from "../api/types";
import { can } from "../state/session";
import { Banner, Button, Card, LoadError, Loading, Modal, Page, Pill, Tabs, TextField, explain, when } from "../ui/kit";
import { SessionTable } from "./Account";

const msg = (e: unknown) => explain(e);
/** A role's name from its id (the id when the list has not loaded). */
const roleName = (roles: Role[] | undefined, id: string) => roles?.find((r) => r.id === id)?.name ?? id;
const LEVELS: CommandLevelName[] = ["none", "everyone", "admin", "console"];
const RANK: Record<string, number> = { viewer: 0, moderator: 1, admin: 2, owner: 3 };

export function UsersPage() {
  const me = useSession((s) => s.me)!;
  const [tab, setTab] = useState<"accounts" | "roles">("accounts");
  return (
    <Page title="Users and roles" wide sub="Accounts that may sign in to the panel, and what each role may do.">
      <Tabs label="Users and roles" value={tab} onChange={setTab} tabs={[{ id: "accounts", label: "Accounts" }, { id: "roles", label: "Roles" }]} />
      {!can(me, "users.view") ? <Banner tone="warn" title="Your account may not see the accounts">Ask the server's owner for a role that may.</Banner> : tab === "accounts" ? <Accounts /> : <Roles />}
    </Page>
  );
}

function useUsersData() {
  const accounts = useQuery({ queryKey: ["accounts"], queryFn: ep.accounts });
  const roles = useQuery({ queryKey: ["roles"], queryFn: ep.roles });
  return { accounts, roles };
}

function Accounts() {
  const me = useSession((s) => s.me)!;
  const qc = useQueryClient();
  const { accounts, roles } = useUsersData();
  const [creating, setCreating] = useState(false);
  const [managing, setManaging] = useState<Account | null>(null);
  const manage = can(me, "users.manage");
  const reload = () => Promise.all([qc.invalidateQueries({ queryKey: ["accounts"] }), qc.invalidateQueries({ queryKey: ["roles"] })]);
  return (
    <Card title="Accounts" actions={manage && <Button kind="primary" size="s" onClick={() => setCreating(true)}>New account</Button>}>
      {accounts.isError && <LoadError what="The accounts" error={accounts.error} retry={() => void accounts.refetch()} />}
      {accounts.isLoading && <Loading />}
      <div className="wa-table-wrap">
        <table className="wa-table wa-table-compact">
          <thead><tr><th>Name</th><th>Roles</th><th>Two-factor</th><th>Last sign-in</th><th>Sessions</th><th><span className="k-sr-only">Actions</span></th></tr></thead>
          <tbody>
            {(accounts.data ?? []).map((a) => (
              <tr key={a.id} className={a.disabled ? "wa-dim" : undefined}>
                <td data-label="Name"><strong>{a.name}</strong> {a.id === me.id && <Pill tone="ember">you</Pill>} {a.disabled && <Pill tone="err">disabled</Pill>} {a.mustChangePassword && <Pill tone="warn">temporary password</Pill>}</td>
                <td data-label="Roles">{a.roles.map((r) => roleName(roles.data, r)).join(", ")}
                  {!!a.mappedRoles?.length && <div className="k-muted">from Steam or Discord: {a.mappedRoles.map((r) => roleName(roles.data, r)).join(", ")}</div>}</td>
                <td data-label="Two-factor">{a.twoFactor ? <Pill tone="ok">on</Pill> : <Pill>off</Pill>}</td>
                <td data-label="Last sign-in">{when(a.lastSignIn)}</td>
                <td data-label="Sessions">{a.sessions}</td>
                <td className="wa-actions">{a.id !== me.id ? <Button size="s" aria-label={`${manage ? "Manage" : "Details of"} ${a.name}`} onClick={() => setManaging(a)}>{manage ? "Manage" : "Details"}</Button>
                  : <Link className="k-btn k-btn-s k-btn-ghost" to="/account">My account</Link>}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {creating && <CreateAccount roles={roles.data ?? []} onClose={() => setCreating(false)} onDone={reload} />}
      {managing && <ManageAccount account={managing} roles={roles.data ?? []} canManage={manage} onClose={() => setManaging(null)} onDone={reload} />}
    </Card>
  );
}

function RolePicker({ roles, value, onChange }: { roles: Role[]; value: string[]; onChange: (v: string[]) => void }) {
  return (
    <fieldset className="wa-checks">
      <legend className="k-label">Roles</legend>
      {roles.map((r) => (
        <label key={r.id} className="wa-check">
          <input type="checkbox" className="k-check" checked={value.includes(r.id)} onChange={(e) => onChange(e.target.checked ? [...value, r.id] : value.filter((x) => x !== r.id))} />
          <span>{r.name} {r.builtin ? "" : <span className="k-muted">(custom)</span>}{r.require2fa ? <span className="k-muted"> · needs 2FA</span> : null}</span>
        </label>
      ))}
    </fieldset>
  );
}

function CreateAccount({ roles, onClose, onDone }: { roles: Role[]; onClose: () => void; onDone: () => Promise<unknown> }) {
  const [name, setName] = useState("");
  const [password, setPassword] = useState("");
  const [picked, setPicked] = useState<string[]>(["viewer"]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  return (
    <Modal title="New account" onClose={onClose} footer={
      <>
        <Button kind="ghost" onClick={onClose}>Cancel</Button>
        <Button kind="primary" busy={busy} disabled={!name.trim() || password.length < 12 || !picked.length} onClick={async () => {
          setBusy(true);
          setError(null);
          try {
            await ep.createAccount(name.trim(), password, picked);
            await onDone();
            onClose();
          } catch (e) {
            setError(msg(e));
          } finally {
            setBusy(false);
          }
        }}>Create</Button>
      </>
    }>
      <div className="wa-form">
        <TextField label="Name" value={name} onChange={setName} autoComplete="off" hint="3 to 32 letters, digits, dots, dashes or underscores." />
        <TextField label="Temporary password" type="password" value={password} onChange={setPassword} autoComplete="new-password" hint="At least 12 characters. They choose their own at the first sign-in." />
        <RolePicker roles={roles.filter((r) => r.id !== "owner")} value={picked} onChange={setPicked} />
        {error && <Banner tone="err" title={error} />}
      </div>
    </Modal>
  );
}

function ManageAccount({ account, roles, canManage, onClose, onDone }: { account: Account; roles: Role[]; canManage: boolean; onClose: () => void; onDone: () => Promise<unknown> }) {
  const [picked, setPicked] = useState(account.roles);
  const [password, setPassword] = useState("");
  const [confirmDelete, setConfirmDelete] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [note, setNote] = useState<string | null>(null);
  const [busy, setBusy] = useState<string | null>(null);
  const sessions = useQuery({ queryKey: ["account-sessions", account.id], queryFn: () => ep.accountSessions(account.id) });
  const act = async (key: string, f: () => Promise<unknown>, done: string, close = false) => {
    setBusy(key);
    setError(null);
    setNote(null);
    try {
      await f();
      await onDone();
      await sessions.refetch();
      if (close) onClose();
      else setNote(done);
    } catch (e) {
      setError(msg(e));
    } finally {
      setBusy(null);
    }
  };
  return (
    <Modal wide title={`Account: ${account.name}`} onClose={onClose}>
      <div className="wa-form">
        <p className="k-muted">Created {when(account.created)}{account.createdBy ? ` by ${account.createdBy}` : ""} · last sign-in {when(account.lastSignIn)}</p>
        {error && <Banner tone="err" title={error} />}
        {note && <Banner tone="ok" title={note} />}
        {!!account.mappedRoles?.length && (
          <Banner tone="info" title={`From Steam or Discord: ${account.mappedRoles.map((r) => roleName(roles, r)).join(", ")}`}>
            Given at each sign-in by the role mapping in Panel settings (Sign-in), apart from the roles below. Change the mapping there to change these.
          </Banner>
        )}
        {canManage && (
          <>
            <RolePicker roles={roles} value={picked} onChange={setPicked} />
            <div className="wa-row">
              <Button kind="primary" busy={busy === "roles"} disabled={!picked.length || picked.join() === account.roles.join()} onClick={() => act("roles", () => ep.updateAccount(account.id, { roles: picked }), "Roles saved. Their sessions get the new rights at the next request.")}>Save roles</Button>
              <Button busy={busy === "disable"} onClick={() => act("disable", () => ep.updateAccount(account.id, { disabled: !account.disabled }), account.disabled ? "Enabled." : "Disabled and signed out.", true)}>{account.disabled ? "Enable" : "Disable"}</Button>
            </div>
            <div className="wa-row wa-end">
              <TextField label="New temporary password" type="password" value={password} onChange={setPassword} autoComplete="new-password" />
              <Button busy={busy === "password"} disabled={password.length < 12} onClick={() => act("password", () => ep.resetPassword(account.id, password), "Password reset; they choose a new one at sign-in. Their sessions ended.")}>Reset password</Button>
            </div>
            <div className="wa-row">
              {account.twoFactor && <Button busy={busy === "2fa"} onClick={() => act("2fa", () => ep.resetTwoFactor(account.id), "Two-factor turned off for this account. Their sessions ended.")}>Reset two-factor</Button>}
              <Button busy={busy === "revoke"} disabled={!sessions.data?.length} onClick={() => act("revoke", () => ep.revokeAccountSessions(account.id), "Signed out everywhere.")}>Sign out everywhere</Button>
            </div>
          </>
        )}
        <h3 className="wa-h3">Sessions</h3>
        <SessionTable sessions={sessions.data ?? []} />
        {canManage && !account.primary && (
          <div className="wa-danger-zone">
            <TextField label={`Type "${account.name}" to delete this account`} value={confirmDelete} onChange={setConfirmDelete} autoComplete="off" />
            <Button kind="danger" busy={busy === "delete"} disabled={confirmDelete !== account.name} onClick={() => act("delete", () => ep.deleteAccount(account.id), "Deleted.", true)}>Delete account</Button>
          </div>
        )}
      </div>
    </Modal>
  );
}

// ---------------------------------------------------------------- roles

function Roles() {
  const me = useSession((s) => s.me)!;
  const qc = useQueryClient();
  const { roles } = useUsersData();
  const perms = useQuery({ queryKey: ["permissions"], queryFn: ep.permissions });
  const [editing, setEditing] = useState<Role | "new" | null>(null);
  const canEdit = can(me, "users.roles");
  const reload = () => Promise.all([qc.invalidateQueries({ queryKey: ["roles"] }), qc.invalidateQueries({ queryKey: ["accounts"] })]);
  return (
    <Card title="Roles" actions={canEdit && <Button kind="primary" size="s" onClick={() => setEditing("new")}>New role</Button>}>
      {roles.isError && <LoadError what="The roles" error={roles.error} retry={() => void roles.refetch()} />}
      {roles.isLoading && <Loading />}
      <div className="wa-table-wrap">
        <table className="wa-table">
          <thead><tr><th>Role</th><th>Commands up to</th><th>Permissions</th><th>Two-factor</th><th>Members</th><th><span className="k-sr-only">Actions</span></th></tr></thead>
          <tbody>
            {(roles.data ?? []).map((r) => (
              <tr key={r.id}>
                <td data-label="Role"><strong>{r.name}</strong> <span className="k-mono k-muted">{r.id}</span> {r.builtin && <Pill>built-in</Pill>}</td>
                <td data-label="Commands up to">{r.commandLevel}{r.commandOnly ? " (only listed)" : r.commandDeny.length ? ` (${r.commandDeny.length} denied)` : ""}</td>
                <td data-label="Permissions">{r.effective.length}</td>
                <td data-label="Two-factor">{r.require2fa ? <Pill tone="warn">required</Pill> : "optional"}</td>
                <td data-label="Members">{r.members}</td>
                <td className="wa-actions"><Button size="s" onClick={() => setEditing(r)}>{canEdit && r.id !== "owner" ? "Edit" : "View"}</Button></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {editing && (
        <RoleEditor role={editing === "new" ? null : editing} modules={perms.data ?? []} canEdit={canEdit} owner={me.owner}
          onClose={() => setEditing(null)} onDone={reload} />
      )}
    </Card>
  );
}

const lines = (s: string) => s.split(/[\n,]/).map((x) => x.trim()).filter(Boolean);

/** Whether `perm` is covered by a grant pattern ("spawn.*", "*"). */
function covered(patterns: string[], perm: string): string | null {
  for (const p of patterns) if (p === perm || p === "*" || p.endsWith(".*") && perm.startsWith(p.slice(0, -1))) return p;
  return null;
}

function RoleEditor({ role, modules, canEdit, owner, onClose, onDone }: {
  role: Role | null; modules: PermissionModule[]; canEdit: boolean; owner: boolean; onClose: () => void; onDone: () => Promise<unknown>;
}) {
  const creating = role === null;
  const [id, setId] = useState("");
  const [name, setName] = useState(role?.name ?? "");
  const [grants, setGrants] = useState<string[]>(role?.grants ?? []);
  const [revokes, setRevokes] = useState<string[]>(role?.revokes ?? []);
  const [level, setLevel] = useState<CommandLevelName>(role?.commandLevel ?? "none");
  const [deny, setDeny] = useState((role?.commandDeny ?? []).join("\n"));
  const [onlyOn, setOnlyOn] = useState(role?.commandOnly != null);
  const [only, setOnly] = useState((role?.commandOnly ?? []).join("\n"));
  const [require2fa, setRequire2fa] = useState(role?.require2fa ?? false);
  const [confirmDelete, setConfirmDelete] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const isOwnerRole = role?.id === "owner";
  const editable = canEdit && (!role?.builtin || owner);
  const rank = role?.builtin ? RANK[role.id] ?? -1 : -1;
  const maxLevel = role?.maxCommandLevel ?? "admin";

  const has = useMemo(() => (perm: string, defaultRole: string) => {
    if (isOwnerRole) return true;
    const byDefault = rank >= 0 && RANK[defaultRole] <= rank;
    if (byDefault) return !covered(revokes, perm);
    return !!covered(grants, perm) && !covered(revokes, perm);
  }, [grants, revokes, rank, isOwnerRole]);

  const toggle = (perm: string, defaultRole: string, on: boolean) => {
    const byDefault = rank >= 0 && RANK[defaultRole] <= rank;
    if (on) {
      setRevokes((r) => r.filter((x) => x !== perm));
      if (!byDefault && !covered(grants, perm)) setGrants((g) => [...g, perm]);
    } else {
      setGrants((g) => g.filter((x) => x !== perm));
      if (byDefault || covered(grants.filter((x) => x !== perm), perm)) setRevokes((r) => (r.includes(perm) ? r : [...r, perm]));
    }
  };

  const save = async () => {
    setBusy(true);
    setError(null);
    const body: RoleInput = isOwnerRole ? { require2fa } : {
      name: name.trim() || undefined, grants, revokes: role?.builtin ? revokes : [], commandLevel: level, commandDeny: lines(deny),
      commandOnly: onlyOn ? lines(only) : null, require2fa,
    };
    try {
      if (creating) await ep.createRole({ id: id.trim(), ...body });
      else await ep.updateRole(role!.id, body);
      await onDone();
      onClose();
    } catch (e) {
      setError(msg(e));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal wide title={creating ? "New role" : `Role: ${role!.name}`} onClose={onClose} footer={editable && (
      <>
        <Button kind="ghost" onClick={onClose}>Cancel</Button>
        <Button kind="primary" busy={busy} disabled={creating && !/^[a-z][a-z0-9_-]{0,31}$/.test(id.trim())} onClick={save}>Save</Button>
      </>
    )}>
      <div className="wa-form">
        {creating && <TextField label="Role id" value={id} onChange={setId} mono hint="a-z, 0-9, _ and -, starting with a letter (never changes)." />}
        {!isOwnerRole && <TextField label="Name" value={name} onChange={setName} disabled={!editable} />}
        {isOwnerRole && <p className="k-muted">The owner role has every permission. Only its two-factor rule can change.</p>}
        <label className="wa-check">
          <input type="checkbox" className="k-check" checked={require2fa} disabled={!editable} onChange={(e) => setRequire2fa(e.target.checked)} />
          <span>Members must use two-factor sign-in</span>
        </label>
        {!isOwnerRole && (
          <>
            <div className="k-field">
              <label className="k-label" htmlFor="wa-level">Commands up to</label>
              <select id="wa-level" className="k-select" value={level} disabled={!editable} onChange={(e) => setLevel(e.target.value as CommandLevelName)}>
                {LEVELS.filter((l) => LEVELS.indexOf(l) <= LEVELS.indexOf(maxLevel)).map((l) => <option key={l} value={l}>{l}</option>)}
              </select>
              <div className="k-hint">The highest Kiln command level this role's lines run at (at most {maxLevel} for this role).</div>
            </div>
            <div className="wa-grid wa-grid-2">
              <div className="k-field">
                <label className="k-label" htmlFor="wa-deny">Commands it may not run</label>
                <textarea id="wa-deny" className="k-textarea" rows={3} value={deny} disabled={!editable} onChange={(e) => setDeny(e.target.value)} placeholder={"kick\nclaims.command.*"} />
                <div className="k-hint">A command's name or permission node, one per line; ".*" ends a prefix.</div>
              </div>
              <div className="k-field">
                <label className="wa-check">
                  <input type="checkbox" className="k-check" checked={onlyOn} disabled={!editable} onChange={(e) => setOnlyOn(e.target.checked)} />
                  <span>Only these commands</span>
                </label>
                <textarea aria-label="Only these commands" className="k-textarea" rows={3} value={only} disabled={!editable || !onlyOn} onChange={(e) => setOnly(e.target.value)} placeholder={"tell\nbroadcast"} />
              </div>
            </div>
            <h3 className="wa-h3">Permissions</h3>
            {grants.some((g) => g.endsWith("*")) && <p className="k-hint">Pattern grants: {grants.filter((g) => g.endsWith("*")).join(", ")}</p>}
            <div className="wa-perms">
              {modules.map((m) => (
                <fieldset key={m.module} className="wa-perm-group">
                  <legend>{m.title} {!m.enabled && <span className="k-muted">(module off)</span>}</legend>
                  {m.permissions.map((p) => {
                    const on = has(p.name, p.defaultRole);
                    return (
                      <label key={p.name} className="wa-check" title={p.description ?? undefined}>
                        <input type="checkbox" className="k-check" checked={on} disabled={!editable || isOwnerRole} onChange={(e) => toggle(p.name, p.defaultRole, e.target.checked)} />
                        <span>{p.title} <span className="k-mono k-muted">{p.name}</span>{p.sensitive && <Pill tone="warn">sensitive</Pill>}</span>
                      </label>
                    );
                  })}
                </fieldset>
              ))}
            </div>
          </>
        )}
        {error && <Banner tone="err" title={error} />}
        {editable && role && !role.builtin && (
          <div className="wa-danger-zone">
            <TextField label={`Type "${role.id}" to delete this role (${role.members} member${role.members === 1 ? "" : "s"} lose it)`} value={confirmDelete} onChange={setConfirmDelete} autoComplete="off" />
            <Button kind="danger" disabled={confirmDelete !== role.id} onClick={async () => {
              try {
                await ep.deleteRole(role.id);
                await onDone();
                onClose();
              } catch (e) {
                setError(msg(e));
              }
            }}>Delete role</Button>
          </div>
        )}
      </div>
    </Modal>
  );
}
