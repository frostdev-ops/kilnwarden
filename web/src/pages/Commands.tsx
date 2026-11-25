// Commands (DESIGN §5a): every Kiln command on the server, Kiln's and every mod's, as a form built from
// commands_describe (grouped by mod, searchable; the gateway lists only what the user's role may run), and a free-text
// console with completion (Tab) and history (Up, Down). A line is checked with command_complete, then run with
// command_run, as the signed-in user: the gateway stamps source "web:<name>" and the role's level on every one (the
// page never sends them). "Run as" a player needs commands.run_as.
import { useAdminCaller, useAdminQuery, useAdminState, type CommandInfo, type CommandNode, type CommandRunResult, type CompleteResult, type ItemHit } from "@kiln/admin-ui";
import { useEffect, useId, useMemo, useRef, useState, type FormEvent, type KeyboardEvent } from "react";
import { useSession } from "../app/context";
import { SERVER_ID } from "../api/kapSocket";
import { buildLine, defaultText, isEmpty, type ArgValue, type PositionValue } from "../commands/line";
import { formsOf, groupCatalog, tabsOf, usageOf, type FormArg } from "../commands/tree";
import { loadHistory, saveHistory } from "../commands/history";
import { DURATION_UNITS, WORLD, durationText, groundDistance, planeToWorld, splitDuration, timeText, worldToPlane, type DurationUnit } from "../commands/units";
import { can } from "../state/session";
import { Banner, Button, Card, LoadError, Loading, Page, Pill, Tabs, errorText } from "../ui/kit";
import { SaveCommandDialog } from "./SavedCommands";

interface OutRow {
  text: string;
  level: "in" | "out" | "error" | "meta";
}

export function CommandsPage() {
  const [tab, setTab] = useState<"forms" | "console">("forms");
  const hub = useAdminState(SERVER_ID);
  const [rows, setRows] = useState<OutRow[]>([]);
  const add = (r: OutRow[]) => setRows((x) => [...x, ...r].slice(-500));
  return (
    <Page title="Commands" wide sub="Every command on the server, as a form or typed by hand. They run as you, at your role's level.">
      <Tabs label="Commands" value={tab} onChange={setTab} tabs={[{ id: "forms", label: "Forms" }, { id: "console", label: "Console" }]} />
      {!hub.ready && (
        <Banner tone={hub.status === "error" ? "err" : "info"} title={hub.status === "error" ? "Not connected to the server" : "Connecting to the server…"}>
          {hub.error?.message}
        </Banner>
      )}
      {tab === "forms" ? <Forms onRows={add} /> : <Console onRows={add} />}
      <Output rows={rows} clear={() => setRows([])} />
    </Page>
  );
}

function useRun(onRows: (r: OutRow[]) => void) {
  const call = useAdminCaller(SERVER_ID);
  const hub = useAdminState(SERVER_ID);
  const [busy, setBusy] = useState(false);
  const run = async (line: string, steamId: string, who: string) => {
    setBusy(true);
    onRows([{ text: who ? `${who}> ${line}` : `> ${line}`, level: "in" }]);
    try {
      const extra: Record<string, string> = steamId ? { steamId } : {};
      // the check: Kiln.Admin parses the line as far as it can (and the gateway applies the role's command rules)
      if (hub.ops.has("command_complete")) await call<CompleteResult>("command_complete", { text: line, ...extra });
      const r = await call<CommandRunResult>("command_run", { line, ...extra });
      const out: OutRow[] = (r.output ?? []).map((text) => ({ text, level: "out" as const }));
      if (!r.ok) out.push({ text: r.denied ? `denied: ${r.error || "no permission"}` : r.found === false ? r.error || "unknown command" : r.error || (typeof r.parseError === "string" ? r.parseError : "failed"), level: "error" });
      onRows(out.length ? out : [{ text: "ok", level: "meta" }]);
      return r.ok;
    } catch (e) {
      onRows([{ text: errorText(e), level: "error" }]);
      return false;
    } finally {
      setBusy(false);
    }
  };
  return { run, busy, ready: hub.ready && hub.ops.has("command_run") };
}

