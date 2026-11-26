// The overview's cards and the account's layout of them (0.1.3: shared by the overview and the setup wizard's
// "Customize your dashboard"): the server (Kiln.Admin's connection, versions, players online, restart support: DESIGN
// §7, "supported" only under kilnctl's unit; otherwise the panel offers Stop with a hint), quick links, the panel itself
// (version, addresses, TLS), and every module's dashboard widgets. The layout is GET/PUT /api/core/dashboard (the order
// and the hidden cards, per account). The restart is Kiln.Admin's op server_restart over the KAP socket, with a
// countdown and a cancel.
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { kapOps, toast, useAdminCaller, useAdminState, useHasOp } from "@kiln/admin-ui";
import { EyeOff, GripVertical } from "lucide-react";
import { useMemo, useState, type ReactNode } from "react";
import { Link } from "react-router-dom";
import { ep, useSession } from "../app/context";
import { panel } from "../app/panel";
import { SERVER_ID } from "../api/kapSocket";
import { layoutOf, type DashboardLayout } from "../api/panel";
import { Widget, widgetsOf } from "../ext/Widgets";
import { can } from "../state/session";
import { SortableList } from "../ui/Sortable";
import { Button, Card, LoadError, Loading, Modal, Pill, TextField, Toggle, when } from "../ui/kit";

export const CARDS: Record<string, string> = { "panel/server": "Server", "panel/links": "Quick links", "panel/info": "Panel" };

/** The cards this account may see, their titles, how each draws, and the saved layout. */
export function useOverview() {
  const me = useSession((s) => s.me)!;
  const signIns = useSession((s) => s.signIns);
  // the modules' dashboard widgets (metrics, activity, pinned saved commands, extensions'), the same query the shell's nav uses
  const modules = useQuery({ queryKey: ["modules", me.id, signIns], queryFn: ep.modules, staleTime: 60_000 });
  const layout = useQuery({ queryKey: ["dashboard", me.id], queryFn: panel.dashboard, staleTime: 60_000 });
  const widgets = useMemo(() => widgetsOf(modules.data ?? []), [modules.data]);
  const showInfo = can(me, "core.settings.view");
  const available = useMemo(() => [...Object.keys(CARDS).filter((k) => k !== "panel/info" || showInfo), ...widgets.map((w) => w.key)], [widgets, showInfo]);
  const titleOf = (k: string) => CARDS[k] ?? widgets.find((w) => w.key === k)?.w.title ?? k;
  const render = (key: string): ReactNode => {
    if (key === "panel/server") return <ServerCard />;
    if (key === "panel/links") return <LinksCard />;
    if (key === "panel/info") return <PanelCard />;
    const w = widgets.find((x) => x.key === key);
    return w ? <Widget module={w.module} w={w.w} /> : null;
  };
  const qc = useQueryClient();
  const save = useMutation({
    mutationFn: (l: DashboardLayout) => panel.saveDashboard(l),
    onSuccess: (_, l) => qc.setQueryData(["dashboard", me.id], l),
  });
  return { me, modules, layout, available, titleOf, render, save, saved: layoutOf(available, layout.data) };
}

/** The layout editor of the overview and the wizard: every card in its place, dragged by its handle (mouse, touch or
 * the keyboard), each with a Show switch. `content` draws a card under its bar (the card itself, or a small preview). */
export function LayoutEditor({ value, onChange, titleOf, content, className, listLabel = "Your overview's cards" }: {
  value: DashboardLayout; onChange: (l: DashboardLayout) => void; titleOf: (k: string) => string;
  content: (key: string, shown: boolean) => ReactNode; className?: string; listLabel?: string;
}) {
  const hidden = new Set(value.hidden);
  return (
    <SortableList items={value.order} label={titleOf} listLabel={listLabel} className={className}
      itemClassName={(k) => (hidden.has(k) ? "wa-card-off" : undefined)}
      onChange={(order) => onChange({ order, hidden: value.hidden })}
      render={(key, handle) => {
        const shown = !hidden.has(key);
        return (
          <div className="wa-dash-edit">
            <div className="wa-dash-tools">
              <button {...handle}><GripVertical size={18} aria-hidden /></button>
              <span className="wa-dash-name">{titleOf(key)}</span>
              {!shown && <EyeOff size={16} aria-hidden className="k-muted" />}
              <Toggle label="Show" ariaLabel={`Show ${titleOf(key)}`} checked={shown}
                onChange={(on) => onChange({ order: value.order, hidden: on ? value.hidden.filter((k) => k !== key) : [...value.hidden, key] })} />
            </div>
            {content(key, shown)}
          </div>
        );
      }} />
  );
}

function ServerCard() {
  const status = useQuery({ queryKey: ["server-status"], queryFn: ep.serverStatus, refetchInterval: 15_000 });
  const hub = useAdminState(SERVER_ID);
  const players = hub.state?.players?.length;
  const s = status.data;
  return (
    <Card title="Server">
      {status.isError && <LoadError what="The server's status" error={status.error} retry={() => void status.refetch()} />}
      {status.isLoading && <Loading />}
      {s && (
        <dl className="wa-kv">
          <dt>Kiln.Admin</dt>
          <dd>{s.connected ? <Pill tone="ok">connected</Pill> : <Pill tone="err">not connected</Pill>}</dd>
          {s.server?.name && (<><dt>Server</dt><dd className="wa-break">{s.server.name}</dd></>)}
          <dt>Kiln</dt>
          <dd className="k-mono wa-break">{s.kiln ?? "unknown"}</dd>
          <dt>Players online</dt>
          <dd>{hub.ready ? players ?? 0 : <span className="k-muted">{hub.status === "error" ? "not connected" : "connecting…"}</span>}</dd>
          <dt>Restart</dt>
          <dd>{s.restart?.supported === "supported" ? <Pill tone="ok">supported ({s.restart.via ?? "host"})</Pill> : <Pill tone="warn">unknown</Pill>}</dd>
        </dl>
      )}
      <RestartControls supported={s?.restart?.supported === "supported"} />
    </Card>
  );
}

