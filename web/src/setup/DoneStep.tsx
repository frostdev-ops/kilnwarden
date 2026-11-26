// The setup wizard's end (0.1.3): the panel's address, then what was set up and what was skipped, each with the page
// where it changes later (each one a link there).
import { useQuery } from "@tanstack/react-query";
import { useEffect, useRef } from "react";
import { useNavigate } from "react-router-dom";
import { api } from "../app/context";
import { ops } from "../app/ops";
import { isPartError } from "../api/ops";
import { isDefaultLayout } from "../api/panel";
import { useOverview } from "../pages/OverviewCards";
import { Button } from "../ui/kit";
import { Frame } from "./Frame";
import { stagedHere } from "./ModsStep";
import { setupSeen } from "./SetupPending";
import { doneRows, routeOf, setupApi, type Route, type SetupState } from "./wizard";

const setup = setupApi(api);

export function DoneStep({ state, route, back }: { state: SetupState; route: Route | ""; back: () => void }) {
  const nav = useNavigate();
  const r = routeOf({ route, hosted: state.hosted });
  const url = r === "hosted" && state.hosted?.ready ? state.hosted.url : r === "own" ? state.own?.url : null;
  const here = !!url && typeof location !== "undefined" && url.startsWith(location.origin);
  const finished = useRef<Promise<unknown> | null>(null);
  const updates = useQuery({ queryKey: ["updates"], queryFn: ops.updates, retry: 0 });
  const { available, saved, layout } = useOverview();
  useEffect(() => {
    setupSeen();
    if (!state.done && !finished.current) finished.current = setup.done().catch(() => undefined);
  }, [state.done]);
  const go = (to: string) => void (finished.current ?? Promise.resolve()).then(() => nav(to, { replace: true }));
  const units = updates.data && !isPartError(updates.data.updates) ? updates.data.updates.units ?? [] : [];
  const mods = Math.max(units.filter((u) => u.staged).length, stagedHere().length);
  const customized = !!layout.data && (layout.data.order.length > 0 || layout.data.hidden.length > 0) && !isDefaultLayout(available, saved);
  // the route this run of the wizard took (the state was read when it opened, before the route was picked)
  const rows = doneRows({ ...state, route }, { mods, customized });
  const title = r === "" ? "Setup skipped" : "You're all set";
  return (
    <Frame step="done" route={route} title={title} back={back}>
      {url ? <p className="wa-lead"><strong className="k-mono wa-break">{url}</strong></p>
        : r === "launcher" ? <p className="wa-lead">Open it with the Kiln Launcher.</p> : null}
      <ul className="wa-done-list" aria-label="What was set up">
        {rows.map((row) => (
          <li key={row.id} className={row.done ? "done" : "skipped"} data-row={row.id}>
            <span className={"wa-check-icon " + (row.done ? "done" : "idle")} aria-hidden>{row.done ? "✓" : ""}</span>
            <span className="wa-done-label">{row.label}{!row.done && <span className="k-muted"> skipped</span>}</span>
            <button type="button" className="k-link wa-link-btn wa-done-where" onClick={() => go(row.to)}
              aria-label={`${row.label}: change it in ${row.where}`}>{row.where === "Panel settings" ? "Settings" : row.where}</button>
          </li>
        ))}
      </ul>
      {here || !url ? <Button kind="primary" onClick={() => go("/")}>Go to the panel</Button>
        : <a className="k-btn k-btn-primary" href={url}>Open your panel</a>}
      {url && !here && <button type="button" className="k-link wa-link-btn" onClick={() => go("/")}>Stay here</button>}
    </Frame>
  );
}
