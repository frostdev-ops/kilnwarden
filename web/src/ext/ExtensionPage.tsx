// Extension pages (/m/:module/:page): declarative pages rendered from their layout (no JavaScript from the mod), custom
// pages in a sandboxed iframe (allow-scripts only: an opaque origin, no cookies, no panel DOM) with the kwa/1 host
// bridge, and the module's settings form. Every call goes to the server, which checks it again.
import { useQuery } from "@tanstack/react-query";
import { useCallback, useEffect, useRef, useState } from "react";
import { useParams } from "react-router-dom";
import { ep, useSession } from "../app/context";
import { isApiError } from "../api/http";
import type { Block, ModuleDescription } from "../api/laneC";
import { Banner, Button, Card, LoadError, Loading, Page, errorText } from "../ui/kit";
import { Link } from "react-router-dom";
import { allowed, frameGuard, fromOurFrame, parseFrameMessage, rateLimiter, type FrameAllow } from "./bridge";
import { lc } from "./client";
import { SchemaForm } from "./SchemaForm";
import "../laneC.css";

/** A dotted path into a query result ("stats.count"). */
export function pick(o: unknown, path: string | null | undefined): unknown {
  if (!path) return o;
  let cur: unknown = o;
  for (const k of path.split(".")) {
    if (!cur || typeof cur !== "object") return undefined;
    cur = (cur as Record<string, unknown>)[k];
  }
  return cur;
}

const show = (v: unknown) => (v === null || v === undefined ? "–" : typeof v === "object" ? JSON.stringify(v) : String(v));

export function ExtensionPage() {
  const { module = "", page = "" } = useParams();
  const me = useSession((s) => s.me);
  const signIns = useSession((s) => s.signIns);
  const modules = useQuery({ queryKey: ["modules", me?.id, signIns], queryFn: ep.modules, staleTime: 60_000 });
  const mod = modules.data?.find((m) => m.id === module);
  const p = mod?.pages.find((x) => x.id === page);
  if (modules.isLoading) return <Page title="Loading the page"><Loading /></Page>;
  if (modules.isError) return <Page title="This page did not load"><LoadError what="The module list" error={modules.error} retry={() => void modules.refetch()} /></Page>;
  if (!mod || !p) return (
    <Page title="Not found">
      <Banner tone="warn" title="This module or page is not there">Its module may be switched off, or the mod that adds it is not loaded. <Link to="/">Back to the overview</Link></Banner>
    </Page>
  );
  return (
    <Page title={p.title} sub={mod.title} wide>
      {p.kind === "custom" ? <CustomFrame module={module} entry={p.entry} title={p.title} /> : p.kind === "declarative" ? <Declarative module={module} page={page} /> : (
        <Banner tone="info" title="This kind of page cannot be shown here">The mod that adds it may need a newer Kilnwarden.</Banner>
      )}
      <ModuleSettings module={module} />
    </Page>
  );
}

function Declarative({ module, page }: { module: string; page: string }) {
  const layout = useQuery({ queryKey: ["ext-layout", module, page], queryFn: () => lc.layout(module, page) });
  if (layout.isError) return <LoadError what="This page" error={layout.error} retry={() => void layout.refetch()} />;
  if (!layout.data) return <Loading />;
  return <div className="wa-grid">{layout.data.blocks.map((b, i) => <BlockView key={i} module={module} b={b} />)}</div>;
}

