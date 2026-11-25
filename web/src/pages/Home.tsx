// Overview: the server (Kiln.Admin's connection, versions, players online), its restart support (DESIGN §7:
// "supported" only under kilnctl's unit; otherwise the panel offers Stop with a hint), the panel itself (version,
// addresses, TLS) and every module's widgets, in the account's own layout (GET/PUT /api/core/dashboard: order and
// hidden cards; Customize moves and hides them with buttons). The restart is Kiln.Admin's op server_restart over the
// KAP socket, with a countdown and a cancel.
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { kapOps, toast, useAdminCaller, useAdminState, useHasOp } from "@kiln/admin-ui";
import { ArrowDown, ArrowUp, EyeOff } from "lucide-react";
import { useMemo, useState, type ReactNode } from "react";
import { Link } from "react-router-dom";
import { ep, useSession } from "../app/context";
import { panel } from "../app/panel";
import { SERVER_ID } from "../api/kapSocket";
import { arrange, move, type DashboardLayout } from "../api/panel";
import { Widget, widgetsOf } from "../ext/Widgets";
import { can } from "../state/session";
import { duration } from "../ui/format";
import { Banner, Button, Card, LoadError, Loading, Modal, Page, Pill, TextField, explain, when } from "../ui/kit";

const CARDS: Record<string, string> = { "panel/server": "Server", "panel/links": "Quick links", "panel/info": "Panel" };

export function HomePage() {
  const me = useSession((s) => s.me)!;
  const branding = useSession((s) => s.branding);
  const signIns = useSession((s) => s.signIns);
  const qc = useQueryClient();
  // the modules' dashboard widgets (metrics, activity, pinned saved commands, extensions'), the same query the shell's nav uses
  const modules = useQuery({ queryKey: ["modules", me.id, signIns], queryFn: ep.modules, staleTime: 60_000 });
  const layout = useQuery({ queryKey: ["dashboard", me.id], queryFn: panel.dashboard, staleTime: 60_000 });
  const online = useQuery({ queryKey: ["activity", "online"], queryFn: panel.activityOnline, refetchInterval: 15_000, enabled: can(me, "activity.view") });
  const [draft, setDraft] = useState<DashboardLayout | null>(null);
  const [note, setNote] = useState<{ tone: "ok" | "err" | "warn"; text: string } | null>(null);
  const widgets = useMemo(() => widgetsOf(modules.data ?? []), [modules.data]);
  const showInfo = can(me, "core.settings.view");
  const available = useMemo(() => [...Object.keys(CARDS).filter((k) => k !== "panel/info" || showInfo), ...widgets.map((w) => w.key)], [widgets, showInfo]);
  const editing = draft !== null;
  const { visible, hidden } = arrange(available, draft ?? layout.data);
  const titleOf = (k: string) => CARDS[k] ?? widgets.find((w) => w.key === k)?.w.title ?? k;
  const save = useMutation({
    mutationFn: (l: DashboardLayout) => panel.saveDashboard(l),
    onSuccess: (_, l) => {
      qc.setQueryData(["dashboard", me.id], l);
      setDraft(null);
      setNote({ tone: "ok", text: "Your overview is saved. Only your account sees this layout." });
    },
    onError: (e) => setNote({ tone: "err", text: `Not saved. ${explain(e)}` }),
  });
  const start = () => { setNote(null); setDraft({ order: [...visible, ...hidden], hidden: [...hidden] }); };
  const change = (f: (l: DashboardLayout) => DashboardLayout) => setDraft((d) => f(d ?? { order: [...visible, ...hidden], hidden }));
  const you = online.data?.players.find((p) => p.you);

  const render = (key: string): ReactNode => {
    if (key === "panel/server") return <ServerCard />;
    if (key === "panel/links") return <LinksCard />;
    if (key === "panel/info") return <PanelCard />;
    const w = widgets.find((x) => x.key === key);
    return w ? <Widget module={w.module} w={w.w} /> : null;
  };

  return (
    <Page title={branding.name} sub="Overview" wide actions={editing ? (
      <>
        <Button kind="primary" busy={save.isPending} onClick={() => save.mutate({ order: [...visible, ...hidden], hidden })}>Save layout</Button>
        <Button kind="ghost" onClick={() => change(() => ({ order: [...available], hidden: [] }))}>Default layout</Button>
        <Button kind="ghost" onClick={() => { setDraft(null); setNote(null); }}>Cancel</Button>
      </>
    ) : <Button onClick={start} disabled={layout.isLoading || modules.isLoading}>Customize</Button>}>
      {you && <Banner tone="ember" title={`You are in game as ${you.name}`}>On for {duration(you.seconds)}.</Banner>}
      <div aria-live="polite">{note && <Banner tone={note.tone} title={note.text} actions={<Button size="s" kind="ghost" onClick={() => setNote(null)}>Close</Button>} />}</div>
      {editing && <Banner tone="info" title="Customize your overview">Move cards up or down and hide the ones you do not need, then save. Hidden cards wait below.</Banner>}
      {modules.isError && <LoadError what="The widgets" error={modules.error} retry={() => void modules.refetch()} />}
      {layout.isError && !editing && <Banner tone="warn" title="Your saved layout did not load">The overview shows every card in the default order.</Banner>}
      <div className={"wa-dash" + (editing ? " wa-dash-editing" : "")}>
        {visible.map((key, i) => (
          <div key={key} className="wa-dash-item">
            {editing && (
              <div className="wa-dash-tools">
                <span className="wa-dash-name">{titleOf(key)}</span>
                <Button size="s" kind="ghost" disabled={i === 0} aria-label={`Move ${titleOf(key)} up`} onClick={() => change((l) => ({ ...l, order: move(visibleFirst(l, visible), key, -1) }))}>
                  <ArrowUp size={16} aria-hidden /> Up
                </Button>
                <Button size="s" kind="ghost" disabled={i === visible.length - 1} aria-label={`Move ${titleOf(key)} down`} onClick={() => change((l) => ({ ...l, order: move(visibleFirst(l, visible), key, 1) }))}>
                  <ArrowDown size={16} aria-hidden /> Down
                </Button>
                <Button size="s" kind="ghost" aria-label={`Hide ${titleOf(key)}`} onClick={() => change((l) => ({ ...l, hidden: [...l.hidden, key] }))}>
                  <EyeOff size={16} aria-hidden /> Hide
                </Button>
              </div>
            )}
            {render(key)}
          </div>
        ))}
      </div>
      {editing && (
        <Card title="Hidden cards" sub={hidden.length ? "Show one to put it back at the end." : undefined}>
          {hidden.length ? (
            <ul className="wa-people">
              {hidden.map((key) => (
                <li key={key} className="wa-person">
                  <span className="wa-person-name">{titleOf(key)}</span>
                  <span className="wa-person-meta"><Button size="s" aria-label={`Show ${titleOf(key)}`} onClick={() => change((l) => ({ order: [...l.order.filter((k) => k !== key), key], hidden: l.hidden.filter((k) => k !== key) }))}>Show</Button></span>
                </li>
              ))}
            </ul>
          ) : <p className="k-muted wa-p">No hidden cards.</p>}
        </Card>
      )}
    </Page>
  );
}