/** "Run as": the web user (default), or a player in game (with commands.run_as). */
function RunAs({ value, onChange }: { value: string; onChange: (v: string) => void }) {
  const me = useSession((s) => s.me)!;
  const hub = useAdminState(SERVER_ID);
  const id = useId();
  const players = (hub.state?.players ?? []).filter((p) => p.steamId);
  if (!can(me, "commands.run_as") || !players.length) return null;
  return (
    <div className="k-field wa-run-as">
      <label className="k-label" htmlFor={id}>Run as</label>
      <select id={id} className="k-select" value={value} onChange={(e) => onChange(e.target.value)}>
        <option value="">{me.name} (you, up to {me.commandLevel})</option>
        {players.map((p) => <option key={p.steamId} value={p.steamId!}>{p.name || p.steamId} (their permissions)</option>)}
      </select>
    </div>
  );
}

function Forms({ onRows }: { onRows: (r: OutRow[]) => void }) {
  const list = useAdminQuery<CommandInfo[]>("commands_describe", {}, { serverId: SERVER_ID, staleTime: 60_000 });
  const [query, setQuery] = useState("");
  const [picked, setPicked] = useState<string | null>(null);
  const groups = useMemo(() => groupCatalog(Array.isArray(list.data) ? list.data : [], query), [list.data, query]);
  const cmd = (Array.isArray(list.data) ? list.data : []).find((c) => c.name === picked) ?? null;
  return (
    <div className="wa-commands">
      <Card title="Catalog" className="wa-catalog">
        <input className="k-input" type="search" placeholder="Search commands" aria-label="Search commands" value={query} onChange={(e) => setQuery(e.target.value)} />
        {list.isError && <LoadError what="The command list" error={list.error} retry={() => void list.refetch()} />}
        {list.isLoading && <Loading label="Loading the commands…" />}
        <div className="wa-catalog-list">
          {groups.map((g) => (
            <div key={g.owner} className="wa-catalog-group">
              <div className="wa-nav-title k-sc">{g.owner}</div>
              {g.commands.map((c) => (
                <button key={c.name} type="button" className={"wa-catalog-item" + (c.name === picked ? " active" : "")} onClick={() => setPicked(c.name)} aria-pressed={c.name === picked}>
                  <span className="k-mono">/{c.name}</span>
                  <span className="k-muted wa-ellipsis">{c.description}</span>
                </button>
              ))}
            </div>
          ))}
          {!list.isLoading && !groups.length && <p className="k-muted">{query ? "No command matches." : "No commands you may run."}</p>}
        </div>
      </Card>
      {cmd ? <CommandForm key={cmd.name} cmd={cmd} onRows={onRows} /> : <Card title="Pick a command"><p className="k-muted">Each command becomes a form from its typed arguments.</p></Card>}
    </div>
  );
}

