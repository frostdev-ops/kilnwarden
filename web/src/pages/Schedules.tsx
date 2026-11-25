// Schedules (DESIGN §5): restarts with a countdown, timed backups, announcements and saved commands, on a clock. A
// schedule runs as the account that last saved or switched it on, checked again at every run; restarts and commands
// need a recent proof to save. Missed runs (the server was down) are skipped or caught up once, as each schedule says.
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useSession } from "../app/context";
import { ops } from "../app/ops";
import { isApiError } from "../api/http";
import {
  WEEKDAYS, describeWhen, parseTimes, type Schedule, type ScheduleInput, type ScheduleKind, type ScheduleRun, type Weekday,
} from "../api/ops";
import { can } from "../state/session";
import { Banner, Button, Card, Check, Empty, Field, LoadError, Loading, Modal, Page, Pill, TextField, explain, when, type Tone } from "../ui/kit";

const KEY = ["schedules"];
const KINDS: { id: ScheduleKind; label: string }[] = [
  { id: "announce", label: "Announcement" }, { id: "backup", label: "Backup" }, { id: "restart", label: "Restart" }, { id: "command", label: "Saved command" },
];
const DAY_NAMES: Record<Weekday, string> = { mon: "Mon", tue: "Tue", wed: "Wed", thu: "Thu", fri: "Fri", sat: "Sat", sun: "Sun" };
const OUTCOME: Record<ScheduleRun["outcome"], Tone> = { ok: "ok", failed: "err", refused: "err", skipped: "idle", missed: "warn" };

function problem(e: unknown): string {
  if (isApiError(e) && e.code === "reauth_required") return "Confirm your identity in the dialog, then save or run it again. Nothing was changed.";
  return explain(e);
}