function LinksCard() {
  const me = useSession((s) => s.me)!;
  return (
    <Card title="Quick links">
      <ul className="wa-links">
        <li><Link to={`/servers/${SERVER_ID}/admin/players`}>Players</Link></li>
        <li><Link to={`/servers/${SERVER_ID}/admin/map`}>Map</Link></li>
        {can(me, "activity.view") && <li><Link to="/activity">Player activity</Link></li>}
        <li><Link to="/commands">Commands</Link></li>
        {can(me, "schedules.view") && <li><Link to="/schedules">Schedules</Link></li>}
        {can(me, "metrics.view") && <li><Link to="/metrics">Metrics</Link></li>}
        {can(me, "audit.view") && <li><Link to="/audit">Audit log</Link></li>}
        {can(me, "users.view") && <li><Link to="/users">Users and roles</Link></li>}
        {can(me, "core.settings.view") && <li><Link to="/settings">Panel settings</Link></li>}
        <li><Link to="/account">My account</Link></li>
      </ul>
    </Card>
  );
}

function PanelCard() {
  const info = useQuery({ queryKey: ["panel-info"], queryFn: ep.info });
  const d = info.data;
  return (
    <Card title="Panel">
      {info.isError ? <LoadError what="The panel's details" error={info.error} retry={() => void info.refetch()} />
        : !d ? <Loading />
        : (
          <dl className="wa-kv">
            <dt>Version</dt><dd className="k-mono wa-break">{d.product} {d.version}</dd>
            <dt>Addresses</dt><dd className="k-mono wa-break">{d.endpoints.join(", ") || "none"}</dd>
            <dt>TLS</dt><dd className="wa-break">{d.connection.protocol ? [d.tls.engine, d.connection.protocol, d.connection.cipherSuite].filter(Boolean).join(" · ") : `off (plain HTTP)`}</dd>
            {d.tls.certificate && (
              <>
                <dt>Certificate</dt>
                <dd><span className="k-mono wa-break">{d.tls.certificate.sha256}</span><br />{d.tls.certificate.keyType}, {d.tls.certificate.source}, until {when(d.tls.certificate.notAfter)}</dd>
              </>
            )}
          </dl>
        )}
    </Card>
  );
}

/** Restart (or stop) with a countdown, typed confirmation and cancel. `compact` (the setup wizard): one "Restart now"
 * button, shown only where restarts are supported. */
export function RestartControls({ supported, compact }: { supported: boolean; compact?: boolean }) {
  const has = useHasOp(SERVER_ID, "server_restart");
  const hasCancel = useHasOp(SERVER_ID, "server_restart_cancel");
  const call = useAdminCaller(SERVER_ID);
  const [ask, setAsk] = useState(false);
  const [seconds, setSeconds] = useState("60");
  const [message, setMessage] = useState("");
  const [confirm, setConfirm] = useState("");
  const [busy, setBusy] = useState(false);
  if (!has || (compact && !supported)) return null;
  const word = supported ? "restart" : "stop";
  const secs = Number(seconds);
  const secsOk = Number.isInteger(secs) && secs >= 0 && secs <= 3600;
  const ok = secsOk && confirm.trim().toLowerCase() === word;
  return (
    <div className="wa-stack wa-gap">
      <div className="wa-row">
        <Button kind={compact ? "default" : "danger"} onClick={() => setAsk(true)}>{compact ? "Restart now" : supported ? "Restart the server" : "Stop the server"}</Button>
        {hasCancel && !compact && <Button kind="ghost" onClick={() => void kapOps(call).serverRestartCancel().then(() => toast.ok("Restart canceled")).catch((e) => toast.error(e))}>Cancel a restart</Button>}
      </div>
      {!supported && !compact && <p className="k-hint wa-p">Nothing known brings this server back after a stop (it does not run under kilnctl's service). Start it again from where you host it.</p>}
      {ask && (
        <Modal title={supported ? "Restart the server" : "Stop the server"} onClose={() => setAsk(false)} footer={
          <>
            <Button kind="ghost" onClick={() => setAsk(false)}>Keep it running</Button>
            <Button kind="danger" busy={busy} disabled={!ok} onClick={async () => {
              setBusy(true);
              try {
                const r = await kapOps(call).serverRestart({ seconds: secs, ...(message.trim() ? { message: message.trim() } : {}) });
                toast.ok(`The server will ${word} in ${r.secondsLeft} s`);
                setAsk(false);
              } catch (e) {
                toast.error(e);
              } finally {
                setBusy(false);
              }
            }}>{supported ? "Restart" : "Stop"}</Button>
          </>
        }>
          <div className="wa-form">
            <p className="wa-p">Players see a countdown in chat; the world is saved first. {supported ? "kilnctl's service starts the server again." : "The server stays off until someone starts it."}</p>
            <TextField label="Countdown (seconds, 0 to 3600)" value={seconds} onChange={setSeconds} inputMode="numeric" error={secsOk ? undefined : "A whole number of seconds, 0 to 3600."} />
            <TextField label="Message for the players (optional)" value={message} onChange={setMessage} maxLength={200} />
            <TextField label={`Type "${word}" to confirm`} value={confirm} onChange={setConfirm} autoComplete="off" />
          </div>
        </Modal>
      )}
    </div>
  );
}