function CommandForm({ cmd, onRows }: { cmd: CommandInfo; onRows: (r: OutRow[]) => void }) {
  const tabs = useMemo(() => tabsOf(formsOf(cmd)), [cmd]);
  const [tabKey, setTabKey] = useState(tabs[0]?.key ?? "");
  const [variant, setVariant] = useState(0);
  const [values, setValues] = useState<ArgValue[]>([]);
  const [free, setFree] = useState("");
  const [as, setAs] = useState("");
  const [saving, setSaving] = useState(false);
  const me = useSession((s) => s.me);
  const { run, busy, ready } = useRun(onRows);
  const tab = tabs.find((t) => t.key === tabKey) ?? tabs[0];
  const form = tab?.forms[Math.min(variant, tab.forms.length - 1)] ?? null;
  const built = buildLine(cmd.name, form, values, free);
  useEffect(() => {
    setValues([]);
    setVariant(0);
  }, [tabKey]);
  useEffect(() => setValues([]), [variant]);
  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (built.problems.length) return;
    await run(built.line, as, as ? "player" : "");
  };
  return (
    <Card title={<span className="k-mono">/{cmd.name}</span>} sub={cmd.description} actions={<Pill tone={cmd.permission === "console" ? "err" : cmd.permission === "admin" ? "warn" : "idle"}>{cmd.permission ?? "?"}</Pill>}>
      <form className="wa-form" onSubmit={submit}>
        {tabs.length > 1 && (
          <Tabs label="Subcommands" value={tab.key} onChange={setTabKey} tabs={tabs.map((t) => ({ id: t.key, label: t.label }))} />
        )}
        {tab && tab.forms.length > 1 && (
          <div className="k-field">
            <label className="k-label" htmlFor="wa-variant">Form</label>
            <select id="wa-variant" className="k-select" value={variant} onChange={(e) => setVariant(Number(e.target.value))}>
              {tab.forms.map((f, i) => <option key={i} value={i}>{usageOf(f)}</option>)}
            </select>
          </div>
        )}
        {form?.description && tabs.length > 1 && <p className="k-muted">{form.description}</p>}
        {form ? (
          form.args.length ? form.args.map((a, i) => (
            <ArgInput key={`${tab.key}-${variant}-${i}`} arg={a} value={values[i]} onChange={(v) => setValues((x) => { const n = [...x]; n[i] = v; return n; })} />
          )) : <p className="k-muted">No arguments.</p>
        ) : (
          <div className="k-field">
            <label className="k-label" htmlFor="wa-free">Arguments</label>
            <input id="wa-free" className="k-input k-input-mono" value={free} onChange={(e) => setFree(e.target.value)} autoComplete="off" spellCheck={false} />
            <div className="k-hint">{(cmd.usageLines ?? [cmd.usage]).filter(Boolean).join(" · ") || "This command takes free text."}</div>
          </div>
        )}
        <RunAs value={as} onChange={setAs} />
        <div className="wa-preview" aria-live="polite">
          <span className="k-label">Line</span>
          <code className="k-mono" data-testid="command-line">{built.line}</code>
        </div>
        {built.problems.length > 0 && <ul className="wa-problems">{built.problems.map((p) => <li key={p} className="k-error-text">{p}</li>)}</ul>}
        <div className="wa-row">
          <Button kind="primary" type="submit" busy={busy} disabled={!ready || built.problems.length > 0}>Run</Button>
          <Button kind="ghost" onClick={() => void navigator.clipboard?.writeText(built.line)}>Copy line</Button>
          {can(me, "saved-commands.manage") && <Button kind="ghost" disabled={built.problems.length > 0 || !!as} title={as ? "a saved command runs as whoever runs it" : undefined} onClick={() => setSaving(true)}>Save…</Button>}
        </div>
      </form>
      {saving && <SaveCommandDialog initial={null} line={built.line} form={{ command: cmd.name, tab: tab?.key, variant, values, free }} onClose={() => setSaving(false)} onSaved={() => setSaving(false)} />}
    </Card>
  );
}

const NUMERIC = new Set(["int", "integer", "long", "double", "float", "number"]);

