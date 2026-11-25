// A form from the JSON Schema subset the server checks (JsonSchemaLite): string, integer, number, boolean, enum; the
// server validates again. Used by declarative pages' form blocks and extension settings.
import { useId, useState, type FormEvent } from "react";
import type { JsonSchema } from "../api/laneC";
import { Button } from "../ui/kit";

export function initialValues(schema: JsonSchema | null | undefined, value?: Record<string, unknown> | null): Record<string, unknown> {
  const out: Record<string, unknown> = {};
  for (const [k, p] of Object.entries(schema?.properties ?? {})) {
    const v = value?.[k] ?? p.default;
    if (v !== undefined) out[k] = v;
    else if (p.type === "boolean") out[k] = false;
  }
  return out;
}

/** The values as the schema's types (numbers from text fields); empty optional fields left out. */
export function coerce(schema: JsonSchema | null | undefined, values: Record<string, unknown>): Record<string, unknown> {
  const out: Record<string, unknown> = {};
  for (const [k, v] of Object.entries(values)) {
    const p = schema?.properties?.[k];
    if (v === "" && !schema?.required?.includes(k)) continue;
    out[k] = p && (p.type === "integer" || p.type === "number") && typeof v === "string" ? Number(v) : v;
  }
  return out;
}

export function SchemaForm({ schema, initial, submit, onSubmit, busy }: {
  schema: JsonSchema | null | undefined; initial?: Record<string, unknown> | null; submit: string; busy?: boolean; onSubmit: (v: Record<string, unknown>) => void;
}) {
  const [values, setValues] = useState(() => initialValues(schema, initial));
  const base = useId();
  const go = (e: FormEvent) => {
    e.preventDefault();
    onSubmit(coerce(schema, values));
  };
  const set = (k: string, v: unknown) => setValues((s) => ({ ...s, [k]: v }));
  return (
    <form onSubmit={go} className="wa-stack">
      {Object.entries(schema?.properties ?? {}).map(([k, p]) => {
        const id = `${base}-${k}`;
        const label = p.title ?? k;
        const v = values[k];
        return (
          <div className="k-field" key={k}>
            {p.type === "boolean" ? (
              <label className="wa-check"><input id={id} type="checkbox" checked={v === true} onChange={(e) => set(k, e.target.checked)} /> {label}</label>
            ) : (
              <>
                <label className="k-label" htmlFor={id}>{label}{schema?.required?.includes(k) ? " *" : ""}</label>
                {p.enum ? (
                  <select id={id} className="k-select" value={String(v ?? "")} onChange={(e) => set(k, p.enum!.find((x) => String(x) === e.target.value))}>
                    <option value="" disabled>Choose…</option>
                    {p.enum.map((x) => <option key={String(x)} value={String(x)}>{String(x)}</option>)}
                  </select>
                ) : (
                  <input id={id} className="k-input" type={p.type === "integer" || p.type === "number" ? "number" : "text"} value={v === undefined ? "" : String(v)}
                    min={p.minimum} max={p.maximum} maxLength={p.maxLength} step={p.type === "integer" ? 1 : "any"} required={schema?.required?.includes(k)}
                    onChange={(e) => set(k, e.target.value)} />
                )}
              </>
            )}
            {p.description && <div className="k-hint">{p.description}</div>}
          </div>
        );
      })}
      <div><Button type="submit" kind="primary" busy={busy}>{submit}</Button></div>
    </form>
  );
}