function BlockView({ module, b }: { module: string; b: Block }) {
  const [result, setResult] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const data = useQuery({
    queryKey: ["ext-data", module, "path" in b ? b.path : ""],
    queryFn: () => lc.moduleGet<{ result: unknown }>(module, (b as { path: string }).path),
    enabled: b.type === "stat" || b.type === "table",
    refetchInterval: 10_000,
  });
  const run = async (path: string, args: unknown, confirm?: string | null) => {
    if (confirm && !window.confirm(confirm)) return;
    setBusy(true);
    setError(null);
    try {
      const r = await lc.modulePost<{ result: unknown }>(module, path, args);
      setResult(r?.result === null || r?.result === undefined ? "Done." : show(pick(r.result, "message") ?? r.result));
      void data.refetch();
    } catch (e) {
      setError(isApiError(e) && e.code === "reauth_required" ? "Confirm it is you, then run it again." : errorText(e));
    } finally {
      setBusy(false);
    }
  };
  const status = <>{result && <p className="k-ok-text" role="status">{result}</p>}{error && <p className="k-error-text" role="alert">{error}</p>}</>;
  switch (b.type) {
    case "text":
      return <Card><p className="wa-break">{b.text}</p></Card>;
    case "stat":
      return (
        <Card title={b.title}>
          <div className="wa-stat"><span className="wa-stat-value">{show(pick(data.data?.result, b.field))}</span>{b.unit && <span className="k-muted">{b.unit}</span>}</div>
        </Card>
      );
    case "table": {
      const rows = pick(data.data?.result, b.rows);
      const list = Array.isArray(rows) ? rows.slice(0, 500) : [];
      return (
        <Card title={b.title ?? undefined}>
          <div className="wa-table-wrap"><table className="wa-table">
            <thead><tr>{b.columns.map((c) => <th key={c.field}>{c.title}</th>)}</tr></thead>
            <tbody>{list.map((r, i) => <tr key={i}>{b.columns.map((c) => <td key={c.field} data-label={c.title}>{show(pick(r, c.field))}</td>)}</tr>)}</tbody>
          </table></div>
          {!list.length && <p className="k-muted">Nothing yet.</p>}
        </Card>
      );
    }
    case "form":
      return <Card title={b.title}><SchemaForm schema={b.schema} submit={b.submit} busy={busy} onSubmit={(v) => void run(b.path, v, b.confirm)} />{status}</Card>;
    case "button":
      return <Card><Button onClick={() => void run(b.path, b.args ?? {}, b.confirm)} busy={busy}>{b.label}</Button>{status}</Card>;
  }
}

/** The host of a custom page: posts the frame's calls to the module's own routes, forwards its subscribed topics. */
function CustomFrame({ module, entry, title }: { module: string; entry: string; title: string }) {
  const [generation, setGeneration] = useState(0);
  return <CustomFrameOnce key={generation} module={module} entry={entry} title={title} reload={() => setGeneration((g) => g + 1)} />;
}