function ArgInput({ arg, value, onChange }: { arg: FormArg; value: ArgValue; onChange: (v: ArgValue) => void }) {
  const id = useId();
  const n = arg.node;
  const type = (n.type ?? "string").toLowerCase();
  const label = `${arg.name}${arg.optional ? " (optional)" : ""}`;
  const hint = [n.describe, n.description, typeof n.min === "number" || typeof n.max === "number" ? `${n.min ?? "…"} to ${n.max ?? "…"}${n.unit === "s" ? " s" : n.unit === "h" ? " h" : ""}` : null].filter(Boolean).join(" · ");
  const placeholder = defaultText(n);
  const str = typeof value === "string" ? value : "";
  let control;
  if (type === "position") {
    const p: PositionValue = typeof value === "object" ? value : { x: "", y: "", z: "", rel: [false, false, false] };
    control = <PositionInput id={id} node={n} value={p} onChange={onChange} />;
  } else if (type === "bool" || type === "boolean") {
    control = (
      <select id={id} className="k-select" value={str} onChange={(e) => onChange(e.target.value)}>
        <option value="">{arg.optional ? `(default${placeholder ? `: ${placeholder}` : ""})` : "choose"}</option>
        <option value="on">on</option>
        <option value="off">off</option>
      </select>
    );
  } else if ((type === "enum" || type === "choice") && n.options?.length) {
    control = (
      <select id={id} className="k-select" value={str} onChange={(e) => onChange(e.target.value)}>
        <option value="">{arg.optional ? `(default${placeholder ? `: ${placeholder}` : ""})` : "choose"}</option>
        {n.options.map((o) => <option key={o} value={o}>{o}</option>)}
      </select>
    );
  } else if (type === "duration") {
    control = <DurationInput id={id} value={str} onChange={onChange} placeholder={placeholder} />;
  } else if (type === "time") {
    control = <TimeInput id={id} value={str} onChange={onChange} />;
  } else if (NUMERIC.has(type)) {
    const ranged = NUMERIC.has(type) && typeof n.min === "number" && typeof n.max === "number" && n.max - n.min <= 10_000;
    control = (
      <div className="wa-row wa-nowrap-row">
        <input id={id} className="k-input k-num" inputMode={type === "int" || type === "integer" ? "numeric" : "decimal"} value={str} placeholder={placeholder} onChange={(e) => onChange(e.target.value)} />
        {ranged && <input type="range" className="k-range" aria-label={`${arg.name} slider`} min={n.min} max={n.max} step={type === "double" || type === "float" ? "any" : 1} value={str === "" ? String(n.default ?? n.min) : str} onChange={(e) => onChange(e.target.value)} />}
      </div>
    );
  } else if (type === "player" || type === "players" || type === "entity" || type === "entities") {
    control = <>
      <TargetInput id={id} node={n} value={str} onChange={onChange} players={type.startsWith("player") || type.startsWith("entit")} />
      {type.startsWith("entit") && <NearbyCreatures value={str} onPick={onChange} />}
    </>;
  } else if (type === "item") {
    control = <ItemInput id={id} value={str} onChange={onChange} placeholder={placeholder} />;
  } else if (type === "greedy" || n.greedy) {
    control = <textarea id={id} className="k-textarea" rows={2} value={str} placeholder={placeholder} onChange={(e) => onChange(e.target.value)} />;
  } else {
    control = <input id={id} className="k-input" value={str} placeholder={placeholder} autoComplete="off" spellCheck={false} onChange={(e) => onChange(e.target.value)} />;
  }
  return (
    <div className="k-field">
      <div className="k-field-head"><label className="k-label" htmlFor={id}>{label}</label> <span className="k-muted k-mono">{type}</span></div>
      {control}
      {hint && <div className="k-hint">{hint}</div>}
      {!arg.optional && isEmpty(value) && <span className="k-sr-only">required</span>}
    </div>
  );
}

