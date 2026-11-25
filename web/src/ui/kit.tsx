// The web app's small components on the launcher's design system (the Hearth classes and tokens of
// @kiln/admin-ui/style.css: k-card, k-field, k-input, k-btn, k-pill, k-banner, k-modal...).
import { useEffect, useId, useRef, useState, type KeyboardEvent, type ReactNode } from "react";
import { createPortal } from "react-dom";
import { isApiError } from "../api/http";
import { sentence } from "./format";

export function Card({ title, sub, actions, children, className }: { title?: ReactNode; sub?: ReactNode; actions?: ReactNode; children: ReactNode; className?: string }) {
  return (
    <section className={["k-card", "wa-card", className].filter(Boolean).join(" ")}>
      {(title || sub || actions) && (
        <header className="k-card-head">
          <div className="k-card-titles">
            {title && <h2 className="k-card-title">{title}</h2>}
            {sub && <div className="k-card-sub">{sub}</div>}
          </div>
          {actions && <div className="k-card-actions">{actions}</div>}
        </header>
      )}
      <div className="k-card-body">{children}</div>
    </section>
  );
}

export function Page({ title, sub, actions, children, wide }: { title: ReactNode; sub?: ReactNode; actions?: ReactNode; children: ReactNode; wide?: boolean }) {
  return (
    <div className={"wa-page" + (wide ? " wa-page-wide" : "")}>
      <header className="wa-page-head">
        <div>
          <h1 className="wa-page-title">{title}</h1>
          {sub && <p className="wa-page-sub">{sub}</p>}
        </div>
        {actions && <div className="wa-page-actions">{actions}</div>}
      </header>
      {children}
    </div>
  );
}

export function Field({ label, hint, error, children, htmlFor, hintId }: { label: ReactNode; hint?: ReactNode; error?: ReactNode; children: ReactNode; htmlFor?: string; hintId?: string }) {
  return (
    <div className="k-field">
      <div className="k-field-head">
        <label className="k-label" htmlFor={htmlFor}>{label}</label>
      </div>
      {children}
      {error ? <div className="k-error-text" id={hintId} role="alert">{error}</div> : hint ? <div className="k-hint" id={hintId}>{hint}</div> : null}
    </div>
  );
}

/** A labelled text input (the label is the field's accessible name). */
export function TextField(props: {
  label: string; value: string; onChange: (v: string) => void; type?: string; autoComplete?: string; hint?: ReactNode; error?: ReactNode;
  placeholder?: string; mono?: boolean; autoFocus?: boolean; inputMode?: "numeric" | "text" | "decimal" | "url" | "email"; maxLength?: number; disabled?: boolean; name?: string;
  readOnly?: boolean; spellCheck?: boolean;
}) {
  const id = useId();
  const hintId = useId();
  return (
    <Field label={props.label} hint={props.hint} error={props.error} htmlFor={id} hintId={hintId}>
      <input id={id} name={props.name} className={"k-input" + (props.mono ? " k-input-mono" : "") + (props.error ? " invalid" : "")} type={props.type ?? "text"}
        value={props.value} onChange={(e) => props.onChange(e.target.value)} autoComplete={props.autoComplete} placeholder={props.placeholder}
        autoFocus={props.autoFocus} inputMode={props.inputMode} maxLength={props.maxLength} disabled={props.disabled} readOnly={props.readOnly}
        spellCheck={props.spellCheck ?? (props.mono ? false : undefined)} aria-invalid={props.error ? true : undefined}
        aria-describedby={props.error || props.hint ? hintId : undefined} />
    </Field>
  );
}

/** A whole number (or decimal) field; the text is kept while typing, the number goes out when it parses. */
export function NumberField({ label, value, onChange, min, max, step, hint, disabled, unit }: {
  label: string; value: number; onChange: (v: number) => void; min?: number; max?: number; step?: number; hint?: ReactNode; disabled?: boolean; unit?: string;
}) {
  const id = useId();
  const hintId = useId();
  const [text, setText] = useState(String(value));
  useEffect(() => {
    if (Number(text) !== value) setText(String(value));
  }, [value]); // eslint-disable-line react-hooks/exhaustive-deps
  const n = Number(text);
  const bad = text.trim() === "" || !Number.isFinite(n) || (min !== undefined && n < min) || (max !== undefined && n > max);
  const range = min !== undefined && max !== undefined ? `${min} to ${max}` : min !== undefined ? `at least ${min}` : max !== undefined ? `at most ${max}` : null;
  return (
    <Field label={unit ? `${label} (${unit})` : label} htmlFor={id} hintId={hintId} error={bad && !disabled ? `A number${range ? `, ${range}` : ""}.` : undefined} hint={hint}>
      <input id={id} className={"k-input wa-num" + (bad ? " invalid" : "")} type="number" inputMode={step && step < 1 ? "decimal" : "numeric"} min={min} max={max} step={step ?? 1}
        value={text} disabled={disabled} aria-invalid={bad || undefined} aria-describedby={hintId}
        onChange={(e) => {
          setText(e.target.value);
          const v = Number(e.target.value);
          if (e.target.value.trim() !== "" && Number.isFinite(v)) onChange(v);
        }} />
    </Field>
  );
}