/** The order with the visible cards first (as shown), so Up and Down move past what is on screen. */
function visibleFirst(l: DashboardLayout, visible: string[]): string[] {
  return [...visible, ...l.order.filter((k) => !visible.includes(k))];
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

function RestartControls({ supported }: { supported: boolean }) {
  const has = useHasOp(SERVER_ID, "server_restart");
  const hasCancel = useHasOp(SERVER_ID, "server_restart_cancel");
  const call = useAdminCaller(SERVER_ID);
  const [ask, setAsk] = useState(false);
  const [seconds, setSeconds] = useState("60");
  const [message, setMessage] = useState("");
  const [confirm, setConfirm] = useState("");
  const [busy, setBusy] = useState(false);
  if (!has) return null;
  const word = supported ? "restart" : "stop";
  const secs = Number(seconds);
  const secsOk = Number.isInteger(secs) && secs >= 0 && secs <= 3600;
  const ok = secsOk && confirm.trim().toLowerCase() === word;
  return (
    <div className="wa-stack wa-gap">
      <div className="wa-row">
        <Button kind="danger" onClick={() => setAsk(true)}>{supported ? "Restart the server" : "Stop the server"}</Button>
        {hasCancel && <Button kind="ghost" onClick={() => void kapOps(call).serverRestartCancel().then(() => toast.ok("Restart canceled")).catch((e) => toast.error(e))}>Cancel a restart</Button>}
      </div>
      {!supported && <p className="k-hint wa-p">Nothing known brings this server back after a stop (it does not run under kilnctl's service). Start it again from where you host it.</p>}
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