function PositionInput({ id, node, value, onChange }: { id: string; node: CommandNode; value: PositionValue; onChange: (v: PositionValue) => void }) {
  const hub = useAdminState(SERVER_ID);
  const [map, setMap] = useState(false);
  const players = (hub.state?.players ?? []).filter((p) => p.pos);
  const rel = value.rel ?? [false, false, false];
  const set = (k: 0 | 1 | 2, v: string) => onChange({ ...value, [["x", "y", "z"][k]]: v } as PositionValue);
  const setRel = (k: number, on: boolean) => onChange({ ...value, rel: rel.map((r, i) => (i === k ? on : r)) as [boolean, boolean, boolean] });
  return (
    <div className="wa-stack">
      <div className="wa-row wa-nowrap-row">
        {(["x", "y", "z"] as const).map((axis, i) => (
          <span key={axis} className="wa-axis">
            <input id={i === 0 ? id : undefined} className="k-input k-num" aria-label={axis} placeholder={axis} inputMode="decimal" value={value[axis]} onChange={(e) => set(i as 0 | 1 | 2, e.target.value)} />
            {node.relative !== false && (
              <label className="wa-check" title="relative to the player running it (~)">
                <input type="checkbox" className="k-check" checked={rel[i]} onChange={(e) => setRel(i, e.target.checked)} />
                <span>~</span>
              </label>
            )}
          </span>
        ))}
      </div>
      <div className="wa-row">
        {players.length > 0 && (
          <select className="k-select wa-grow" aria-label="At a player" value="" onChange={(e) => {
            const p = players.find((x) => String(x.entity) === e.target.value);
            if (p?.pos) onChange({ x: p.pos.x.toFixed(1), y: p.pos.y.toFixed(1), z: p.pos.z.toFixed(1), rel: [false, false, false] });
          }}>
            <option value="">At a player…</option>
            {players.map((p) => <option key={p.entity} value={p.entity}>{p.name}</option>)}
          </select>
        )}
        <Button size="s" kind="ghost" aria-expanded={map} onClick={() => setMap((m) => !m)}>{map ? "Hide the map" : "Pick on the map"}</Button>
      </div>
      {map && <WorldPlane value={value} players={players.map((p) => ({ name: p.name, pos: p.pos! }))} onPick={(x, z, y) => onChange({
        x: String(x), y: y !== undefined ? y.toFixed(1) : value.y, z: String(z), rel: [false, rel[1] && y === undefined, false],
      })} />}
    </div>
  );
}

/** The world as a plain square (no game art): a click or tap sets x and z; a player's dot sets all three. The number
 * fields stay the keyboard's way in. */
function WorldPlane({ value, players, onPick }: { value: PositionValue; players: { name: string; pos: { x: number; y: number; z: number } }[]; onPick: (x: number, z: number, y?: number) => void }) {
  const box = useRef<HTMLDivElement>(null);
  const x = Number(value.x), z = Number(value.z);
  const marked = value.x.trim() !== "" && value.z.trim() !== "" && Number.isFinite(x) && Number.isFinite(z) && !(value.rel ?? []).some(Boolean);
  const pick = (clientX: number, clientY: number) => {
    const r = box.current?.getBoundingClientRect();
    if (!r) return;
    const w = planeToWorld(clientX - r.left, clientY - r.top, r.width, r.height);
    // a click on (or within 2% of) a player's dot takes that player's position, height too
    const near = players.find((p) => groundDistance(p.pos, w) < WORLD * 0.02);
    if (near) onPick(Math.round(near.pos.x), Math.round(near.pos.z), near.pos.y);
    else onPick(w.x, w.z);
  };
  return (
    <div className="wa-plane-wrap">
      <div ref={box} className="wa-plane" aria-hidden onPointerDown={(e) => { e.preventDefault(); pick(e.clientX, e.clientY); }}>
        <svg viewBox="0 0 100 100" preserveAspectRatio="none" className="wa-plane-svg">
          {[10, 20, 30, 40, 50, 60, 70, 80, 90].map((g) => <g key={g}><line x1={g} x2={g} y1={0} y2={100} /><line y1={g} y2={g} x1={0} x2={100} /></g>)}
          {players.map((p) => {
            const at = worldToPlane(p.pos.x, p.pos.z, 100, 100);
            return <circle key={p.name} cx={at.px} cy={at.py} r={1.6} className="wa-plane-player" />;
          })}
          {marked && (() => { const at = worldToPlane(x, z, 100, 100); return <g className="wa-plane-mark"><line x1={at.px - 3} x2={at.px + 3} y1={at.py} y2={at.py} /><line x1={at.px} x2={at.px} y1={at.py - 3} y2={at.py + 3} /></g>; })()}
        </svg>
        {players.map((p) => {
          const at = worldToPlane(p.pos.x, p.pos.z, 100, 100);
          return <span key={p.name} className="wa-plane-label" ref={(el) => { if (el) { el.style.left = `${at.px}%`; el.style.top = `${at.py}%`; } }}>{p.name}</span>;
        })}
      </div>
      <p className="k-hint wa-p">Click or tap to set x and z ({WORLD} m square, north up); a player's dot takes their position. Height stays as typed.</p>
    </div>
  );
}