export function SchedulesPage() {
  const me = useSession((s) => s.me)!;
  const qc = useQueryClient();
  const q = useQuery({ queryKey: KEY, queryFn: ops.schedules, refetchInterval: 15_000 });
  const [editing, setEditing] = useState<Schedule | "new" | null>(null);
  const [history, setHistory] = useState<Schedule | null>(null);
  const [msg, setMsg] = useState<{ tone: Tone; text: string } | null>(null);
  const refresh = () => qc.invalidateQueries({ queryKey: KEY });
  const act = useMutation({
    mutationFn: (f: () => Promise<unknown>) => f(),
    onSuccess: () => setMsg(null),
    onError: (e) => setMsg({ tone: "err", text: problem(e) }),
    onSettled: refresh,
  });
  const manage = can(me, "schedules.manage");
  const restart = q.data?.restart;
  return (
    <Page title="Schedules" wide sub={`Restarts, backups, announcements and saved commands on a clock. Times show in your browser's time; each schedule runs in its own zone${q.data ? ` (the server's is ${q.data.localZone})` : ""}.`}
      actions={manage && <Button kind="primary" onClick={() => setEditing("new")}>New schedule</Button>}>
      {q.isError && <LoadError what="The schedules" error={q.error} retry={() => void q.refetch()} />}
      {q.isLoading && <Loading />}
      <div aria-live="polite">{msg && <Banner tone={msg.tone} title={msg.text} actions={<Button size="s" kind="ghost" onClick={() => setMsg(null)}>Close</Button>} />}</div>
      {restart && restart.supported !== "supported" && (
        <Banner tone="warn" title="Restart: unknown">
          Nothing known brings this server back after a stop (it is not run by kilnctl's service). A restart schedule only runs when it allows a plain stop.
        </Banner>
      )}
      {q.data && !q.data.schedules.length && (
        <Card>
          <Empty title="No schedules yet">
            Restart every night with a countdown in chat, back up the world every few hours, or announce something at set times.
          </Empty>
        </Card>
      )}
      {!!q.data?.schedules.length && <div className="wa-table-wrap">
        <table className="wa-table">
          <thead><tr><th>Name</th><th>What</th><th>When</th><th>Next</th><th>Last</th><th><span className="k-sr-only">Actions</span></th></tr></thead>
          <tbody>
            {(q.data?.schedules ?? []).map((s) => (
              <tr key={s.id}>
                <td data-label="Name">{s.name} {!s.enabled && <Pill tone="idle">off</Pill>}<div className="k-muted">as {s.actor.name}</div></td>
                <td data-label="What">{what(s)}</td>
                <td data-label="When">{describeWhen(s.when, s.timeZone)}{s.skipWhenEmpty && <div className="k-muted">skipped when nobody is online</div>}</td>
                <td data-label="Next" className="wa-nowrap">
                  {s.nextRun ? when(s.nextRun) : "none"}
                  {s.skipSlot && <div><Pill tone="warn">next one skipped</Pill></div>}
                  {s.kind === "restart" && s.nextFire && <div className="k-muted">countdown from {when(s.nextFire)}</div>}
                </td>
                <td data-label="Last">
                  {s.lastRun ? <><Pill tone={OUTCOME[s.lastRun.outcome]} title={s.lastRun.message ?? undefined}>{s.lastRun.outcome}</Pill> <span className="k-muted">{when(s.lastRun.at)}</span>
                    {s.lastRun.outcome !== "ok" && s.lastRun.message && <div className="k-muted wa-break">{s.lastRun.message}</div>}</> : <span className="k-muted">never</span>}
                </td>
                <td className="wa-actions">
                  <Button size="s" kind="ghost" aria-label={`History of ${s.name}`} onClick={() => setHistory(s)}>History</Button>
                  {manage && <>
                    <Button size="s" kind="ghost" aria-label={`Edit ${s.name}`} onClick={() => setEditing(s)}>Edit</Button>
                    <Button size="s" kind="ghost" onClick={() => act.mutate(() => ops.enableSchedule(s.id, !s.enabled))}>{s.enabled ? "Switch off" : "Switch on"}</Button>
                    {s.enabled && s.nextRun && <Button size="s" kind="ghost" onClick={() => act.mutate(() => ops.skipNext(s.id, !s.skipSlot))}>{s.skipSlot ? "Don't skip" : "Skip next"}</Button>}
                    <Button size="s" kind="ghost" aria-label={`Run ${s.name} now`} onClick={() => act.mutate(() => ops.runSchedule(s.id))}>Run now</Button>
                    <Button size="s" kind="ghost" aria-label={`Remove ${s.name}`} onClick={() => window.confirm(`Remove "${s.name}"?`) && act.mutate(() => ops.deleteSchedule(s.id))}>Remove</Button>
                  </>}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>}
      {manage && <div className="wa-row"><Button kind="ghost" onClick={() => act.mutate(() => ops.cancelRestart())}>Cancel a restart countdown</Button></div>}
      {editing && <ScheduleEditor initial={editing === "new" ? null : editing} onClose={() => setEditing(null)} onSaved={() => { setEditing(null); void refresh(); }} />}
      {history && <History s={history} onClose={() => setHistory(null)} />}
    </Page>
  );
}

function what(s: Schedule): string {
  const p = s.params as Record<string, unknown>;
  switch (s.kind) {
    case "restart": return `Restart (${Math.round(Number(p.countdownSeconds ?? 0) / 60)} min countdown)`;
    case "backup": return p.name ? `Backup "${p.name}-…"` : "Backup";
    case "announce": return `Announce: ${p.text}`;
    case "command": return s.savedCommand ? `/${s.savedCommand.command} (${s.savedCommand.name})${s.savedCommand.changed ? " - changed, save again" : ""}` : "a removed saved command";
  }
}

function History({ s, onClose }: { s: Schedule; onClose: () => void }) {
  const q = useQuery({ queryKey: [...KEY, s.id, "runs"], queryFn: () => ops.scheduleRuns(s.id) });
  return (
    <Modal title={`History: ${s.name}`} onClose={onClose} wide>
      {q.isError && <LoadError what="The history" error={q.error} retry={() => void q.refetch()} />}
      {q.isLoading && <Loading />}
      {!!q.data?.length && <div className="wa-table-wrap"><table className="wa-table">
        <thead><tr><th>When</th><th>For</th><th>Result</th><th>By</th></tr></thead>
        <tbody>{(q.data ?? []).map((r, i) => (
          <tr key={i}>
            <td data-label="When">{when(r.at)}</td>
            <td data-label="For">{r.slot ? when(r.slot) : "by hand"}</td>
            <td data-label="Result"><Pill tone={OUTCOME[r.outcome]}>{r.outcome}</Pill> {r.code && <span className="k-mono">{r.code}</span>}{r.message && <div className="k-muted wa-break">{r.message}</div>}</td>
            <td data-label="By">{r.by ?? ""}</td>
          </tr>
        ))}</tbody>
      </table></div>}
      {q.data && !q.data.length && <Empty title="Nothing has run yet">Runs show here, by the clock or by hand.</Empty>}
    </Modal>
  );
}

function toLocalInput(iso: string | null | undefined): string {
  const d = iso ? new Date(iso) : new Date(Date.now() + 3600_000);
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

function ScheduleEditor({ initial, onClose, onSaved }: { initial: Schedule | null; onClose: () => void; onSaved: () => void }) {
  const p = (initial?.params ?? {}) as Record<string, unknown>;
  const [name, setName] = useState(initial?.name ?? "");
  const [kind, setKind] = useState<ScheduleKind>(initial?.kind ?? "announce");
  const [type, setType] = useState(initial?.when.type ?? "daily");
  const [times, setTimes] = useState(initial?.when.type === "daily" ? initial.when.times.join(", ") : "06:00");
  const [days, setDays] = useState<Weekday[]>(initial?.when.type === "daily" ? initial.when.days ?? [] : []);
  const [every, setEvery] = useState(String(initial?.when.type === "interval" ? initial.when.everyMinutes : 360));
  const [at, setAt] = useState(toLocalInput(initial?.when.type === "once" ? initial.when.at : null));
  const [zone, setZone] = useState(initial?.timeZone ?? "UTC");
  const [skipEmpty, setSkipEmpty] = useState(initial?.skipWhenEmpty ?? false);
  const [missed, setMissed] = useState<"skip" | "catchUp">(initial?.missed ?? "skip");
  const [catchUp, setCatchUp] = useState(String(initial?.catchUpMinutes ?? 60));
  const [countdown, setCountdown] = useState(String(Math.round(Number(p.countdownSeconds ?? 300) / 60)));
  const [message, setMessage] = useState(String(p.message ?? ""));
  const [stopOnly, setStopOnly] = useState(Boolean(p.allowStopOnly));
  const [text, setText] = useState(String(p.text ?? ""));
  const [prefix, setPrefix] = useState(String(p.name ?? ""));
  const [saved, setSaved] = useState(String(p.savedCommandId ?? ""));
  const commands = useQuery({ queryKey: ["saved-commands"], queryFn: ops.savedCommands, enabled: kind === "command" });
  const body = (): ScheduleInput => ({
    name, enabled: initial?.enabled ?? true, kind, timeZone: zone, skipWhenEmpty: skipEmpty, missed: kind === "restart" ? "skip" : missed,
    catchUpMinutes: Number(catchUp) || 60,
    when: type === "daily" ? { type, times: parseTimes(times), days: days.length ? days : null }
      : type === "interval" ? { type, everyMinutes: Number(every) || 0, anchor: initial?.when.type === "interval" ? initial.when.anchor : null }
      : { type: "once", at: new Date(at).toISOString() },
    params: kind === "restart" ? { countdownSeconds: (Number(countdown) || 0) * 60, message: message || undefined, allowStopOnly: stopOnly }
      : kind === "backup" ? { name: prefix || undefined }
      : kind === "announce" ? { text }
      : { savedCommandId: saved },
  });
  const save = useMutation({ mutationFn: () => (initial ? ops.updateSchedule(initial.id, body()) : ops.createSchedule(body())), onSuccess: onSaved });
  const sensitive = kind === "restart" || kind === "command";
  return (
    <Modal title={initial ? "Change schedule" : "New schedule"} onClose={onClose} wide footer={<div className="wa-row">
      <Button kind="primary" busy={save.isPending} disabled={!name.trim()} onClick={() => save.mutate()}>Save</Button>
      <Button kind="ghost" onClick={onClose}>Cancel</Button>
    </div>}>
      <div className="wa-form">
        <TextField label="Name" value={name} onChange={setName} maxLength={64} autoFocus />
        <Field label="What" htmlFor="wa-s-kind">
          <select id="wa-s-kind" className="k-select" value={kind} onChange={(e) => setKind(e.target.value as ScheduleKind)} disabled={!!initial}>
            {KINDS.map((k) => <option key={k.id} value={k.id}>{k.label}</option>)}
          </select>
        </Field>
        {kind === "announce" && <TextField label="Message (one line, shown in chat)" value={text} onChange={setText} maxLength={200} />}
        {kind === "backup" && <TextField label="Name prefix (optional)" value={prefix} onChange={setPrefix} maxLength={40} hint="A-Z a-z 0-9 . _ -; the date is added." />}
        {kind === "restart" && <>
          <TextField label="Countdown (minutes before the time below)" value={countdown} onChange={setCountdown} inputMode="numeric" hint="0 to 60. Chat is told at the start, then at 10, 5, 2, 1 min, 30 and 10 s." />
          <TextField label="Message added to the notices (optional)" value={message} onChange={setMessage} maxLength={200} />
          <Check label="Run even when restart support is unknown" hint="(the server only stops)" checked={stopOnly} onChange={setStopOnly} />
        </>}
        {kind === "command" && (
          <Field label="Saved command" htmlFor="wa-s-cmd" hint="The schedule keeps this version of it; if the command is changed later, save the schedule again.">
            <select id="wa-s-cmd" className="k-select" value={saved} onChange={(e) => setSaved(e.target.value)}>
              <option value="">Choose…</option>
              {(commands.data ?? []).filter((c) => c.runnable).map((c) => <option key={c.id} value={c.id}>{c.name} ({c.line})</option>)}
            </select>
          </Field>
        )}
        <Field label="When" htmlFor="wa-s-type">
          <select id="wa-s-type" className="k-select" value={type} onChange={(e) => setType(e.target.value as typeof type)}>
            <option value="daily">At set times</option><option value="interval">Every N minutes</option><option value="once">Once</option>
          </select>
        </Field>
        {type === "daily" && <>
          <TextField label="Times (HH:mm, comma separated)" value={times} onChange={setTimes} mono hint={parseTimes(times).join(", ") || "no valid time yet"} />
          <fieldset className="wa-plain">
            <legend className="k-label">Days</legend>
            <div className="wa-row">
              {WEEKDAYS.map((d) => <Check key={d} label={DAY_NAMES[d]} checked={days.includes(d)} onChange={(on) => setDays(on ? [...days, d] : days.filter((x) => x !== d))} />)}
            </div>
            <span className="k-hint">None ticked: every day.</span>
          </fieldset>
          <Field label="Time zone" htmlFor="wa-s-zone" hint="UTC, local (the server's), or a zone id the server knows (for example Europe/Berlin).">
            <input id="wa-s-zone" className="k-input" list="wa-s-zones" value={zone} onChange={(e) => setZone(e.target.value)} />
            <datalist id="wa-s-zones"><option value="UTC" /><option value="local" /></datalist>
          </Field>
        </>}
        {type === "interval" && <TextField label="Every (minutes)" value={every} onChange={setEvery} inputMode="numeric" hint={kind === "restart" ? "60 to 10080" : "5 to 10080"} />}
        {type === "once" && <Field label="At (your browser's time)" htmlFor="wa-s-at"><input id="wa-s-at" className="k-input" type="datetime-local" value={at} onChange={(e) => setAt(e.target.value)} /></Field>}
        <Check label="Skip when nobody is online" checked={skipEmpty} onChange={setSkipEmpty} />
        {kind !== "restart" && (
          <Field label="If a run was missed (server or panel down)" htmlFor="wa-s-missed">
            <select id="wa-s-missed" className="k-select" value={missed} onChange={(e) => setMissed(e.target.value as "skip" | "catchUp")}>
              <option value="skip">Skip it</option><option value="catchUp">Run it once when back, if it is not too late</option>
            </select>
          </Field>
        )}
        {kind !== "restart" && missed === "catchUp" && <TextField label="Too late after (minutes)" value={catchUp} onChange={setCatchUp} inputMode="numeric" />}
        {sensitive && <p className="k-muted">Saving a {kind === "restart" ? "restart" : "command"} schedule asks for your password or code if you have not confirmed it in the last ten minutes. It then runs as you, with your permissions as they are at each run.</p>}
        <div aria-live="polite">{save.isError && <Banner tone="err" title={problem(save.error)} />}</div>
      </div>
    </Modal>
  );
}
