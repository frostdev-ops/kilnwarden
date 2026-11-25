// Dashboard widgets of every module (GET /api/core/modules widgets[]): the metrics module's own (players, a 1 h chart),
// player activity (who is on, the newest joins, leaves and deaths), the pinned saved commands, and extensions' stat,
// table and text widgets bound to their data routes. The overview (pages/Home) places them in the account's layout.
import { useQuery } from "@tanstack/react-query";
import { useAdminState } from "@kiln/admin-ui";
import { SERVER_ID } from "../api/kapSocket";
import { Fragment } from "react";
import { Link } from "react-router-dom";
import type { NavModule } from "../api/types";
import { panel } from "../app/panel";
import { lc } from "./client";
import { pick } from "./ExtensionPage";
import { Card, Empty, LoadError, Loading, Pill } from "../ui/kit";
import { LineChart } from "../ui/LineChart";
import { formatValue } from "../ui/chartMath";
import { sentence } from "../ui/format";
import { PinnedCommands } from "../pages/SavedCommands";
import { EventList, OnlineList } from "../pages/Activity";
import "../laneC.css";

export interface WidgetInfo { id: string; title: string; data: string; kind: string | null; field?: string | null }

/** Every widget the user may see, keyed "module/widget" (the dashboard layout's keys). */
export function widgetsOf(modules: NavModule[]): { key: string; module: string; w: WidgetInfo }[] {
  return modules.flatMap((m) => ((m.widgets ?? []) as unknown as WidgetInfo[]).map((w) => ({ key: `${m.id}/${w.id}`, module: m.id, w })));
}

export function Widget({ module, w }: { module: string; w: WidgetInfo }) {
  if (w.kind === "metrics.chart") return <MetricsChart />;
  if (w.kind === "metrics.players") return <MetricsPlayers title={w.title} />;
  if (w.kind === "activity.recent") return <ActivityRecent title={w.title} />;
  // the saved commands module's widget: the user's pinned saved commands as buttons (its own data route and run flow)
  if (module === "saved-commands" && w.id === "pinned") return <PinnedCommands />;
  return <ExtWidget module={module} w={w} />;
}

function MetricsPlayers({ title }: { title: string }) {
  const q = useQuery({ queryKey: ["metrics-live"], queryFn: lc.metricsLive, refetchInterval: 5000 });
  // until the metrics report a count, the admin connection's player list says it
  const hub = useAdminState(SERVER_ID);
  const value = q.data?.values.players ?? (hub.ready ? hub.state?.players?.length ?? null : null);
  return (
    <Card title={title}>
      {q.isError ? <LoadError what="The player count" error={q.error} retry={() => void q.refetch()} /> : (
        <div className="wa-stat"><span className="wa-stat-value">{formatValue(value, "count")}</span>
          <span className="k-muted">{!q.data ? "Loading…" : q.data.connected === false ? "server not connected" : "online now"}</span></div>
      )}
    </Card>
  );
}

function MetricsChart() {
  const q = useQuery({ queryKey: ["metrics", "1h"], queryFn: () => lc.metricsHistory("1h"), refetchInterval: 30_000 });
  const f = q.data?.fields.find((x) => x.key === "tickP95Ms");
  return (
    <Card>
      {q.isError ? <LoadError what="The tick time chart" error={q.error} retry={() => void q.refetch()} />
        : <LineChart title={f?.title ?? "Tick time (p95)"} unit="ms" t={q.data?.t ?? []} values={q.data?.series.tickP95Ms ?? []}
          empty={!q.data ? "Loading…" : f && !f.available ? sentence(f.reason ?? "Not reported yet") : "No values in the last hour yet."} />}
    </Card>
  );
}

function ActivityRecent({ title }: { title: string }) {
  const q = useQuery({ queryKey: ["activity", "recent"], queryFn: panel.activityRecent, refetchInterval: 10_000 });
  const d = q.data;
  return (
    <Card title={title} actions={d && <Pill tone={d.players.length ? "ok" : "idle"}>{d.players.length} online</Pill>}>
      {q.isError ? <LoadError what="Player activity" error={q.error} retry={() => void q.refetch()} />
        : !d ? <Loading />
        : (
          <div className="wa-sections">
            {d.players.length ? <OnlineList players={d.players.slice(0, 6)} /> : <Empty title="Nobody is on right now" />}
            {d.players.length > 6 && <span className="k-muted">and {d.players.length - 6} more</span>}
            {d.recent.length > 0 && <><h3 className="wa-group-title">Recent</h3><EventList events={d.recent.slice(0, 5)} /></>}
            <Link to="/activity">All player activity</Link>
          </div>
        )}
    </Card>
  );
}

function ExtWidget({ module, w }: { module: string; w: WidgetInfo }) {
  const q = useQuery({ queryKey: ["ext-data", module, w.data], queryFn: () => lc.moduleGet<{ result: unknown }>(module, w.data), refetchInterval: 15_000 });
  const v = q.data?.result;
  return (
    <Card title={w.title}>
      {q.isError ? <LoadError what={w.title} error={q.error} retry={() => void q.refetch()} />
        : q.isLoading ? <Loading />
        : w.kind === "stat" ? <span className="wa-stat-value">{plain(pick(v, w.field))}</span>
        : <ValueView value={v} />}
    </Card>
  );
}

const plain = (v: unknown): string => (v === null || v === undefined || v === "" ? "–" : typeof v === "object" ? JSON.stringify(v) : String(v));

/** A module's data, readable: text as text, a list of rows as a table, an object as name and value pairs. */
export function ValueView({ value }: { value: unknown }) {
  if (value === null || value === undefined || value === "") return <Empty title="Nothing to show yet" />;
  if (typeof value !== "object") return <p className="wa-p wa-break">{String(value)}</p>;
  if (Array.isArray(value)) {
    if (!value.length) return <Empty title="Nothing to show yet" />;
    const rows = value.filter((r) => r && typeof r === "object" && !Array.isArray(r)) as Record<string, unknown>[];
    if (rows.length !== value.length) return <ul className="wa-list">{value.map((x, i) => <li key={i}>{plain(x)}</li>)}</ul>;
    const cols = [...new Set(rows.flatMap((r) => Object.keys(r)))].slice(0, 6);
    return (
      <div className="wa-table-wrap">
        <table className="wa-table">
          <thead><tr>{cols.map((c) => <th key={c}>{c}</th>)}</tr></thead>
          <tbody>{rows.slice(0, 50).map((r, i) => <tr key={i}>{cols.map((c) => <td key={c} data-label={c}>{plain(r[c])}</td>)}</tr>)}</tbody>
        </table>
      </div>
    );
  }
  return <dl className="wa-kv">{Object.entries(value as Record<string, unknown>).slice(0, 30).map(([k, x]) => <Fragment key={k}><dt>{k}</dt><dd className="wa-break">{plain(x)}</dd></Fragment>)}</dl>;
}
