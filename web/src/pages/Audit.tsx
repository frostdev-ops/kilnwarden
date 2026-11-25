// The audit log (DESIGN §5): newest first, filtered by user and by the start of the op ("auth.", "users.", "kap.");
// "Older" pages back with `before`; the loaded entries export as JSON Lines.
import { useInfiniteQuery, useQuery } from "@tanstack/react-query";
import { Fragment, useMemo, useState } from "react";
import { ep, useSession } from "../app/context";
import type { AuditEntry } from "../api/types";
import { can } from "../state/session";
import { Button, Card, Empty, LoadError, Loading, Page, Pill, when } from "../ui/kit";

const PAGE = 100;
const PRESETS: [string, string][] = [["", "Everything"], ["auth.", "Sign-ins"], ["users.", "Accounts and roles"], ["kap.", "Server actions"], ["core.", "Panel settings"]];

export function AuditPage() {
  const me = useSession((s) => s.me)!;
  const [user, setUser] = useState("");
  const [op, setOp] = useState("");
  const [open, setOpen] = useState<string | null>(null);
  const accounts = useQuery({ queryKey: ["accounts"], queryFn: ep.accounts, enabled: can(me, "users.view") });
  const q = useInfiniteQuery({
    queryKey: ["audit", user, op],
    initialPageParam: null as string | null,
    queryFn: ({ pageParam }) => ep.audit({ limit: PAGE, before: pageParam ?? undefined, user: user || undefined, op: op || undefined }),
    getNextPageParam: (last) => (last.length < PAGE ? null : last[last.length - 1]?.at ?? null),
  });
  const entries = useMemo(() => q.data?.pages.flat() ?? [], [q.data]);
  const exportUrl = useMemo(() => (entries.length ? URL.createObjectURL(new Blob([entries.map((e) => JSON.stringify(e)).join("\n") + "\n"], { type: "application/x-ndjson" })) : null), [entries]);
  return (
    <Page title="Audit log" wide sub="Every sign-in, account and role change, and every action on the server through this panel.">
      <Card title="Filter" actions={exportUrl && <a className="k-btn k-btn-s" href={exportUrl} download="audit.jsonl">Export loaded</a>}>
        <div className="wa-row wa-end">
          <div className="k-field">
            <label className="k-label" htmlFor="wa-audit-user">User</label>
            <select id="wa-audit-user" className="k-select" value={user} onChange={(e) => setUser(e.target.value)}>
              <option value="">Everyone</option>
              {(accounts.data ?? []).map((a) => <option key={a.id} value={a.id}>{a.name}</option>)}
            </select>
          </div>
          <div className="k-field">
            <label className="k-label" htmlFor="wa-audit-op">What</label>
            <select id="wa-audit-op" className="k-select" value={op} onChange={(e) => setOp(e.target.value)}>
              {PRESETS.map(([v, l]) => <option key={v} value={v}>{l}</option>)}
            </select>
          </div>
          <Button onClick={() => void q.refetch()} busy={q.isRefetching}>Refresh</Button>
        </div>
      </Card>
      {q.isError && <LoadError what="The audit log" error={q.error} retry={() => void q.refetch()} />}
      {q.isLoading && <Loading />}
      {!q.isLoading && !q.isError && !entries.length && (
        <Empty title={op || user ? "Nothing matches this filter" : "Nothing recorded yet"}>
          {op || user ? "Choose someone else or another kind of action." : "Sign-ins, account changes and actions on the server appear here as they happen."}
        </Empty>
      )}
      {entries.length > 0 && <div className="wa-table-wrap">
        <table className="wa-table wa-table-compact wa-audit">
          <thead><tr><th>When</th><th>Who</th><th>What</th><th>Result</th><th>From</th><th><span className="k-sr-only">Details</span></th></tr></thead>
          <tbody>
            {entries.map((e, i) => {
              const key = `${e.at}-${i}`;
              return (
                <AuditRow key={key} e={e} open={open === key} toggle={() => setOpen(open === key ? null : key)} />
              );
            })}
          </tbody>
        </table>
      </div>}
      {q.hasNextPage && <Button onClick={() => void q.fetchNextPage()} busy={q.isFetchingNextPage}>Older</Button>}
    </Page>
  );
}

function AuditRow({ e, open, toggle }: { e: AuditEntry; open: boolean; toggle: () => void }) {
  return (
    <>
      <tr>
        <td data-label="When" className="wa-nowrap">{when(e.at)}</td>
        <td data-label="Who">{e.userName || <span className="k-muted">anonymous</span>}</td>
        <td data-label="What" className="k-mono">{e.op}</td>
        <td data-label="Result">{e.ok ? <Pill tone="ok">ok</Pill> : <Pill tone="err" title={e.error}>refused</Pill>} {!e.ok && e.error && <span className="k-muted wa-break">{e.error}</span>}</td>
        <td data-label="From" className="k-mono">{e.ip ?? ""}</td>
        <td className="wa-actions">{e.args !== undefined && <Button size="s" kind="ghost" onClick={toggle} aria-expanded={open}>{open ? "Hide" : "Details"}</Button>}</td>
      </tr>
      {open && (
        <tr className="wa-detail"><td colSpan={6}><Args args={e.args} />{e.lost ? <p className="k-error-text">{e.lost} entries before this one were lost (the log was busy).</p> : null}</td></tr>
      )}
    </>
  );
}

/** What the action was given: name and value pairs (nested values as compact JSON; secrets are masked by the server). */
function Args({ args }: { args: unknown }) {
  if (!args || typeof args !== "object" || Array.isArray(args)) return <p className="wa-p k-mono wa-break">{JSON.stringify(args)}</p>;
  const entries = Object.entries(args as Record<string, unknown>);
  if (!entries.length) return <p className="k-muted wa-p">No arguments.</p>;
  return (
    <dl className="wa-kv">
      {entries.map(([k, v]) => <Fragment key={k}><dt>{k}</dt><dd className="k-mono wa-break">{typeof v === "string" ? v : JSON.stringify(v)}</dd></Fragment>)}
    </dl>
  );
}
