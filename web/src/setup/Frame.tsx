// The setup wizard's frame (0.1.3: shared by its pages): the server's name, the progress dots, one page, and the same
// footer everywhere (Back on the left, Skip on the right of an optional page). Technical detail goes behind Details.
import { createContext, useContext, type ReactNode } from "react";
import { useSession } from "../app/context";
import { isApiError } from "../api/http";
import { useBranding } from "../shell/useBranding";
import { errorText } from "../ui/kit";
import { position, type PageOptions, type Route, type StepId } from "./wizard";

/** Which optional pages this panel has (the dots count only those). */
export const PagesContext = createContext<PageOptions>({});

const wait = (s: number | null) => (s ? ` Try again in ${s >= 60 ? `${Math.ceil(s / 60)} min` : `${s} s`}.` : "");

/** An error in one sentence: the server's own words, capitalized and ended, or the client's. */
export const say = (e: unknown) => (isApiError(e) ? e.message.replace(/^./, (c) => c.toUpperCase()) + (/[.!?]$/.test(e.message) ? "" : ".") + wait(e.retryAfter) : errorText(e));

export function Frame({ step, route, title, children, back, skip, skipLabel = "Skip", wide }: {
  step: StepId; route: Route | ""; title: string; children: ReactNode; back?: () => void; skip?: () => void; skipLabel?: string; wide?: boolean;
}) {
  const b = useSession((s) => s.branding);
  useBranding(b, "Setup");
  const { at, of } = position(route, step, useContext(PagesContext));
  return (
    <div className="wa-auth">
      <main className={"wa-auth-card wa-wizard" + (wide ? " wa-wizard-wide" : "")} aria-labelledby="wa-wizard-title" data-step={step}>
        <div className="wa-wizard-top">
          <div className="wa-auth-brand">
            {b.logo ? <img className="wa-auth-logo" src={b.logo} alt="" /> : <span className="wa-mark wa-mark-l" aria-hidden />}
            <div className="wa-auth-server">{b.name}</div>
          </div>
          <ol className="wa-dots" aria-label={`Step ${at} of ${of}`}>
            {Array.from({ length: of }, (_, i) => <li key={i} className={i + 1 < at ? "done" : i + 1 === at ? "now" : ""} aria-hidden />)}
          </ol>
        </div>
        <h1 id="wa-wizard-title" className="wa-auth-title">{title}</h1>
        {children}
        {(back || skip) && (
          <div className="wa-wizard-foot">
            {back && <button type="button" className="k-link wa-link-btn wa-back" onClick={back}>Back</button>}
            {skip && <button type="button" className="k-link wa-link-btn wa-skip-step" onClick={skip}>{skipLabel}</button>}
          </div>
        )}
      </main>
    </div>
  );
}

export function Details({ children, label = "Details" }: { children: ReactNode; label?: string }) {
  return (
    <details className="wa-details wa-wizard-details">
      <summary>{label}</summary>
      <div className="wa-stack">{children}</div>
    </details>
  );
}