/** A duration as a number and a unit ("90s", "5m", "1h"). */
function DurationInput({ id, value, onChange, placeholder }: { id: string; value: string; onChange: (v: string) => void; placeholder: string }) {
  const parsed = splitDuration(value);
  const def = splitDuration(placeholder);
  const [unit, setUnit] = useState<DurationUnit>(parsed?.unit ?? def?.unit ?? "s");
  const [amount, setAmount] = useState(parsed?.amount ?? "");
  return (
    <div className="wa-row wa-nowrap-row">
      <input id={id} className="k-input k-num" inputMode="decimal" value={amount} placeholder={def?.amount ?? ""}
        onChange={(e) => { setAmount(e.target.value); onChange(durationText(e.target.value, unit)); }} />
      <select className="k-select wa-unit" aria-label="Unit" value={unit} onChange={(e) => { const u = e.target.value as DurationUnit; setUnit(u); onChange(durationText(amount, u)); }}>
        {DURATION_UNITS.map((u) => <option key={u.value} value={u.value}>{u.label}</option>)}
      </select>
    </div>
  );
}

/** A time of day from an hour:minute input ("07:30"), with noon and dusk as Kiln names them. */
function TimeInput({ id, value, onChange }: { id: string; value: string; onChange: (v: string) => void }) {
  const named = value !== "" && !timeText(value);
  return (
    <div className="wa-row">
      <input id={id} className="k-input wa-time" type="time" step={60} value={named ? "" : timeText(value)} onChange={(e) => onChange(timeText(e.target.value))} />
      <div className="k-chip-row" role="group" aria-label="Named times">
        {["noon", "dusk"].map((t) => <button key={t} type="button" className={"k-chip" + (value === t ? " on" : "")} aria-pressed={value === t} onClick={() => onChange(value === t ? "" : t)}>{t}</button>)}
      </div>
    </div>
  );
}

interface NearCreature { entity: number; templateName?: string; name?: string; template?: string; level?: number; dead?: boolean; pos?: { x: number; y: number; z: number } }

/** Creatures within 50 m of an online player (Kiln.Admin's creature_near), to pick an entity id from. */
function NearbyCreatures({ value, onPick }: { value: string; onPick: (v: string) => void }) {
  const hub = useAdminState(SERVER_ID);
  const players = (hub.state?.players ?? []).filter((p) => p.pos);
  const [who, setWho] = useState("");
  const player = players.find((p) => String(p.entity) === who);
  const at = player?.pos ? { x: player.pos.x, y: player.pos.y, z: player.pos.z, radius: 50 } : null;
  const near = useAdminQuery<NearCreature[]>("creature_near", at ?? {}, { serverId: SERVER_ID, enabled: !!at && hub.ops.has("creature_near"), staleTime: 5_000 });
  if (!hub.ops.has("creature_near") || !players.length) return null;
  const list = Array.isArray(near.data) ? near.data.filter((c) => !c.dead) : [];
  return (
    <div className="wa-stack wa-gap">
      <select className="k-select" aria-label="Creatures near a player" value={who} onChange={(e) => setWho(e.target.value)}>
        <option value="">Creatures near a player…</option>
        {players.map((p) => <option key={p.entity} value={p.entity}>{p.name}</option>)}
      </select>
      {at && (near.isLoading ? <Loading label="Looking around…" />
        : near.isError ? <LoadError what="The creatures nearby" error={near.error} retry={() => void near.refetch()} />
        : !list.length ? <p className="k-muted wa-p">No creatures within 50 m of {player!.name}.</p>
        : (
          <ul className="wa-near" aria-label={`Creatures within 50 m of ${player!.name}`}>
            {list.slice(0, 20).map((c) => {
              const d = c.pos && player?.pos ? Math.round(Math.hypot(c.pos.x - player.pos.x, c.pos.y - player.pos.y, c.pos.z - player.pos.z)) : null;
              return (
                <li key={c.entity}>
                  <button type="button" className={"wa-catalog-item" + (value === String(c.entity) ? " active" : "")} aria-pressed={value === String(c.entity)} onClick={() => onPick(String(c.entity))}>
                    <span className="k-mono">{c.entity}</span>
                    <span className="wa-ellipsis">{c.templateName ?? c.name ?? c.template ?? "creature"}{c.level !== undefined ? ` · level ${c.level}` : ""}{d !== null ? ` · ${d} m` : ""}</span>
                  </button>
                </li>
              );
            })}
          </ul>
        ))}
    </div>
  );
}