function CustomFrameOnce({ module, entry, title, reload }: { module: string; entry: string; title: string; reload: () => void }) {
  const frame = useRef<HTMLIFrameElement>(null);
  const [height, setHeight] = useState(480);
  const [left, setLeft] = useState(false);
  const guard = useRef(frameGuard());
  const desc = useQuery({ queryKey: ["ext-describe", module], queryFn: () => lc.describe(module) });
  const subs = useRef(new Set<string>());
  // the frame's document is opaque ("null" origin), so "*" is the only target; the guard keeps it the first document
  const reply = useCallback((m: Record<string, unknown>) => {
    if (guard.current.accepts()) frame.current?.contentWindow?.postMessage({ kwa: 1, ...m }, "*");
  }, []);
  useEffect(() => {
    const d: ModuleDescription | undefined = desc.data;
    if (!d) return;
    const allow: FrameAllow = { actions: new Set(d.actions.map((a) => a.name)), queries: new Set(d.queries), topics: new Set(d.topics) };
    const limit = rateLimiter();
    const onMessage = async (e: MessageEvent) => {
      if (!fromOurFrame(e, frame.current?.contentWindow) || !guard.current.accepts() || !limit()) return;
      const req = parseFrameMessage(e.data);
      if ("refused" in req) return;
      const id = "id" in req ? req.id : undefined;
      if (!allowed(req, allow)) {
        if (id !== undefined) reply({ type: "result", id, ok: false, error: "not allowed" });
        return;
      }
      try {
        switch (req.type) {
          case "call": {
            const a = d.actions.find((x) => x.name === req.action);
            if (a?.confirm && !window.confirm(a.confirm)) return reply({ type: "result", id, ok: false, error: "cancelled" });
            const r = await lc.modulePost<{ result: unknown }>(module, `/api/${module}/actions/${req.action}`, req.args);
            return reply({ type: "result", id, ok: true, result: r?.result ?? null });
          }
          case "query": {
            const r = await lc.query<{ result: unknown }>(module, req.query, req.params);
            return reply({ type: "result", id, ok: true, result: r?.result ?? null });
          }
          case "subscribe": subs.current.add(req.topic); return;
          case "unsubscribe": subs.current.delete(req.topic); return;
          case "resize": setHeight(Math.max(200, Math.min(4000, req.height + 16))); return;
          case "ready": return reply({ type: "theme", tokens: themeTokens() });
        }
      } catch (err) {
        if (id !== undefined) reply({ type: "result", id, ok: false, error: errorText(err) });
      }
    };
    const l = (e: MessageEvent) => void onMessage(e);
    window.addEventListener("message", l);
    return () => window.removeEventListener("message", l);
  }, [desc.data, module, reply]);
  // topics: poll the module's events while something is subscribed
  useEffect(() => {
    let after = 0;
    let stop = false;
    const tick = async () => {
      if (stop) return;
      if (subs.current.size) {
        try {
          const r = await lc.events(module, after);
          if (after === 0) after = r.seq; // start from now
          else {
            for (const ev of r.events) if (subs.current.has(ev.topic)) reply({ type: "event", topic: ev.topic, seq: ev.seq, payload: ev.payload });
            after = r.seq;
          }
        } catch {
          // keep polling
        }
      }
      if (!stop) setTimeout(() => void tick(), 3000);
    };
    void tick();
    return () => {
      stop = true;
    };
  }, [module, reply]);
  if (desc.isError) return <LoadError what="This page" error={desc.error} retry={() => void desc.refetch()} />;
  if (left)
    return (
      <Banner tone="warn" title="This page left its frame, so the panel stopped answering it.">
        <Button size="s" onClick={reload}>Load the page again</Button>
      </Banner>
    );
  const onLoad = () => {
    guard.current.loaded();
    if (guard.current.navigated()) {
      subs.current.clear();
      setLeft(true);
    }
  };
  return <iframe ref={frame} className="wa-ext-frame" title={title} src={entry} sandbox="allow-scripts" referrerPolicy="no-referrer" style={{ height }} onLoad={onLoad} />;
}

function themeTokens(): Record<string, string> {
  const cs = getComputedStyle(document.documentElement);
  const out: Record<string, string> = {};
  for (const [k, v] of [["--kwa-text", "--k-text-0"], ["--kwa-muted", "--k-text-2"], ["--kwa-bg", "--k-bg-1"], ["--kwa-accent", "--k-ember-400"], ["--kwa-line", "--k-line"]])
    out[k] = cs.getPropertyValue(v).trim();
  return out;
}

function ModuleSettings({ module }: { module: string }) {
  const desc = useQuery({ queryKey: ["ext-describe", module], queryFn: () => lc.describe(module) });
  const s = useQuery({ queryKey: ["ext-settings", module], queryFn: () => lc.extSettings(module), enabled: desc.data?.settings === true });
  const [msg, setMsg] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  if (!desc.data?.settings || !s.data) return null;
  return (
    <Card title="Settings" sub="Changing them asks you to confirm it is you.">
      <SchemaForm schema={s.data.schema} initial={s.data.value} submit="Save" busy={busy} onSubmit={async (v) => {
        setBusy(true);
        try {
          await lc.saveExtSettings(module, v);
          setMsg("Saved.");
          void s.refetch();
        } catch (e) {
          setMsg(isApiError(e) && e.code === "reauth_required" ? "Confirm it is you, then save again." : errorText(e));
        } finally {
          setBusy(false);
        }
      }} />
      {msg && <p role="status">{msg}</p>}
    </Card>
  );
}
