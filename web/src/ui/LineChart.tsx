// One metric over time (dataviz rules: one axis per chart, so several metrics are small multiples; a single series has
// no legend, its title names it; 2px line, recessive grid; a crosshair with a tooltip on hover/touch; a table view).
// Series colour: categorical slot 1 (#3987e5, validated against the Hearth surface #1a120e: lightness, chroma and
// 3:1 contrast pass); text stays in the text tokens.
import { useEffect, useId, useMemo, useRef, useState } from "react";
import { formatValue, linePath, nearestIndex, niceMax, ticks } from "./chartMath";

const H = 160;
const PAD = { l: 52, r: 12, t: 10, b: 22 };

export function LineChart({ title, unit, t, values, empty }: { title: string; unit: string | null; t: number[]; values: (number | null)[]; empty?: string }) {
  const box = useRef<HTMLDivElement>(null);
  const [w, setW] = useState(480);
  const [hover, setHover] = useState<number | null>(null);
  const [table, setTable] = useState(false);
  const id = useId();
  useEffect(() => {
    const el = box.current;
    if (!el) return;
    const ro = new ResizeObserver(([e]) => setW(Math.max(220, Math.round(e.contentRect.width))));
    ro.observe(el);
    return () => ro.disconnect();
  }, []);
  const nums = values.filter((v): v is number => v !== null && Number.isFinite(v));
  const top = niceMax(nums.length ? Math.max(...nums) : 0);
  const t0 = t[0] ?? 0;
  const t1 = t[t.length - 1] ?? 1;
  const x = (v: number) => PAD.l + ((v - t0) / Math.max(1, t1 - t0)) * (w - PAD.l - PAD.r);
  const y = (v: number) => PAD.t + (1 - v / top) * (H - PAD.t - PAD.b);
  const d = useMemo(() => linePath(t, values, x, y), [t, values, w, top]); // eslint-disable-line react-hooks/exhaustive-deps
  const last = nums.length ? values[values.lastIndexOf(nums[nums.length - 1])] : null;
  const time = (ms: number) => new Date(ms).toLocaleTimeString(undefined, { hour: "2-digit", minute: "2-digit" });
  const day = (ms: number) => new Date(ms).toLocaleString(undefined, { month: "short", day: "numeric", hour: "2-digit", minute: "2-digit" });
  const span = t1 - t0 > 36 * 3600_000 ? day : time;
  const onMove = (clientX: number) => {
    const r = box.current?.querySelector("svg")?.getBoundingClientRect();
    if (!r || !t.length) return;
    const tx = t0 + ((clientX - r.left - PAD.l) / (w - PAD.l - PAD.r)) * (t1 - t0);
    setHover(nearestIndex(t, tx));
  };
  const hv = hover !== null ? values[hover] : null;
  return (
    <figure className="wa-chart" aria-labelledby={id}>
      <figcaption className="wa-chart-head">
        <span id={id} className="wa-chart-title">{title}</span>
        <span className="wa-chart-now">{formatValue(last, unit)}</span>
        <button type="button" className="k-btn k-btn-ghost k-btn-s" aria-pressed={table} onClick={() => setTable(!table)}>{table ? "Chart" : "Table"}</button>
      </figcaption>
      <div ref={box} className="wa-chart-box">
        {!nums.length ? (
          <p className="k-muted wa-chart-empty">{empty ?? "No values yet."}</p>
        ) : table ? (
          <div className="wa-table-wrap wa-chart-table">
            <table className="wa-table"><thead><tr><th>Time</th><th>{title}</th></tr></thead>
              <tbody>{t.map((ts, i) => <tr key={ts}><td>{day(ts)}</td><td>{formatValue(values[i], unit)}</td></tr>).reverse()}</tbody></table>
          </div>
        ) : (
          <svg width={w} height={H} role="img" aria-label={`${title}: latest ${formatValue(last, unit)}`}
            onPointerMove={(e) => onMove(e.clientX)} onPointerLeave={() => setHover(null)} onPointerDown={(e) => onMove(e.clientX)}>
            {ticks(top).map((v) => (
              <g key={v}>
                <line x1={PAD.l} x2={w - PAD.r} y1={y(v)} y2={y(v)} className="wa-chart-grid" />
                <text x={PAD.l - 6} y={y(v)} className="wa-chart-axis" textAnchor="end" dominantBaseline="middle">{formatValue(v, unit)}</text>
              </g>
            ))}
            <text x={PAD.l} y={H - 4} className="wa-chart-axis">{span(t0)}</text>
            <text x={w - PAD.r} y={H - 4} className="wa-chart-axis" textAnchor="end">{span(t1)}</text>
            <path d={d} className="wa-chart-line" />
            {hover !== null && (
              <g>
                <line x1={x(t[hover])} x2={x(t[hover])} y1={PAD.t} y2={H - PAD.b} className="wa-chart-cross" />
                {hv !== null && <circle cx={x(t[hover])} cy={y(hv)} r={4} className="wa-chart-dot" />}
              </g>
            )}
          </svg>
        )}
        {hover !== null && !table && (
          <div className="wa-chart-tip" style={{ left: Math.min(Math.max(x(t[hover]) - 60, 0), w - 140) }} role="status">
            <div>{day(t[hover])}</div><strong>{formatValue(hv, unit)}</strong>
          </div>
        )}
      </div>
    </figure>
  );
}