function TargetInput({ id, node, value, onChange, players }: { id: string; node: CommandNode; value: string; onChange: (v: string) => void; players: boolean }) {
  const hub = useAdminState(SERVER_ID);
  const online = players ? hub.state?.players ?? [] : [];
  const selectors = node.selectors ?? (node.type === "players" ? ["@a", "@p", "@r", "@s"] : node.type === "player" ? ["@p", "@r", "@s"] : ["@e", "@p", "@s"]);
  const options = [...online.map((p) => ({ v: /\s/.test(p.name) ? String(p.entity) : p.name, l: p.name })), ...selectors.map((s) => ({ v: s, l: s }))];
  const listId = `${id}-list`;
  return (
    <>
      <input id={id} className="k-input" list={listId} value={value} autoComplete="off" spellCheck={false} placeholder={online.length ? "a player, or a selector" : "a name, an entity id or a selector"} onChange={(e) => onChange(e.target.value)} />
      <datalist id={listId}>{options.map((o) => <option key={o.v} value={o.v}>{o.l}</option>)}</datalist>
      {online.length > 0 && (
        <div className="k-chip-row wa-chips">
          {online.slice(0, 12).map((p) => (
            <button key={p.entity} type="button" className={"k-chip" + (value === p.name || value === String(p.entity) ? " on" : "")} onClick={() => onChange(/\s/.test(p.name) ? String(p.entity) : p.name)}>{p.name}</button>
          ))}
          {selectors.map((s) => <button key={s} type="button" className={"k-chip" + (value === s ? " on" : "")} onClick={() => onChange(s)}>{s}</button>)}
        </div>
      )}
    </>
  );
}

function ItemInput({ id, value, onChange, placeholder }: { id: string; value: string; onChange: (v: string) => void; placeholder: string }) {
  const [q, setQ] = useState(value);
  useEffect(() => {
    const t = setTimeout(() => setQ(value), 250);
    return () => clearTimeout(t);
  }, [value]);
  const hits = useAdminQuery<ItemHit[]>("item_search", { q }, { serverId: SERVER_ID, enabled: q.trim().length >= 2, staleTime: 60_000 });
  const listId = `${id}-items`;
  return (
    <>
      <input id={id} className="k-input" list={listId} value={value} placeholder={placeholder || "an item name or id"} autoComplete="off" onChange={(e) => onChange(e.target.value)} />
      <datalist id={listId}>{(Array.isArray(hits.data) ? hits.data : []).slice(0, 30).map((h) => <option key={h.id} value={h.name}>{h.debug ?? h.id}</option>)}</datalist>
    </>
  );
}

