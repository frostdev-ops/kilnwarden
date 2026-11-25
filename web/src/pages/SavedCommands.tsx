// Saved commands (DESIGN §5a): filled-in command forms saved under a name. Run one again (as yourself: the server checks
// it like any command_run, Console-level lines need a recent proof), pin it to your dashboard, share it, use it in a
// schedule. A saved command that asks for confirmation shows its line and waits for an explicit yes.
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { Link } from "react-router-dom";
import { useSession } from "../app/context";
import { ops } from "../app/ops";
import { isApiError } from "../api/http";
import type { SavedCommand, SavedCommandInput } from "../api/ops";
import { can } from "../state/session";
import { Banner, Button, Card, Check, Empty, LoadError, Loading, Modal, Page, Pill, TextField, explain } from "../ui/kit";

const KEY = ["saved-commands"];

export function SavedCommandsPage() {
  const me = useSession((s) => s.me)!;
  const qc = useQueryClient();
  const q = useQuery({ queryKey: KEY, queryFn: ops.savedCommands });
  const [editing, setEditing] = useState<SavedCommand | "new" | null>(null);
  const [confirm, setConfirm] = useState<SavedCommand | null>(null);
  const [out, setOut] = useState<{ name: string; ok: boolean; text: string } | null>(null);
  const refresh = () => qc.invalidateQueries({ queryKey: KEY });
  const run = useMutation({
    mutationFn: ({ c, confirmed }: { c: SavedCommand; confirmed: boolean }) => ops.runCommand(c.id, confirmed),
    onSuccess: (r, { c }) => setOut({ name: c.name, ok: true, text: resultText(r.result) }),
    onError: (e, { c }) => setOut({ name: c.name, ok: false, text: reauthText(e) }),
  });
  const pin = useMutation({ mutationFn: (c: SavedCommand) => ops.pinCommand(c.id, !c.pinned), onSettled: refresh });
  const del = useMutation({ mutationFn: (c: SavedCommand) => ops.deleteCommand(c.id), onSettled: refresh });
  const manage = can(me, "saved-commands.manage");
  const manageAll = can(me, "saved-commands.manage_all");
  const start = (c: SavedCommand) => (c.confirm ? setConfirm(c) : run.mutate({ c, confirmed: false }));
  return (
    <Page title="Saved commands" wide sub="Commands saved from their forms. Each run is checked as your own command, at your level."
      actions={manage && <Button kind="primary" onClick={() => setEditing("new")}>New saved command</Button>}>
      {q.isError && <LoadError what="Saved commands" error={q.error} retry={() => void q.refetch()} />}
      <div aria-live="polite">
        {out && <Banner tone={out.ok ? "ok" : "err"} title={`${out.name}: ${out.ok ? "ran" : "not run"}`} actions={<Button size="s" kind="ghost" onClick={() => setOut(null)}>Close</Button>}>
          {out.text && <pre className="k-code-block wa-pre">{out.text}</pre>}
        </Banner>}
      </div>
      {q.isLoading && <Loading />}
      {q.data && !q.data.length && (
        <Card>
          <Empty title="Nothing saved yet">
            Fill in a command's form on the <Link to="/commands">Commands</Link> page and save it, or add one here. Saved commands can be pinned to your overview and used in schedules.
          </Empty>
        </Card>
      )}
      {!!q.data?.length && (
        <div className="wa-table-wrap">
          <table className="wa-table">
            <thead><tr><th>Name</th><th>Line</th><th>By</th><th>Flags</th><th><span className="k-sr-only">Actions</span></th></tr></thead>
            <tbody>
              {q.data.map((c) => (
                <tr key={c.id}>
                  <td data-label="Name">{c.name}{c.description && <div className="k-muted">{c.description}</div>}</td>
                  <td data-label="Line" className="k-mono wa-break">{c.line}</td>
                  <td data-label="By">{c.mine ? "you" : c.owner.name}</td>
                  <td data-label="Flags">
                    <div className="wa-row">
                      {c.shared && <Pill tone="info">shared</Pill>}
                      {c.confirm && <Pill tone="warn">asks first</Pill>}
                      {c.pinned && <Pill tone="ember">pinned</Pill>}
                      {!c.runnable && <Pill tone="err" title="your roles may not run this command">not for you</Pill>}
                    </div>
                  </td>
                  <td className="wa-actions">
                    <Button size="s" kind="primary" disabled={!c.runnable} busy={run.isPending && run.variables?.c.id === c.id} onClick={() => start(c)} aria-label={`Run ${c.name}`}>Run</Button>
                    <Button size="s" kind="ghost" onClick={() => pin.mutate(c)} aria-label={`${c.pinned ? "Unpin" : "Pin"} ${c.name}`}>{c.pinned ? "Unpin" : "Pin"}</Button>
                    {(c.mine ? manage : c.shared && manageAll) && <>
                      <Button size="s" kind="ghost" onClick={() => setEditing(c)} aria-label={`Edit ${c.name}`}>Edit</Button>
                      <Button size="s" kind="ghost" onClick={() => window.confirm(`Remove "${c.name}"?`) && del.mutate(c)} aria-label={`Remove ${c.name}`}>Remove</Button>
                    </>}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {editing && <SaveCommandDialog initial={editing === "new" ? null : editing} onClose={() => setEditing(null)} onSaved={() => { setEditing(null); void refresh(); }} />}
      {confirm && (
        <Modal title={`Run "${confirm.name}"?`} onClose={() => setConfirm(null)} footer={<div className="wa-row">
          <Button kind="primary" onClick={() => { run.mutate({ c: confirm, confirmed: true }); setConfirm(null); }}>Run it</Button>
          <Button kind="ghost" onClick={() => setConfirm(null)}>Cancel</Button>
        </div>}>
          <p>This runs as you, at your command level:</p>
          <div className="wa-preview"><span className="k-label">Line</span><code className="k-mono">{confirm.line}</code></div>
        </Modal>
      )}
    </Page>
  );
}

function reauthText(e: unknown): string {
  if (isApiError(e) && e.code === "reauth_required") return "Confirm your identity in the dialog, then run it again.";
  return explain(e);
}

/** A command_run answer as the lines it printed ("ok" when it printed nothing). */
function resultText(r: unknown): string {
  const o = r as { output?: unknown; error?: unknown } | null;
  if (o && Array.isArray(o.output) && o.output.length) return o.output.map(String).join("\n");
  if (o && typeof o.error === "string" && o.error) return o.error;
  if (typeof r === "string") return r;
  return "";
}

/** Save (or change) a saved command. From the Commands page, `line` and `form` come from the filled-in form. */
export function SaveCommandDialog({ initial, line, form, onClose, onSaved }: {
  initial: SavedCommand | null; line?: string; form?: unknown; onClose: () => void; onSaved: (c: SavedCommand) => void;
}) {
  const [name, setName] = useState(initial?.name ?? "");
  const [text, setText] = useState(initial?.line ?? line ?? "");
  const [description, setDescription] = useState(initial?.description ?? "");
  const [confirm, setConfirm] = useState(initial?.confirm ?? false);
  const [shared, setShared] = useState(initial?.shared ?? false);
  const save = useMutation({
    mutationFn: () => {
      const body: SavedCommandInput = { name, line: text, description: description || null, confirm, shared, form: initial ? initial.form : form };
      return initial ? ops.updateCommand(initial.id, body) : ops.saveCommand(body);
    },
    onSuccess: onSaved,
  });
  return (
    <Modal title={initial ? "Change saved command" : "Save command"} onClose={onClose} footer={<div className="wa-row">
      <Button kind="primary" busy={save.isPending} disabled={!name.trim() || !text.trim()} onClick={() => save.mutate()}>Save</Button>
      <Button kind="ghost" onClick={onClose}>Cancel</Button>
    </div>}>
      <div className="wa-form">
        <TextField label="Name" value={name} onChange={setName} maxLength={64} autoFocus />
        <TextField label="Line" value={text} onChange={setText} mono maxLength={1000} hint={line !== undefined && !initial ? "From the form you filled in." : undefined} />
        <TextField label="Description (optional)" value={description} onChange={setDescription} maxLength={300} />
        <Check label="Ask before running" checked={confirm} onChange={setConfirm} />
        <Check label="Share with others who may run commands" hint="(they run it as themselves)" checked={shared} onChange={setShared} />
        {initial && <p className="k-muted">Schedules that use this command stop until they are saved again after a change.</p>}
        {save.isError && <Banner tone="err" title={reauthText(save.error)} />}
      </div>
    </Modal>
  );
}

/** The dashboard widget: your pinned saved commands as buttons (no confirmation skipped). */
export function PinnedCommands() {
  const q = useQuery({ queryKey: [...KEY, "pinned"], queryFn: ops.pinnedCommands });
  const [msg, setMsg] = useState<string | null>(null);
  const run = useMutation({
    mutationFn: (c: SavedCommand) => ops.runCommand(c.id, c.confirm),
    onSuccess: (_, c) => setMsg(`${c.name}: ran`),
    onError: (e, c) => setMsg(`${c.name}: ${reauthText(e)}`),
  });
  return (
    <Card title="Pinned commands">
      {q.isError ? <LoadError what="Your pinned commands" error={q.error} retry={() => void q.refetch()} />
        : q.isLoading ? <Loading />
        : !q.data?.length ? <Empty title="Nothing pinned yet">Pin a saved command to run it from here with one click. <Link to="/saved-commands">Saved commands</Link></Empty>
        : <div className="wa-row">{q.data.map((c) => <Button key={c.id} disabled={!c.runnable} busy={run.isPending && run.variables?.id === c.id} onClick={() => {
          if (c.confirm && !window.confirm(`Run "${c.name}"?\n\n${c.line}`)) return;
          run.mutate(c);
        }}>{c.name}</Button>)}</div>}
      <div aria-live="polite">{msg && <p className="k-muted wa-p">{msg}</p>}</div>
    </Card>
  );
}
