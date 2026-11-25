// Metrics: small multiples, one metric per chart (never two scales on one axis), a range picker above them, the
// fields Kiln.Admin does not report said plainly, and the alert rules (saving them needs recent proof).
import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { useSession } from "../app/context";
import { isApiError } from "../api/http";
import type { AlertRule } from "../api/laneC";
import { lc } from "../ext/client";
import { can } from "../state/session";
import { sentence } from "../ui/format";
import { Button, Card, Check, Empty, LoadError, Page, Pill, Tabs, explain } from "../ui/kit";
import { LineChart } from "../ui/LineChart";
import "../laneC.css";

const RANGES = ["10m", "1h", "6h", "24h", "7d", "30d"] as const;
const CHARTS = ["players", "tickP95Ms", "frameP95Ms", "cpuPercent", "workingSetBytes", "managedHeapBytes", "stepsPerS", "gcPauseMs", "modFaults", "saveAgeS"];
/** Titles for fields the server names only by their key (an unavailable field). */
const TITLES: Record<string, string> = { saveMs: "World save time" };
const RANGE_LABELS: Record<(typeof RANGES)[number], string> = { "10m": "10 min", "1h": "1 hour", "6h": "6 hours", "24h": "24 hours", "7d": "7 days", "30d": "30 days" };

export function MetricsPage() {
  const me = useSession((s) => s.me);
  const [range, setRange] = useState<(typeof RANGES)[number]>("1h");
  const hist = useQuery({ queryKey: ["metrics", range], queryFn: () => lc.metricsHistory(range), refetchInterval: range === "10m" ? 4000 : 30_000 });
  const live = useQuery({ queryKey: ["metrics-live"], queryFn: lc.metricsLive, refetchInterval: 4000 });
  const fields = hist.data?.fields ?? [];
  const missing = fields.filter((f) => !f.available);
  return (
    <Page title="Metrics" wide sub="From Kiln.Admin's metrics, every 2 seconds. History: 24 hours at 10 s, 30 days at 5 min.">
      <div className="wa-row">
        <Tabs label="Range" tabs={RANGES.map((r) => ({ id: r, label: RANGE_LABELS[r] }))} value={range} onChange={setRange} />
        <span className="k-spacer" />
        {live.data && (live.data.connected ? <Pill tone="ok">Connected</Pill> : <Pill tone="warn">Server not connected</Pill>)}
      </div>
      {hist.isError && <LoadError what="The metrics" error={hist.error} retry={() => void hist.refetch()} />}
      <div className="wa-grid">
        {CHARTS.map((key) => {
          const f = fields.find((x) => x.key === key);
          return (
            <Card key={key}>
              <LineChart title={f?.title ?? TITLES[key] ?? key} unit={f?.unit ?? null} t={hist.data?.t ?? []} values={hist.data?.series[key] ?? []}
                empty={!hist.data ? (hist.isError ? "No history." : "Loading…") : f && !f.available ? sentence(f.reason ?? "Not reported yet") : "No values in this range yet."} />
            </Card>
          );
        })}
      </div>
      {missing.length > 0 && (
        <Card title="Not available">
          <ul className="wa-list">{missing.map((f) => <li key={f.key}><strong>{f.title === f.key ? TITLES[f.key] ?? f.title : f.title}</strong>: <span className="k-muted">{sentence(f.reason ?? "not reported")}</span></li>)}</ul>
        </Card>
      )}
      <Alerts editable={can(me, "metrics.alerts.edit")} metrics={fields.filter((f) => f.unit).map((f) => [f.key, f.title] as const)} />
    </Page>
  );
}

function Alerts({ editable, metrics }: { editable: boolean; metrics: (readonly [string, string])[] }) {
  const q = useQuery({ queryKey: ["metrics-alerts"], queryFn: lc.alerts });
  const [draft, setDraft] = useState<AlertRule[] | null>(null);
  const [msg, setMsg] = useState<string | null>(null);
  const rules = draft ?? q.data?.alerts ?? [];
  const set = (i: number, r: Partial<AlertRule>) => setDraft(rules.map((x, j) => (j === i ? { ...x, ...r } : x)));
  const save = async () => {
    try {
      await lc.saveAlerts({ alerts: rules });
      setDraft(null);
      setMsg("Saved.");
      void q.refetch();
    } catch (e) {
      setMsg(isApiError(e) && e.code === "reauth_required" ? "Confirm it is you in the dialog, then save again. Nothing was saved." : `Not saved. ${explain(e)}`);
    }
  };
  return (
    <Card title="Alerts" sub={`A rule fires when it held for its time, then waits its cooldown. At most ${q.data?.maxAlertsPerHour ?? 6} alerts an hour; they go to webhooks that choose "metrics.alert".`}
      actions={editable && <Button size="s" onClick={() => setDraft([...rules, { id: `rule-${rules.length + 1}`, metric: "tickP95Ms", op: ">", threshold: 40, forS: 60, cooldownS: 900, enabled: true }])}>Add rule</Button>}>
      {q.isError && <LoadError what="The alert rules" error={q.error} retry={() => void q.refetch()} />}
      {!rules.length && !q.isError && <Empty title="No alert rules">{editable ? "Add a rule to get a webhook message when a metric stays above or below a value." : "Nobody has added one yet."}</Empty>}
      {rules.length > 0 && (
        <div className="wa-table-wrap"><table className="wa-table">
          <thead><tr><th>On</th><th>Metric</th><th>When</th><th>Value</th><th>For (s)</th><th>Cooldown (s)</th><th /></tr></thead>
          <tbody>{rules.map((r, i) => (
            <tr key={i}>
              <td data-label="On"><Check label={<span className="k-sr-only">Rule {i + 1} on</span>} checked={r.enabled} disabled={!editable} onChange={(on) => set(i, { enabled: on })} /></td>
              <td data-label="Metric"><select className="k-select" aria-label="Metric" value={r.metric} disabled={!editable} onChange={(e) => set(i, { metric: e.target.value })}>
                {metrics.map(([k, t]) => <option key={k} value={k}>{t}</option>)}</select></td>
              <td data-label="When"><select className="k-select" aria-label="Comparison" value={r.op} disabled={!editable} onChange={(e) => set(i, { op: e.target.value as ">" | "<" })}>
                <option value=">">above</option><option value="<">below</option></select></td>
              <td data-label="Value"><input className="k-input" type="number" aria-label="Threshold" value={r.threshold} disabled={!editable} onChange={(e) => set(i, { threshold: Number(e.target.value) })} /></td>
              <td data-label="For"><input className="k-input" type="number" min={0} max={3600} aria-label="For seconds" value={r.forS} disabled={!editable} onChange={(e) => set(i, { forS: Number(e.target.value) })} /></td>
              <td data-label="Cooldown"><input className="k-input" type="number" min={300} max={86400} aria-label="Cooldown seconds" value={r.cooldownS} disabled={!editable} onChange={(e) => set(i, { cooldownS: Number(e.target.value) })} /></td>
              <td className="wa-actions">{editable && <Button size="s" kind="ghost" onClick={() => setDraft(rules.filter((_, j) => j !== i))}>Remove</Button>}</td>
            </tr>))}</tbody>
        </table></div>
      )}
      {editable && draft && <div className="wa-row"><Button kind="primary" onClick={() => void save()}>Save rules</Button><Button kind="ghost" onClick={() => setDraft(null)}>Discard</Button></div>}
      <div aria-live="polite">{msg && <p className="wa-p">{msg}</p>}</div>
    </Card>
  );
}