/** A select with its label. */
export function SelectField<T extends string>({ label, value, onChange, options, hint, disabled }: {
  label: string; value: T; onChange: (v: T) => void; options: { value: T; label: string }[]; hint?: ReactNode; disabled?: boolean;
}) {
  const id = useId();
  const hintId = useId();
  return (
    <Field label={label} htmlFor={id} hint={hint} hintId={hintId}>
      <select id={id} className="k-select" value={value} disabled={disabled} onChange={(e) => onChange(e.target.value as T)} aria-describedby={hint ? hintId : undefined}>
        {options.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
      </select>
    </Field>
  );
}

/** A multi-line text field. */
export function TextAreaField({ label, value, onChange, hint, rows = 4, maxLength, mono, disabled, error }: {
  label: string; value: string; onChange: (v: string) => void; hint?: ReactNode; rows?: number; maxLength?: number; mono?: boolean; disabled?: boolean; error?: ReactNode;
}) {
  const id = useId();
  const hintId = useId();
  return (
    <Field label={label} htmlFor={id} hint={hint} hintId={hintId} error={error}>
      <textarea id={id} className={(mono ? "k-textarea" : "k-input wa-textarea") + (error ? " invalid" : "")} rows={rows} maxLength={maxLength} value={value} disabled={disabled}
        spellCheck={mono ? false : undefined} onChange={(e) => onChange(e.target.value)} aria-describedby={hint || error ? hintId : undefined} aria-invalid={error ? true : undefined} />
    </Field>
  );
}

/** A list of short strings, one per line (addresses, names, origins). */
export function ListField({ label, value, onChange, hint, disabled, rows = 3 }: {
  label: string; value: string[]; onChange: (v: string[]) => void; hint?: ReactNode; disabled?: boolean; rows?: number;
}) {
  const [text, setText] = useState(value.join("\n"));
  useEffect(() => {
    if (lines(text).join("\n") !== value.join("\n")) setText(value.join("\n"));
  }, [value]); // eslint-disable-line react-hooks/exhaustive-deps
  return <TextAreaField label={label} value={text} rows={rows} mono disabled={disabled} hint={hint ?? "One per line."}
    onChange={(t) => { setText(t); onChange(lines(t)); }} />;
}

/** Non-empty trimmed lines (commas also split). */
export function lines(text: string): string[] {
  return text.split(/[\n,]+/).map((x) => x.trim()).filter(Boolean);
}

/** An on/off switch (a checkbox with role switch), its label and an optional line under it. */
export function Toggle({ label, checked, onChange, hint, disabled, ariaLabel }: {
  label: ReactNode; checked: boolean; onChange: (v: boolean) => void; hint?: ReactNode; disabled?: boolean; ariaLabel?: string;
}) {
  const hintId = useId();
  return (
    <label className={"k-toggle wa-toggle" + (disabled ? " disabled" : "")}>
      <input type="checkbox" role="switch" checked={checked} disabled={disabled} onChange={(e) => onChange(e.target.checked)} aria-describedby={hint ? hintId : undefined}
        aria-label={ariaLabel} />
      <span className="k-toggle-track" aria-hidden />
      <span className="k-toggle-text">
        <span className="k-label">{label}</span>
        {hint && <span className="k-hint" id={hintId}>{hint}</span>}
      </span>
    </label>
  );
}

/** A checkbox with its label. */
export function Check({ label, checked, onChange, hint, disabled }: { label: ReactNode; checked: boolean; onChange: (v: boolean) => void; hint?: ReactNode; disabled?: boolean }) {
  return (
    <label className={"wa-check" + (disabled ? " wa-dim" : "")}>
      <input type="checkbox" className="k-check" checked={checked} disabled={disabled} onChange={(e) => onChange(e.target.checked)} />
      <span>{label}{hint && <span className="k-muted"> {hint}</span>}</span>
    </label>
  );
}

/** What is loading, said (a spinner and a line; screen readers hear it once). */
export function Loading({ label = "Loading…" }: { label?: string }) {
  return (
    <div className="wa-loading" role="status" aria-live="polite">
      <span className="k-spin" aria-hidden />
      <span>{label}</span>
    </div>
  );
}

/** Nothing to show yet: what that means and what to do. */
export function Empty({ title, children, action }: { title: ReactNode; children?: ReactNode; action?: ReactNode }) {
  return (
    <div className="k-empty wa-empty">
      <div className="k-empty-title">{title}</div>
      {children && <div className="k-empty-hint">{children}</div>}
      {action && <div className="k-empty-action">{action}</div>}
    </div>
  );
}

/** A failed read: what failed, in a sentence, and a way to try again. */
export function LoadError({ what, error, retry }: { what: string; error: unknown; retry?: () => void }) {
  return (
    <Banner tone="err" title={`${what} did not load`} actions={retry && <Button size="s" onClick={retry}>Try again</Button>}>
      {explain(error)}
    </Banner>
  );
}

/** An error as a plain sentence for the page (the server's message, or what to do for the common cases). */
export function explain(e: unknown): string {
  if (isApiError(e)) {
    if (e.code === "network") return "The panel did not answer. Check that the server is running, then try again.";
    if (e.code === "reauth_required") return "Confirm it is you in the dialog, then try again.";
    if (e.status === 401) return "Your sign-in ended. Sign in again.";
    if (e.code === "bad_response") return "The panel's answer could not be read. Reload the page.";
    const wait = e.retryAfter ? ` Try again in ${e.retryAfter >= 60 ? `${Math.ceil(e.retryAfter / 60)} min` : `${e.retryAfter} s`}.` : "";
    return sentence(e.message) + wait;
  }
  return sentence(errorText(e) || "Something went wrong.");
}

export type Tone = "ok" | "warn" | "err" | "info" | "idle" | "ember" | "gold";

export function Pill({ tone = "idle", children, title }: { tone?: Tone; children: ReactNode; title?: string }) {
  return <span className={`k-pill k-tone-${tone}`} title={title}>{children}</span>;
}

export function Banner({ tone = "info", title, children, actions }: { tone?: Tone; title?: ReactNode; children?: ReactNode; actions?: ReactNode }) {
  return (
    <div className="k-banner wa-banner" role={tone === "err" ? "alert" : "status"}>
      <div className="k-banner-body">
        {title && <div className={`k-banner-title k-tone-${tone}`}>{title}</div>}
        {children && <div className="k-banner-text">{children}</div>}
      </div>
      {actions && <div className="k-banner-actions">{actions}</div>}
    </div>
  );
}

export function Button({ kind = "default", size, children, onClick, type = "button", disabled, title, busy, ...rest }: {
  kind?: "default" | "primary" | "ghost" | "danger"; size?: "s" | "l"; children: ReactNode; onClick?: () => void; type?: "button" | "submit";
  disabled?: boolean; title?: string; busy?: boolean; "aria-label"?: string;
}) {
  const cls = ["k-btn", kind === "primary" && "k-btn-primary", kind === "ghost" && "k-btn-ghost", kind === "danger" && "k-btn-danger", size && `k-btn-${size}`]
    .filter(Boolean).join(" ");
  return (
    <button type={type} className={cls} onClick={onClick} disabled={disabled || busy} title={title} aria-busy={busy || undefined} {...rest}>
      {busy && <span className="k-spin wa-spin" aria-hidden />}
      {children}
    </button>
  );
}

/** A dialog in a portal: Escape and the backdrop close it; focus goes in and comes back. */
export function Modal({ title, onClose, children, footer, wide }: { title: ReactNode; onClose: () => void; children: ReactNode; footer?: ReactNode; wide?: boolean }) {
  const id = useId();
  const box = useRef<HTMLDivElement>(null);
  useEffect(() => {
    const before = document.activeElement as HTMLElement | null;
    // a field that took the focus itself (autoFocus) keeps it; else the first control gets it
    if (!box.current?.contains(document.activeElement)) box.current?.querySelector<HTMLElement>("input, select, textarea, button:not(.wa-modal-x)")?.focus();
    const key = (e: globalThis.KeyboardEvent) => {
      // the topmost dialog only (a confirmation over a dialog)
      const all = document.querySelectorAll(".wa-modal");
      if (all[all.length - 1] !== box.current) return;
      if (e.key === "Escape") {
        e.stopPropagation();
        onClose();
      } else if (e.key === "Tab") {
        const f = [...(box.current?.querySelectorAll<HTMLElement>("a[href], button:not(:disabled), input:not(:disabled), select:not(:disabled), textarea:not(:disabled), [tabindex]:not([tabindex='-1'])") ?? [])]
          .filter((el) => el.offsetParent !== null);
        if (!f.length) return;
        const first = f[0], last = f[f.length - 1];
        if (e.shiftKey && document.activeElement === first) { e.preventDefault(); last.focus(); }
        else if (!e.shiftKey && document.activeElement === last) { e.preventDefault(); first.focus(); }
      }
    };
    document.addEventListener("keydown", key);
    return () => {
      document.removeEventListener("keydown", key);
      before?.focus?.();
    };
  }, [onClose]);
  return createPortal(
    <div className="k-modal-backdrop wa-backdrop" onMouseDown={(e) => e.target === e.currentTarget && onClose()}>
      <div ref={box} className={"k-modal wa-modal" + (wide ? " wa-modal-wide" : "")} role="dialog" aria-modal="true" aria-labelledby={id}>
        <div className="k-modal-head">
          <h2 className="k-modal-title" id={id}>{title}</h2>
          <span className="k-spacer" />
          <button type="button" className="k-btn k-btn-ghost k-btn-s wa-modal-x" aria-label="Close" onClick={onClose}>×</button>
        </div>
        <div className="k-modal-body">{children}</div>
        {footer && <div className="k-modal-foot">{footer}</div>}
      </div>
    </div>,
    document.body,
  );
}

/** Tabs (arrow keys move between them). With `idBase`, tab i is `${idBase}-tab-${id}` and controls `${idBase}-panel-${id}`. */
export function Tabs<T extends string>({ tabs, value, onChange, label, idBase }: { tabs: { id: T; label: ReactNode }[]; value: T; onChange: (v: T) => void; label: string; idBase?: string }) {
  const ref = useRef<HTMLDivElement>(null);
  const onKey = (e: KeyboardEvent<HTMLDivElement>) => {
    const i = tabs.findIndex((t) => t.id === value);
    const j = e.key === "ArrowRight" ? i + 1 : e.key === "ArrowLeft" ? i - 1 : e.key === "Home" ? 0 : e.key === "End" ? tabs.length - 1 : null;
    if (j === null) return;
    e.preventDefault();
    const next = tabs[(j + tabs.length) % tabs.length];
    onChange(next.id);
    requestAnimationFrame(() => ref.current?.querySelector<HTMLElement>(`[data-tab="${next.id}"]`)?.focus());
  };
  return (
    <div ref={ref} className="k-tabs wa-tabs" role="tablist" aria-label={label} onKeyDown={onKey}>
      {tabs.map((t) => (
        <button key={t.id} type="button" role="tab" data-tab={t.id} aria-selected={t.id === value} tabIndex={t.id === value ? 0 : -1}
          id={idBase ? `${idBase}-tab-${t.id}` : undefined} aria-controls={idBase ? `${idBase}-panel-${t.id}` : undefined}
          className={"k-tab" + (t.id === value ? " active" : "")} onClick={() => onChange(t.id)}>
          {t.label}
        </button>
      ))}
    </div>
  );
}

/** The panel of the selected tab (see Tabs' idBase). */
export function TabPanel({ idBase, id, children }: { idBase: string; id: string; children: ReactNode }) {
  return <div role="tabpanel" id={`${idBase}-panel-${id}`} aria-labelledby={`${idBase}-tab-${id}`} className="wa-tabpanel">{children}</div>;
}

/** Text with a copy button (`block`: a multi-line block, such as a public key). */
export function Copyable({ text, label = "Copy", block, name }: { text: string; label?: string; block?: boolean; name?: string }) {
  const [done, setDone] = useState<"ok" | "no" | null>(null);
  const copy = () => {
    const ok = () => { setDone("ok"); setTimeout(() => setDone(null), 1500); };
    const no = () => { setDone("no"); setTimeout(() => setDone(null), 2500); };
    if (navigator.clipboard?.writeText) void navigator.clipboard.writeText(text).then(ok, no);
    else no();
  };
  const button = <Button size="s" kind={block ? "default" : "ghost"} onClick={copy} aria-label={name ? `${label} ${name}` : undefined}>{done === "ok" ? "Copied" : label}</Button>;
  return (
    <span className={block ? "wa-copy-block" : "wa-copy"}>
      {block ? <pre className="k-code-block wa-pre">{text}</pre> : <code className="k-mono wa-break">{text}</code>}
      <span className="wa-row">{button}{done === "no" && <span className="k-muted" role="status">Select the text and copy it by hand.</span>}</span>
    </span>
  );
}

export function when(iso: string | null | undefined): string {
  if (!iso) return "never";
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleString(undefined, { dateStyle: "medium", timeStyle: "short" });
}

export function errorText(e: unknown): string {
  if (!e) return "";
  if (e instanceof Error) return e.message;
  if (typeof e === "object" && e && "message" in e) return String((e as { message: unknown }).message);
  return String(e);
}