function Console({ onRows }: { onRows: (r: OutRow[]) => void }) {
  const call = useAdminCaller(SERVER_ID);
  const hub = useAdminState(SERVER_ID);
  const [text, setText] = useState("");
  const [as, setAs] = useState("");
  const [sugg, setSugg] = useState<string[]>([]);
  const [sel, setSel] = useState(0);
  const account = useSession((s) => s.me?.id ?? "");
  const history = useRef<string[]>(loadHistory(account));
  const at = useRef(-1);
  const { run, busy, ready } = useRun(onRows);

  const complete = async () => {
    if (!hub.ops.has("command_complete") || !text.trim()) return;
    const full = text.startsWith("/") ? text : "/" + text;
    try {
      const r = await call<CompleteResult>("command_complete", { text: full, ...(as ? { steamId: as } : {}) });
      const off = text.startsWith("/") ? 0 : 1;
      const start = Math.max(0, r.start - off);
      const list = (r.suggestions ?? []).filter((s) => s !== text.slice(start)).map((s) => text.slice(0, start) + s + (/^<.*>$/.test(s) ? "" : " "));
      if (list.length === 1) {
        setText(list[0]);
        setSugg([]);
      } else {
        setSugg(list.slice(0, 12));
        setSel(0);
      }
    } catch (e) {
      setSugg([]);
      onRows([{ text: errorText(e), level: "error" }]);
    }
  };
  const submit = async (e: FormEvent) => {
    e.preventDefault();
    const raw = text.trim();
    if (!raw || busy) return;
    history.current = [...history.current.filter((h) => h !== raw), raw].slice(-100);
    saveHistory(account, history.current);
    at.current = -1;
    setText("");
    setSugg([]);
    await run(raw.startsWith("/") ? raw : "/" + raw, as, as ? "player" : "");
  };
  const key = (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === "Tab") {
      e.preventDefault();
      if (sugg.length) {
        setText(sugg[sel]);
        setSugg([]);
      } else void complete();
      return;
    }
    if (e.key === "Escape" && sugg.length) {
      e.preventDefault();
      setSugg([]);
      return;
    }
    if (sugg.length && (e.key === "ArrowUp" || e.key === "ArrowDown")) {
      e.preventDefault();
      setSel((s) => (s + (e.key === "ArrowDown" ? 1 : -1) + sugg.length) % sugg.length);
      return;
    }
    if (sugg.length && e.key === "Enter") {
      e.preventDefault();
      setText(sugg[sel]);
      setSugg([]);
      return;
    }
    const h = history.current;
    if ((e.key !== "ArrowUp" && e.key !== "ArrowDown") || !h.length) return;
    e.preventDefault();
    const cur = at.current < 0 ? h.length : at.current;
    const next = Math.max(0, Math.min(h.length, cur + (e.key === "ArrowUp" ? -1 : 1)));
    at.current = next;
    setText(h[next] ?? "");
  };
  return (
    <Card title="Console" sub="Tab completes, Up and Down go through what you ran.">
      <form className="wa-form" onSubmit={submit}>
        <div className="wa-console-line">
          <label className="k-mono wa-prompt" htmlFor="wa-console">&gt;</label>
          <input id="wa-console" className="k-input k-input-mono" value={text} disabled={!ready} autoComplete="off" spellCheck={false} maxLength={1000}
            placeholder={ready ? "/help" : "not connected"} onChange={(e) => { setText(e.target.value); setSugg([]); }} onKeyDown={key}
            aria-autocomplete="list" aria-expanded={sugg.length > 0} aria-controls="wa-sugg" />
          <Button kind="primary" type="submit" busy={busy} disabled={!ready || !text.trim()}>Run</Button>
        </div>
        {sugg.length > 0 && (
          <ul id="wa-sugg" className="wa-sugg" role="listbox" aria-label="Suggestions">
            {sugg.map((s, i) => (
              <li key={s} role="option" aria-selected={i === sel}>
                <button type="button" className={"wa-sugg-item k-mono" + (i === sel ? " active" : "")} onClick={() => { setText(s); setSugg([]); }}>{s}</button>
              </li>
            ))}
          </ul>
        )}
        <RunAs value={as} onChange={setAs} />
      </form>
    </Card>
  );
}

function Output({ rows, clear }: { rows: OutRow[]; clear: () => void }) {
  const end = useRef<HTMLDivElement>(null);
  useEffect(() => { end.current?.scrollIntoView({ block: "end" }); }, [rows.length]);
  if (!rows.length) return null;
  return (
    <Card title="Output" actions={<Button size="s" kind="ghost" onClick={clear}>Clear</Button>}>
      <div className="wa-output k-mono" role="log" aria-live="polite" data-testid="command-output">
        {rows.map((r, i) => <div key={i} className={`wa-out-${r.level}`}>{r.text}</div>)}
        <div ref={end} />
      </div>
    </Card>
  );
}
